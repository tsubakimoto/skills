using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;

internal static class OfficeSupport
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly Dictionary<string, string> SmartQuoteEntities = new()
    {
        ["\u201c"] = "&#x201C;",
        ["\u201d"] = "&#x201D;",
        ["\u2018"] = "&#x2018;",
        ["\u2019"] = "&#x2019;"
    };

    private const string LibreOfficeShimSource = """
#define _GNU_SOURCE
#include <dlfcn.h>
#include <errno.h>
#include <signal.h>
#include <stdio.h>
#include <stdlib.h>
#include <sys/socket.h>
#include <unistd.h>

static int (*real_socket)(int, int, int);
static int (*real_socketpair)(int, int, int, int[2]);
static int (*real_listen)(int, int);
static int (*real_accept)(int, struct sockaddr *, socklen_t *);
static int (*real_close)(int);
static int (*real_read)(int, void *, size_t);

static int is_shimmed[1024];
static int peer_of[1024];
static int wake_r[1024];
static int wake_w[1024];
static int listener_fd = -1;

__attribute__((constructor))
static void init(void) {
    real_socket     = dlsym(RTLD_NEXT, "socket");
    real_socketpair = dlsym(RTLD_NEXT, "socketpair");
    real_listen     = dlsym(RTLD_NEXT, "listen");
    real_accept     = dlsym(RTLD_NEXT, "accept");
    real_close      = dlsym(RTLD_NEXT, "close");
    real_read       = dlsym(RTLD_NEXT, "read");
    for (int i = 0; i < 1024; i++) {
        peer_of[i] = -1;
        wake_r[i]  = -1;
        wake_w[i]  = -1;
    }
}

int socket(int domain, int type, int protocol) {
    if (domain == AF_UNIX) {
        int fd = real_socket(domain, type, protocol);
        if (fd >= 0) return fd;
        int sv[2];
        if (real_socketpair(domain, type, protocol, sv) == 0) {
            if (sv[0] >= 0 && sv[0] < 1024) {
                is_shimmed[sv[0]] = 1;
                peer_of[sv[0]]    = sv[1];
                int wp[2];
                if (pipe(wp) == 0) {
                    wake_r[sv[0]] = wp[0];
                    wake_w[sv[0]] = wp[1];
                }
            }
            return sv[0];
        }
        errno = EPERM;
        return -1;
    }
    return real_socket(domain, type, protocol);
}

int listen(int sockfd, int backlog) {
    if (sockfd >= 0 && sockfd < 1024 && is_shimmed[sockfd]) {
        listener_fd = sockfd;
        return 0;
    }
    return real_listen(sockfd, backlog);
}

int accept(int sockfd, struct sockaddr *addr, socklen_t *addrlen) {
    if (sockfd >= 0 && sockfd < 1024 && is_shimmed[sockfd]) {
        if (wake_r[sockfd] >= 0) {
            char buf;
            real_read(wake_r[sockfd], &buf, 1);
        }
        errno = ECONNABORTED;
        return -1;
    }
    return real_accept(sockfd, addr, addrlen);
}

int close(int fd) {
    if (fd >= 0 && fd < 1024 && is_shimmed[fd]) {
        int was_listener = (fd == listener_fd);
        is_shimmed[fd] = 0;
        if (wake_w[fd] >= 0) {
            char c = 0;
            write(wake_w[fd], &c, 1);
            real_close(wake_w[fd]);
            wake_w[fd] = -1;
        }
        if (wake_r[fd] >= 0) { real_close(wake_r[fd]); wake_r[fd] = -1; }
        if (peer_of[fd] >= 0) { real_close(peer_of[fd]); peer_of[fd] = -1; }
        if (was_listener) _exit(0);
    }
    return real_close(fd);
}
""";

    public static string SourceDirectory([CallerFilePath] string filePath = "") =>
        Path.GetDirectoryName(filePath) ?? throw new InvalidOperationException("Unable to locate source directory.");

    public static int RunSoffice(string[] args, int timeoutSeconds = 300)
    {
        var info = new ProcessStartInfo
        {
            FileName = "soffice",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (var argument in args)
        {
            info.ArgumentList.Add(argument);
        }

        foreach (var pair in GetSofficeEnvironment())
        {
            info.Environment[pair.Key] = pair.Value;
        }

        using var process = Process.Start(info)
            ?? throw new InvalidOperationException("Failed to start soffice.");

        if (!process.WaitForExit(timeoutSeconds * 1000))
        {
            TryKill(process);
            return 124;
        }

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        if (!string.IsNullOrWhiteSpace(stdout))
        {
            Console.Write(stdout);
        }

        if (!string.IsNullOrWhiteSpace(stderr))
        {
            Console.Error.Write(stderr);
        }

        return process.ExitCode;
    }

    public static Dictionary<string, string> GetSofficeEnvironment()
    {
        var environment = Environment.GetEnvironmentVariables()
            .Cast<System.Collections.DictionaryEntry>()
            .ToDictionary(entry => (string)entry.Key, entry => (string?)entry.Value ?? string.Empty, StringComparer.Ordinal);

        environment["SAL_USE_VCLPLUGIN"] = "svp";

        if (OperatingSystem.IsLinux() && NeedsUnixSocketShim())
        {
            var shimPath = EnsureUnixSocketShim();
            environment["LD_PRELOAD"] = shimPath;
        }

        return environment;
    }

    public static (int Count, string Message) MergeRuns(string inputDirectory)
    {
        var documentPath = Path.Combine(inputDirectory, "word", "document.xml");
        if (!File.Exists(documentPath))
        {
            return (0, $"Error: {documentPath} not found");
        }

        try
        {
            var xml = LoadXml(documentPath);
            RemoveElementsByLocalName(xml.DocumentElement!, "proofErr");
            StripRunRsidAttributes(xml.DocumentElement!);

            var containers = FindElementsByLocalName(xml.DocumentElement!, "r")
                .Select(node => node.ParentNode)
                .OfType<XmlNode>()
                .Distinct()
                .ToList();

            var mergeCount = 0;
            foreach (var container in containers)
            {
                mergeCount += MergeRunsInContainer(container);
            }

            SaveXml(xml, documentPath, preserveEntities: false);
            return (mergeCount, $"Merged {mergeCount} runs");
        }
        catch (Exception ex)
        {
            return (0, $"Error: {ex.Message}");
        }
    }

    public static (int Count, string Message) SimplifyRedlines(string inputDirectory)
    {
        var documentPath = Path.Combine(inputDirectory, "word", "document.xml");
        if (!File.Exists(documentPath))
        {
            return (0, $"Error: {documentPath} not found");
        }

        try
        {
            var xml = LoadXml(documentPath);
            var containers = FindElementsByLocalName(xml.DocumentElement!, "p")
                .Concat(FindElementsByLocalName(xml.DocumentElement!, "tc"))
                .ToList();

            var mergeCount = 0;
            foreach (var container in containers)
            {
                mergeCount += MergeTrackedChangesInContainer(container, "ins");
                mergeCount += MergeTrackedChangesInContainer(container, "del");
            }

            SaveXml(xml, documentPath, preserveEntities: false);
            return (mergeCount, $"Simplified {mergeCount} tracked changes");
        }
        catch (Exception ex)
        {
            return (0, $"Error: {ex.Message}");
        }
    }

    public static (bool Success, string Message) UnpackOffice(
        string inputFile,
        string outputDirectory,
        bool mergeRuns = true,
        bool simplifyRedlines = true)
    {
        var inputPath = Path.GetFullPath(inputFile);
        var outputPath = Path.GetFullPath(outputDirectory);
        var suffix = Path.GetExtension(inputPath).ToLowerInvariant();

        if (!File.Exists(inputPath))
        {
            return (false, $"Error: {inputFile} does not exist");
        }

        if (suffix is not ".docx" and not ".pptx" and not ".xlsx")
        {
            return (false, $"Error: {inputFile} must be a .docx, .pptx, or .xlsx file");
        }

        try
        {
            Directory.CreateDirectory(outputPath);
            ZipFile.ExtractToDirectory(inputPath, outputPath, overwriteFiles: true);

            var xmlFiles = Directory.GetFiles(outputPath, "*.xml", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(outputPath, "*.rels", SearchOption.AllDirectories))
                .ToList();

            foreach (var xmlFile in xmlFiles)
            {
                PrettyPrintXml(xmlFile);
            }

            var message = $"Unpacked {inputFile} ({xmlFiles.Count} XML files)";
            if (suffix == ".docx")
            {
                if (simplifyRedlines)
                {
                    var (count, _) = SimplifyRedlines(outputPath);
                    message += $", simplified {count} tracked changes";
                }

                if (mergeRuns)
                {
                    var (count, _) = MergeRuns(outputPath);
                    message += $", merged {count} runs";
                }
            }

            foreach (var xmlFile in xmlFiles)
            {
                EscapeSmartQuotes(xmlFile);
            }

            return (true, message);
        }
        catch (InvalidDataException)
        {
            return (false, $"Error: {inputFile} is not a valid Office file");
        }
        catch (Exception ex)
        {
            return (false, $"Error unpacking: {ex.Message}");
        }
    }

    public static (bool Success, string Message) PackOffice(
        string inputDirectory,
        string outputFile,
        string? originalFile,
        bool validate,
        Func<string, string, string>? inferAuthor)
    {
        var inputPath = Path.GetFullPath(inputDirectory);
        var outputPath = Path.GetFullPath(outputFile);
        var suffix = Path.GetExtension(outputPath).ToLowerInvariant();

        if (!Directory.Exists(inputPath))
        {
            return (false, $"Error: {inputDirectory} is not a directory");
        }

        if (suffix is not ".docx" and not ".pptx" and not ".xlsx")
        {
            return (false, $"Error: {outputFile} must be a .docx, .pptx, or .xlsx file");
        }

        if (validate && !string.IsNullOrWhiteSpace(originalFile) && File.Exists(Path.GetFullPath(originalFile)))
        {
            var author = suffix == ".docx" && inferAuthor is not null
                ? inferAuthor(inputPath, Path.GetFullPath(originalFile))
                : "Claude";

            var validation = ValidateOffice(inputPath, Path.GetFullPath(originalFile), verbose: false, autoRepair: true, author);
            if (!string.IsNullOrWhiteSpace(validation.Output))
            {
                Console.WriteLine(validation.Output.TrimEnd());
            }

            if (!validation.Success)
            {
                return (false, $"Error: Validation failed for {inputDirectory}");
            }
        }

        var tempRoot = Path.Combine(Path.GetTempPath(), $"office-pack-{Guid.NewGuid():N}");
        var tempContent = Path.Combine(tempRoot, "content");
        try
        {
            CopyDirectory(inputPath, tempContent);
            foreach (var pattern in new[] { "*.xml", "*.rels" })
            {
                foreach (var xmlFile in Directory.GetFiles(tempContent, pattern, SearchOption.AllDirectories))
                {
                    CondenseXml(xmlFile);
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }

            ZipFile.CreateFromDirectory(tempContent, outputPath, CompressionLevel.SmallestSize, includeBaseDirectory: false);
            return (true, $"Successfully packed {inputDirectory} to {outputFile}");
        }
        catch (Exception ex)
        {
            return (false, $"Error: {ex.Message}");
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    public static ValidationResult ValidateOffice(
        string path,
        string? originalFile,
        bool verbose,
        bool autoRepair,
        string author)
    {
        var fullPath = Path.GetFullPath(path);
        var sourcePath = fullPath;
        var extension = DetermineExtension(fullPath, originalFile);
        if (extension is not ".docx" and not ".pptx")
        {
            return new ValidationResult(false, $"Error: Validation not supported for file type {extension}");
        }

        var unpackedDirectory = fullPath;
        var temporaryDirectory = string.Empty;
        if (File.Exists(fullPath))
        {
            temporaryDirectory = Path.Combine(Path.GetTempPath(), $"office-validate-{Guid.NewGuid():N}");
            Directory.CreateDirectory(temporaryDirectory);
            ZipFile.ExtractToDirectory(fullPath, temporaryDirectory, overwriteFiles: true);
            unpackedDirectory = temporaryDirectory;
        }

        try
        {
            var output = new List<string>();
            if (autoRepair)
            {
                var repairs = RepairWhitespacePreservation(unpackedDirectory);
                if (extension == ".docx")
                {
                    repairs += RepairDocxHexIdentifiers(unpackedDirectory, output);
                }

                if (repairs > 0)
                {
                    output.Add($"Auto-repaired {repairs} issue(s)");
                }
            }

            var errors = new List<string>();
            errors.AddRange(ValidateXmlWellFormedness(unpackedDirectory));
            errors.AddRange(ValidateNamespaces(unpackedDirectory));
            errors.AddRange(ValidateRelationships(unpackedDirectory));
            errors.AddRange(ValidateContentTypes(unpackedDirectory));
            errors.AddRange(ValidateOpenXmlPackage(unpackedDirectory, extension));

            if (extension == ".docx")
            {
                errors.AddRange(ValidateDocxWhitespace(unpackedDirectory));
                errors.AddRange(ValidateDocxDeletions(unpackedDirectory));
                errors.AddRange(ValidateDocxInsertions(unpackedDirectory));
                errors.AddRange(ValidateDocxCommentMarkers(unpackedDirectory));
                errors.AddRange(ValidateDocxIdentifiers(unpackedDirectory));

                if (!string.IsNullOrWhiteSpace(originalFile) && File.Exists(Path.GetFullPath(originalFile)))
                {
                    errors.AddRange(ValidateRedlining(unpackedDirectory, Path.GetFullPath(originalFile), author));
                    output.Add(CompareParagraphCounts(unpackedDirectory, Path.GetFullPath(originalFile)));
                }
            }

            if (extension == ".pptx")
            {
                errors.AddRange(ValidatePptxSlideLayouts(unpackedDirectory));
                errors.AddRange(ValidatePptxNotesSlideReferences(unpackedDirectory));
                errors.AddRange(ValidatePptxDuplicateSlideLayouts(unpackedDirectory));
            }

            if (errors.Count == 0)
            {
                output.Add("All validations PASSED!");
                return new ValidationResult(true, string.Join(Environment.NewLine, output));
            }

            output.AddRange(errors);
            return new ValidationResult(false, string.Join(Environment.NewLine, output));
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(temporaryDirectory))
            {
                TryDeleteDirectory(temporaryDirectory);
            }
        }
    }

    public static string InferTrackedChangeAuthor(string modifiedDirectory, string originalDocx, string defaultAuthor = "Claude")
    {
        var modifiedAuthors = GetTrackedChangeAuthors(Path.Combine(modifiedDirectory, "word", "document.xml"));
        if (modifiedAuthors.Count == 0)
        {
            return defaultAuthor;
        }

        var originalAuthors = GetTrackedChangeAuthorsFromDocx(originalDocx);
        var newChanges = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var pair in modifiedAuthors)
        {
            var diff = pair.Value - (originalAuthors.TryGetValue(pair.Key, out var count) ? count : 0);
            if (diff > 0)
            {
                newChanges[pair.Key] = diff;
            }
        }

        return newChanges.Count switch
        {
            0 => defaultAuthor,
            1 => newChanges.Keys.Single(),
            _ => throw new InvalidOperationException($"Multiple authors added new changes: {JsonSerializer.Serialize(newChanges, JsonOptions)}. Cannot infer which author to validate.")
        };
    }

    private static string DetermineExtension(string path, string? originalFile)
    {
        var direct = Path.GetExtension(path).ToLowerInvariant();
        return !string.IsNullOrWhiteSpace(direct)
            ? direct
            : string.IsNullOrWhiteSpace(originalFile)
                ? string.Empty
                : Path.GetExtension(originalFile).ToLowerInvariant();
    }

    private static List<string> ValidateXmlWellFormedness(string unpackedDirectory)
    {
        var errors = new List<string>();
        foreach (var xmlFile in GetXmlFiles(unpackedDirectory))
        {
            try
            {
                using var reader = XmlReader.Create(xmlFile, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore });
                while (reader.Read()) { }
            }
            catch (Exception ex)
            {
                errors.Add($"FAILED - XML: {Path.GetRelativePath(unpackedDirectory, xmlFile)}: {ex.Message}");
            }
        }

        return errors;
    }

    private static List<string> ValidateNamespaces(string unpackedDirectory)
    {
        var errors = new List<string>();
        XNamespace mc = "http://schemas.openxmlformats.org/markup-compatibility/2006";
        foreach (var xmlFile in GetXmlFiles(unpackedDirectory))
        {
            try
            {
                var root = XDocument.Load(xmlFile, LoadOptions.SetLineInfo).Root;
                if (root is null)
                {
                    continue;
                }

                var declared = root.Attributes().Where(attribute => attribute.IsNamespaceDeclaration)
                    .Select(attribute => attribute.Name.LocalName == "xmlns" ? string.Empty : attribute.Name.LocalName)
                    .ToHashSet(StringComparer.Ordinal);

                var ignorable = root.Attribute(mc + "Ignorable")?.Value;
                if (string.IsNullOrWhiteSpace(ignorable))
                {
                    continue;
                }

                foreach (var prefix in ignorable.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!declared.Contains(prefix))
                    {
                        errors.Add($"FAILED - Namespace: {Path.GetRelativePath(unpackedDirectory, xmlFile)}: Namespace '{prefix}' in Ignorable but not declared");
                    }
                }
            }
            catch
            {
            }
        }

        return errors;
    }

    private static List<string> ValidateRelationships(string unpackedDirectory)
    {
        var errors = new List<string>();
        var allFiles = Directory.GetFiles(unpackedDirectory, "*", SearchOption.AllDirectories)
            .Where(path => Path.GetFileName(path) != "[Content_Types].xml" && !path.EndsWith(".rels", StringComparison.OrdinalIgnoreCase))
            .Select(Path.GetFullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var referencedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var relsFile in Directory.GetFiles(unpackedDirectory, "*.rels", SearchOption.AllDirectories))
        {
            XDocument document;
            try
            {
                document = XDocument.Load(relsFile, LoadOptions.SetLineInfo);
            }
            catch (Exception ex)
            {
                errors.Add($"FAILED - Relationships: {Path.GetRelativePath(unpackedDirectory, relsFile)}: {ex.Message}");
                continue;
            }

            foreach (var relationship in document.Descendants().Where(element => element.Name.LocalName == "Relationship"))
            {
                var target = relationship.Attribute("Target")?.Value;
                if (string.IsNullOrWhiteSpace(target) || target.StartsWith("http", StringComparison.OrdinalIgnoreCase) || target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var targetPath = ResolveRelationshipTarget(unpackedDirectory, relsFile, target);
                if (targetPath is null || !File.Exists(targetPath))
                {
                    errors.Add($"FAILED - Relationships: {Path.GetRelativePath(unpackedDirectory, relsFile)}: Broken reference to {target}");
                }
                else
                {
                    referencedFiles.Add(targetPath);
                }
            }
        }

        foreach (var unreferenced in allFiles.Where(path => !referencedFiles.Contains(path)).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var relative = Path.GetRelativePath(unpackedDirectory, unreferenced);
            if (relative.Contains(Path.DirectorySeparatorChar + "_rels" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                relative.StartsWith("_rels", StringComparison.OrdinalIgnoreCase) ||
                relative.StartsWith("docProps", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            errors.Add($"FAILED - Relationships: Unreferenced file: {relative}");
        }

        return errors;
    }

    private static List<string> ValidateContentTypes(string unpackedDirectory)
    {
        var errors = new List<string>();
        var contentTypesPath = Path.Combine(unpackedDirectory, "[Content_Types].xml");
        if (!File.Exists(contentTypesPath))
        {
            errors.Add("FAILED - [Content_Types].xml file not found");
            return errors;
        }

        XDocument contentTypes;
        try
        {
            contentTypes = XDocument.Load(contentTypesPath);
        }
        catch (Exception ex)
        {
            errors.Add($"FAILED - Content types: {ex.Message}");
            return errors;
        }

        var declaredParts = contentTypes.Descendants().Where(element => element.Name.LocalName == "Override")
            .Select(element => element.Attribute("PartName")?.Value?.TrimStart('/'))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var declaredExtensions = contentTypes.Descendants().Where(element => element.Name.LocalName == "Default")
            .Select(element => element.Attribute("Extension")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var mediaDefaults = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["png"] = "image/png",
            ["jpg"] = "image/jpeg",
            ["jpeg"] = "image/jpeg",
            ["gif"] = "image/gif",
            ["bmp"] = "image/bmp",
            ["tiff"] = "image/tiff",
            ["wmf"] = "image/x-wmf",
            ["emf"] = "image/x-emf"
        };

        foreach (var xmlFile in GetXmlFiles(unpackedDirectory))
        {
            var relative = Path.GetRelativePath(unpackedDirectory, xmlFile).Replace('\\', '/');
            if (relative.EndsWith(".rels", StringComparison.OrdinalIgnoreCase) ||
                relative.Equals("[Content_Types].xml", StringComparison.OrdinalIgnoreCase) ||
                relative.StartsWith("docProps/", StringComparison.OrdinalIgnoreCase) ||
                relative.Contains("/_rels/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                var rootName = XDocument.Load(xmlFile).Root?.Name.LocalName;
                if (rootName is "sld" or "sldLayout" or "sldMaster" or "presentation" or "document" or "workbook" or "worksheet" or "theme")
                {
                    if (!declaredParts.Contains(relative))
                    {
                        errors.Add($"FAILED - Content types: {relative}: File with <{rootName}> root not declared in [Content_Types].xml");
                    }
                }
            }
            catch
            {
            }
        }

        foreach (var file in Directory.GetFiles(unpackedDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(unpackedDirectory, file).Replace('\\', '/');
            if (relative.Equals("[Content_Types].xml", StringComparison.OrdinalIgnoreCase) ||
                relative.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ||
                relative.EndsWith(".rels", StringComparison.OrdinalIgnoreCase) ||
                relative.StartsWith("docProps/", StringComparison.OrdinalIgnoreCase) ||
                relative.Contains("/_rels/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var extension = Path.GetExtension(file).TrimStart('.').ToLowerInvariant();
            if (mediaDefaults.TryGetValue(extension, out var contentType) && !declaredExtensions.Contains(extension))
            {
                errors.Add($"FAILED - Content types: {relative}: File with extension '{extension}' not declared in [Content_Types].xml - should add: <Default Extension=\"{extension}\" ContentType=\"{contentType}\"/>");
            }
        }

        return errors;
    }

    private static List<string> ValidateOpenXmlPackage(string unpackedDirectory, string extension)
    {
        var errors = new List<string>();
        var tempFile = Path.Combine(Path.GetTempPath(), $"office-validation-{Guid.NewGuid():N}{extension}");
        try
        {
            ZipFile.CreateFromDirectory(unpackedDirectory, tempFile, CompressionLevel.SmallestSize, includeBaseDirectory: false);
            OpenXmlPackage? package = extension switch
            {
                ".docx" => WordprocessingDocument.Open(tempFile, false),
                ".pptx" => PresentationDocument.Open(tempFile, false),
                ".xlsx" => SpreadsheetDocument.Open(tempFile, false),
                _ => null
            };

            if (package is not null)
            {
                using (package)
                {
                    var validator = new OpenXmlValidator();
                    errors.AddRange(validator.Validate(package).Select(error => $"FAILED - OpenXML: {error.Description}"));
                }
            }
        }
        catch (Exception ex)
        {
            errors.Add($"FAILED - OpenXML: {ex.Message}");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }

        return errors;
    }

    private static List<string> ValidateDocxWhitespace(string unpackedDirectory)
    {
        var errors = new List<string>();
        var documentPath = Path.Combine(unpackedDirectory, "word", "document.xml");
        if (!File.Exists(documentPath))
        {
            return errors;
        }

        var document = XDocument.Load(documentPath, LoadOptions.SetLineInfo);
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        XNamespace xmlNs = "http://www.w3.org/XML/1998/namespace";

        foreach (var element in document.Descendants(w + "t"))
        {
            var text = element.Value;
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            if ((char.IsWhiteSpace(text[0]) || char.IsWhiteSpace(text[^1])) &&
                element.Attribute(xmlNs + "space")?.Value != "preserve")
            {
                errors.Add($"FAILED - DOCX whitespace: word/document.xml: w:t element with whitespace missing xml:space='preserve': {Preview(text)}");
            }
        }

        return errors;
    }

    private static List<string> ValidateDocxDeletions(string unpackedDirectory)
    {
        var errors = new List<string>();
        var documentPath = Path.Combine(unpackedDirectory, "word", "document.xml");
        if (!File.Exists(documentPath))
        {
            return errors;
        }

        var document = XDocument.Load(documentPath, LoadOptions.SetLineInfo);
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

        foreach (var element in document.Descendants(w + "del").Descendants(w + "t"))
        {
            errors.Add($"FAILED - DOCX deletions: word/document.xml: <w:t> found within <w:del>: {Preview(element.Value)}");
        }

        foreach (var element in document.Descendants(w + "del").Descendants(w + "instrText"))
        {
            errors.Add($"FAILED - DOCX deletions: word/document.xml: <w:instrText> found within <w:del> (use <w:delInstrText>): {Preview(element.Value)}");
        }

        return errors;
    }

    private static List<string> ValidateDocxInsertions(string unpackedDirectory)
    {
        var errors = new List<string>();
        var documentPath = Path.Combine(unpackedDirectory, "word", "document.xml");
        if (!File.Exists(documentPath))
        {
            return errors;
        }

        var document = XDocument.Load(documentPath, LoadOptions.SetLineInfo);
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        foreach (var element in document.Descendants(w + "ins").Descendants(w + "delText"))
        {
            errors.Add($"FAILED - DOCX insertions: word/document.xml: <w:delText> within <w:ins>: {Preview(element.Value)}");
        }

        return errors;
    }

    private static List<string> ValidateDocxCommentMarkers(string unpackedDirectory)
    {
        var errors = new List<string>();
        var documentPath = Path.Combine(unpackedDirectory, "word", "document.xml");
        if (!File.Exists(documentPath))
        {
            return errors;
        }

        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var document = XDocument.Load(documentPath);
        var starts = document.Descendants(w + "commentRangeStart").Select(element => element.Attribute(w + "id")?.Value).Where(value => value is not null).ToHashSet();
        var ends = document.Descendants(w + "commentRangeEnd").Select(element => element.Attribute(w + "id")?.Value).Where(value => value is not null).ToHashSet();
        var references = document.Descendants(w + "commentReference").Select(element => element.Attribute(w + "id")?.Value).Where(value => value is not null).ToHashSet();

        foreach (var orphaned in ends.Except(starts).OrderBy(value => value, StringComparer.Ordinal))
        {
            errors.Add($"FAILED - DOCX comments: document.xml: commentRangeEnd id=\"{orphaned}\" has no matching commentRangeStart");
        }

        foreach (var orphaned in starts.Except(ends).OrderBy(value => value, StringComparer.Ordinal))
        {
            errors.Add($"FAILED - DOCX comments: document.xml: commentRangeStart id=\"{orphaned}\" has no matching commentRangeEnd");
        }

        var commentsPath = Path.Combine(unpackedDirectory, "word", "comments.xml");
        if (File.Exists(commentsPath))
        {
            var comments = XDocument.Load(commentsPath)
                .Descendants(w + "comment")
                .Select(element => element.Attribute(w + "id")?.Value)
                .Where(value => value is not null)
                .ToHashSet();

            foreach (var missing in starts.Union(ends).Union(references).Except(comments).OrderBy(value => value, StringComparer.Ordinal))
            {
                errors.Add($"FAILED - DOCX comments: document.xml: marker id=\"{missing}\" references non-existent comment");
            }
        }

        return errors;
    }

    private static List<string> ValidateDocxIdentifiers(string unpackedDirectory)
    {
        var errors = new List<string>();
        XNamespace w14 = "http://schemas.microsoft.com/office/word/2010/wordml";
        XNamespace w16cid = "http://schemas.microsoft.com/office/word/2016/wordml/cid";

        foreach (var xmlFile in GetXmlFiles(unpackedDirectory))
        {
            try
            {
                var document = XDocument.Load(xmlFile);
                foreach (var element in document.Descendants())
                {
                    var paraId = element.Attribute(w14 + "paraId")?.Value;
                    if (!string.IsNullOrWhiteSpace(paraId) &&
                        uint.TryParse(paraId, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsedParaId) &&
                        parsedParaId >= 0x80000000u)
                    {
                        errors.Add($"FAILED - DOCX identifiers: {Path.GetFileName(xmlFile)}: paraId={paraId} >= 0x80000000");
                    }

                    var durableId = element.Attribute(w16cid + "durableId")?.Value;
                    if (string.IsNullOrWhiteSpace(durableId))
                    {
                        continue;
                    }

                    if (Path.GetFileName(xmlFile).Equals("numbering.xml", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!int.TryParse(durableId, out var parsedDurableId) || parsedDurableId >= 0x7FFFFFFF)
                        {
                            errors.Add($"FAILED - DOCX identifiers: {Path.GetFileName(xmlFile)}: durableId={durableId} >= 0x7FFFFFFF");
                        }
                    }
                    else if (!uint.TryParse(durableId, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsedHexDurableId) ||
                             parsedHexDurableId >= 0x7FFFFFFFu)
                    {
                        errors.Add($"FAILED - DOCX identifiers: {Path.GetFileName(xmlFile)}: durableId={durableId} >= 0x7FFFFFFF");
                    }
                }
            }
            catch
            {
            }
        }

        return errors;
    }

    private static List<string> ValidateRedlining(string unpackedDirectory, string originalDocx, string author)
    {
        var errors = new List<string>();
        var modifiedPath = Path.Combine(unpackedDirectory, "word", "document.xml");
        if (!File.Exists(modifiedPath))
        {
            errors.Add($"FAILED - Modified document.xml not found at {modifiedPath}");
            return errors;
        }

        var modifiedText = GetDocumentTextWithoutAuthorChanges(modifiedPath, author);
        string originalText;
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"office-redlining-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDirectory);
            ZipFile.ExtractToDirectory(originalDocx, tempDirectory, overwriteFiles: true);
            var originalPath = Path.Combine(tempDirectory, "word", "document.xml");
            if (!File.Exists(originalPath))
            {
                errors.Add($"FAILED - Original document.xml not found in {originalDocx}");
                return errors;
            }

            originalText = GetDocumentTextWithoutAuthorChanges(originalPath, author);
        }
        catch (Exception ex)
        {
            errors.Add($"FAILED - Redlining: {ex.Message}");
            return errors;
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }

        if (!string.Equals(modifiedText, originalText, StringComparison.Ordinal))
        {
            errors.Add($"FAILED - Document text doesn't match after removing {author}'s tracked changes");
        }

        return errors;
    }

    private static string CompareParagraphCounts(string unpackedDirectory, string originalDocx)
    {
        var newCount = CountParagraphs(Path.Combine(unpackedDirectory, "word", "document.xml"));
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"office-paragraphs-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDirectory);
            ZipFile.ExtractToDirectory(originalDocx, tempDirectory, overwriteFiles: true);
            var originalCount = CountParagraphs(Path.Combine(tempDirectory, "word", "document.xml"));
            var diff = newCount - originalCount;
            var diffText = diff > 0 ? $"+{diff}" : diff.ToString(CultureInfo.InvariantCulture);
            return $"{Environment.NewLine}Paragraphs: {originalCount} → {newCount} ({diffText})";
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    private static List<string> ValidatePptxSlideLayouts(string unpackedDirectory)
    {
        var errors = new List<string>();
        XNamespace r = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        var slideMastersDirectory = Path.Combine(unpackedDirectory, "ppt", "slideMasters");
        if (!Directory.Exists(slideMastersDirectory))
        {
            return errors;
        }

        foreach (var slideMaster in Directory.GetFiles(slideMastersDirectory, "*.xml", SearchOption.TopDirectoryOnly))
        {
            var relationshipsPath = Path.Combine(Path.GetDirectoryName(slideMaster)!, "_rels", $"{Path.GetFileName(slideMaster)}.rels");
            if (!File.Exists(relationshipsPath))
            {
                errors.Add($"FAILED - PPTX slide layouts: Missing relationships file: {Path.GetRelativePath(unpackedDirectory, relationshipsPath)}");
                continue;
            }

            var validRelationshipIds = XDocument.Load(relationshipsPath).Descendants()
                .Where(element => element.Name.LocalName == "Relationship" && (element.Attribute("Type")?.Value?.Contains("slideLayout", StringComparison.OrdinalIgnoreCase) ?? false))
                .Select(element => element.Attribute("Id")?.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToHashSet(StringComparer.Ordinal);

            var masterDocument = XDocument.Load(slideMaster);
            foreach (var layoutId in masterDocument.Descendants().Where(element => element.Name.LocalName == "sldLayoutId"))
            {
                var relationshipId = layoutId.Attribute(r + "id")?.Value;
                var id = layoutId.Attribute("id")?.Value;
                if (!string.IsNullOrWhiteSpace(relationshipId) && !validRelationshipIds.Contains(relationshipId))
                {
                    errors.Add($"FAILED - PPTX slide layouts: {Path.GetRelativePath(unpackedDirectory, slideMaster)}: sldLayoutId with id='{id}' references r:id='{relationshipId}' which is not found in slide layout relationships");
                }
            }
        }

        return errors;
    }

    private static List<string> ValidatePptxDuplicateSlideLayouts(string unpackedDirectory)
    {
        var errors = new List<string>();
        var relsDirectory = Path.Combine(unpackedDirectory, "ppt", "slides", "_rels");
        if (!Directory.Exists(relsDirectory))
        {
            return errors;
        }

        foreach (var relsFile in Directory.GetFiles(relsDirectory, "*.xml.rels", SearchOption.TopDirectoryOnly))
        {
            var count = XDocument.Load(relsFile).Descendants()
                .Count(element => element.Name.LocalName == "Relationship" && (element.Attribute("Type")?.Value?.Contains("slideLayout", StringComparison.OrdinalIgnoreCase) ?? false));
            if (count > 1)
            {
                errors.Add($"FAILED - PPTX duplicate layouts: {Path.GetRelativePath(unpackedDirectory, relsFile)} has {count} slideLayout references");
            }
        }

        return errors;
    }

    private static List<string> ValidatePptxNotesSlideReferences(string unpackedDirectory)
    {
        var errors = new List<string>();
        var notesReferences = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var relsDirectory = Path.Combine(unpackedDirectory, "ppt", "slides", "_rels");
        if (!Directory.Exists(relsDirectory))
        {
            return errors;
        }

        foreach (var relsFile in Directory.GetFiles(relsDirectory, "*.xml.rels", SearchOption.TopDirectoryOnly))
        {
            var relationships = XDocument.Load(relsFile).Descendants().Where(element => element.Name.LocalName == "Relationship");
            foreach (var relationship in relationships)
            {
                var type = relationship.Attribute("Type")?.Value ?? string.Empty;
                if (!type.Contains("notesSlide", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var target = relationship.Attribute("Target")?.Value?.Replace("../", string.Empty, StringComparison.Ordinal) ?? string.Empty;
                if (!notesReferences.TryGetValue(target, out var slides))
                {
                    slides = [];
                    notesReferences[target] = slides;
                }

                slides.Add(Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(relsFile)));
            }
        }

        foreach (var pair in notesReferences.Where(pair => pair.Value.Count > 1))
        {
            errors.Add($"FAILED - PPTX notes slides: Notes slide '{pair.Key}' is referenced by multiple slides: {string.Join(", ", pair.Value)}");
        }

        return errors;
    }

    private static int RepairWhitespacePreservation(string unpackedDirectory)
    {
        var repairs = 0;
        XNamespace xmlNs = "http://www.w3.org/XML/1998/namespace";
        foreach (var xmlFile in GetXmlFiles(unpackedDirectory))
        {
            try
            {
                var document = XDocument.Load(xmlFile, LoadOptions.PreserveWhitespace);
                var modified = false;
                foreach (var element in document.Descendants().Where(element => element.Name.LocalName == "t"))
                {
                    var text = element.Value;
                    if (string.IsNullOrEmpty(text))
                    {
                        continue;
                    }

                    if ((text.StartsWith(' ') || text.StartsWith('\t') || text.EndsWith(' ') || text.EndsWith('\t')) &&
                        element.Attribute(xmlNs + "space")?.Value != "preserve")
                    {
                        element.SetAttributeValue(xmlNs + "space", "preserve");
                        modified = true;
                        repairs++;
                    }
                }

                if (modified)
                {
                    document.Save(xmlFile, SaveOptions.DisableFormatting);
                }
            }
            catch
            {
            }
        }

        return repairs;
    }

    private static int RepairDocxHexIdentifiers(string unpackedDirectory, List<string> output)
    {
        var repairs = 0;
        XNamespace w14 = "http://schemas.microsoft.com/office/word/2010/wordml";
        XNamespace w16cid = "http://schemas.microsoft.com/office/word/2016/wordml/cid";
        var random = new Random();

        foreach (var xmlFile in GetXmlFiles(unpackedDirectory))
        {
            try
            {
                var document = XDocument.Load(xmlFile, LoadOptions.PreserveWhitespace);
                var modified = false;
                foreach (var element in document.Descendants())
                {
                    var paraId = element.Attribute(w14 + "paraId");
                    if (paraId is not null &&
                        (!uint.TryParse(paraId.Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsedParaId) || parsedParaId >= 0x80000000u))
                    {
                        var newValue = random.NextInt64(1, 0x7FFFFFFF).ToString("X8", CultureInfo.InvariantCulture);
                        output.Add($"  Repaired: {Path.GetFileName(xmlFile)}: paraId {paraId.Value} → {newValue}");
                        paraId.Value = newValue;
                        modified = true;
                        repairs++;
                    }

                    var durableId = element.Attribute(w16cid + "durableId");
                    if (durableId is null)
                    {
                        continue;
                    }

                    var isNumbering = Path.GetFileName(xmlFile).Equals("numbering.xml", StringComparison.OrdinalIgnoreCase);
                    var valid = isNumbering
                        ? int.TryParse(durableId.Value, out var decimalId) && decimalId < 0x7FFFFFFF
                        : int.TryParse(durableId.Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hexId) && hexId < 0x7FFFFFFF;
                    if (valid)
                    {
                        continue;
                    }

                    var replacement = random.NextInt64(1, 0x7FFFFFFF);
                    var newDurableId = isNumbering
                        ? replacement.ToString(CultureInfo.InvariantCulture)
                        : replacement.ToString("X8", CultureInfo.InvariantCulture);
                    output.Add($"  Repaired: {Path.GetFileName(xmlFile)}: durableId {durableId.Value} → {newDurableId}");
                    durableId.Value = newDurableId;
                    modified = true;
                    repairs++;
                }

                if (modified)
                {
                    document.Save(xmlFile, SaveOptions.DisableFormatting);
                }
            }
            catch
            {
            }
        }

        return repairs;
    }

    private static Dictionary<string, int> GetTrackedChangeAuthors(string documentXmlPath)
    {
        var authors = new Dictionary<string, int>(StringComparer.Ordinal);
        if (!File.Exists(documentXmlPath))
        {
            return authors;
        }

        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var document = XDocument.Load(documentXmlPath);
        foreach (var tag in new[] { w + "ins", w + "del" })
        {
            foreach (var element in document.Descendants(tag))
            {
                var author = element.Attribute(w + "author")?.Value;
                if (!string.IsNullOrWhiteSpace(author))
                {
                    authors[author] = authors.TryGetValue(author, out var count) ? count + 1 : 1;
                }
            }
        }

        return authors;
    }

    private static Dictionary<string, int> GetTrackedChangeAuthorsFromDocx(string path)
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"office-authors-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDirectory);
            ZipFile.ExtractToDirectory(path, tempDirectory, overwriteFiles: true);
            return GetTrackedChangeAuthors(Path.Combine(tempDirectory, "word", "document.xml"));
        }
        catch
        {
            return new Dictionary<string, int>(StringComparer.Ordinal);
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    private static string GetDocumentTextWithoutAuthorChanges(string documentXmlPath, string author)
    {
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var document = XDocument.Load(documentXmlPath, LoadOptions.PreserveWhitespace);

        foreach (var insertion in document.Descendants(w + "ins").Where(element => element.Attribute(w + "author")?.Value == author).ToList())
        {
            insertion.Remove();
        }

        foreach (var deletion in document.Descendants(w + "del").Where(element => element.Attribute(w + "author")?.Value == author).ToList())
        {
            deletion.Name = w + "restored";
            foreach (var delText in deletion.Descendants(w + "delText").ToList())
            {
                delText.Name = w + "t";
            }

            deletion.ReplaceWith(deletion.Nodes());
        }

        return string.Join(
            "\n",
            document.Descendants(w + "p")
                .Select(paragraph => string.Concat(paragraph.Descendants(w + "t").Select(text => text.Value)))
                .Where(text => !string.IsNullOrEmpty(text)));
    }

    private static int CountParagraphs(string documentXmlPath)
    {
        if (!File.Exists(documentXmlPath))
        {
            return 0;
        }

        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        return XDocument.Load(documentXmlPath).Descendants(w + "p").Count();
    }

    private static XmlDocument LoadXml(string path)
    {
        var document = new XmlDocument { PreserveWhitespace = true };
        document.Load(path);
        return document;
    }

    private static void SaveXml(XmlDocument document, string path, bool preserveEntities)
    {
        var settings = new XmlWriterSettings
        {
            Indent = true,
            Encoding = new UTF8Encoding(false),
            OmitXmlDeclaration = false,
            NewLineHandling = NewLineHandling.None
        };

        using var writer = XmlWriter.Create(path, settings);
        document.Save(writer);
        if (preserveEntities)
        {
            EscapeSmartQuotes(path);
        }
    }

    private static void PrettyPrintXml(string path)
    {
        try
        {
            var document = LoadXml(path);
            SaveXml(document, path, preserveEntities: false);
        }
        catch
        {
        }
    }

    private static void CondenseXml(string path)
    {
        var document = LoadXml(path);
        RemoveWhitespaceNodes(document.DocumentElement!);
        SaveXml(document, path, preserveEntities: false);
    }

    private static void RemoveWhitespaceNodes(XmlNode node)
    {
        if (node is XmlElement element && element.LocalName == "t")
        {
            return;
        }

        for (var index = node.ChildNodes.Count - 1; index >= 0; index--)
        {
            var child = node.ChildNodes[index];
            if (child.NodeType is XmlNodeType.Whitespace or XmlNodeType.SignificantWhitespace or XmlNodeType.Comment)
            {
                node.RemoveChild(child);
                continue;
            }

            if (child.NodeType == XmlNodeType.Text && string.IsNullOrWhiteSpace(child.Value ?? string.Empty))
            {
                node.RemoveChild(child);
                continue;
            }

            RemoveWhitespaceNodes(child);
        }
    }

    private static void EscapeSmartQuotes(string path)
    {
        var content = File.ReadAllText(path, Encoding.UTF8);
        foreach (var pair in SmartQuoteEntities)
        {
            content = content.Replace(pair.Key, pair.Value, StringComparison.Ordinal);
        }

        File.WriteAllText(path, content, new UTF8Encoding(false));
    }

    private static IEnumerable<string> GetXmlFiles(string directory) =>
        Directory.GetFiles(directory, "*.xml", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(directory, "*.rels", SearchOption.AllDirectories));

    private static bool NeedsUnixSocketShim()
    {
        try
        {
            using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            return false;
        }
        catch
        {
            return true;
        }
    }

    private static string EnsureUnixSocketShim()
    {
        var sharedObjectPath = Path.Combine(Path.GetTempPath(), "lo_socket_shim.so");
        if (File.Exists(sharedObjectPath))
        {
            return sharedObjectPath;
        }

        var sourcePath = Path.Combine(Path.GetTempPath(), "lo_socket_shim.c");
        File.WriteAllText(sourcePath, LibreOfficeShimSource);

        var info = new ProcessStartInfo
        {
            FileName = "gcc",
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        info.ArgumentList.Add("-shared");
        info.ArgumentList.Add("-fPIC");
        info.ArgumentList.Add("-o");
        info.ArgumentList.Add(sharedObjectPath);
        info.ArgumentList.Add(sourcePath);
        info.ArgumentList.Add("-ldl");

        using var process = Process.Start(info)
            ?? throw new InvalidOperationException("Failed to start gcc.");
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(process.StandardError.ReadToEnd());
        }

        File.Delete(sourcePath);
        return sharedObjectPath;
    }

    private static XmlElement? GetChild(XmlNode parent, string localName) =>
        parent.ChildNodes.OfType<XmlElement>().FirstOrDefault(child => child.LocalName == localName);

    private static List<XmlElement> GetChildren(XmlNode parent, string localName) =>
        parent.ChildNodes.OfType<XmlElement>().Where(child => child.LocalName == localName).ToList();

    private static List<XmlElement> FindElementsByLocalName(XmlNode root, string localName)
    {
        var results = new List<XmlElement>();
        Traverse(root);
        return results;

        void Traverse(XmlNode node)
        {
            if (node is XmlElement element && element.LocalName == localName)
            {
                results.Add(element);
            }

            foreach (XmlNode child in node.ChildNodes)
            {
                Traverse(child);
            }
        }
    }

    private static void RemoveElementsByLocalName(XmlNode root, string localName)
    {
        foreach (var element in FindElementsByLocalName(root, localName))
        {
            element.ParentNode?.RemoveChild(element);
        }
    }

    private static void StripRunRsidAttributes(XmlNode root)
    {
        foreach (var run in FindElementsByLocalName(root, "r"))
        {
            var attributes = run.Attributes?.Cast<XmlAttribute>().Where(attribute => attribute.Name.Contains("rsid", StringComparison.OrdinalIgnoreCase)).ToList() ?? [];
            foreach (var attribute in attributes)
            {
                run.Attributes?.Remove(attribute);
            }
        }
    }

    private static int MergeRunsInContainer(XmlNode container)
    {
        var count = 0;
        var run = container.ChildNodes.OfType<XmlElement>().FirstOrDefault(child => child.LocalName == "r");
        while (run is not null)
        {
            while (true)
            {
                var next = NextElementSibling(run);
                if (next is not null && next.LocalName == "r" && CanMergeRuns(run, next))
                {
                    MergeRunContent(run, next);
                    container.RemoveChild(next);
                    count++;
                }
                else
                {
                    break;
                }
            }

            ConsolidateRunText(run);
            run = NextElementSibling(run)?.LocalName == "r" ? NextElementSibling(run) : FindNextRun(run);
        }

        return count;
    }

    private static XmlElement? FindNextRun(XmlNode node)
    {
        var sibling = node.NextSibling;
        while (sibling is not null)
        {
            if (sibling is XmlElement element && element.LocalName == "r")
            {
                return element;
            }

            sibling = sibling.NextSibling;
        }

        return null;
    }

    private static XmlElement? NextElementSibling(XmlNode node)
    {
        var sibling = node.NextSibling;
        while (sibling is not null)
        {
            if (sibling is XmlElement element)
            {
                return element;
            }

            sibling = sibling.NextSibling;
        }

        return null;
    }

    private static bool CanMergeRuns(XmlElement first, XmlElement second)
    {
        var firstProperties = GetChild(first, "rPr");
        var secondProperties = GetChild(second, "rPr");
        if ((firstProperties is null) != (secondProperties is null))
        {
            return false;
        }

        return firstProperties is null || firstProperties.OuterXml == secondProperties?.OuterXml;
    }

    private static void MergeRunContent(XmlElement target, XmlElement source)
    {
        foreach (var child in source.ChildNodes.Cast<XmlNode>().Where(child => child is XmlElement element && element.LocalName != "rPr").ToList())
        {
            target.AppendChild(child);
        }
    }

    private static void ConsolidateRunText(XmlElement run)
    {
        var texts = GetChildren(run, "t");
        for (var index = texts.Count - 1; index > 0; index--)
        {
            var current = texts[index];
            var previous = texts[index - 1];
            if (!AreAdjacent(previous, current))
            {
                continue;
            }

            var merged = (previous.InnerText ?? string.Empty) + (current.InnerText ?? string.Empty);
            previous.InnerText = merged;
            if (merged.StartsWith(' ') || merged.EndsWith(' '))
            {
                previous.SetAttribute("xml:space", "preserve");
            }
            else if (previous.HasAttribute("xml:space"))
            {
                previous.RemoveAttribute("xml:space");
            }

            run.RemoveChild(current);
        }
    }

    private static bool AreAdjacent(XmlNode first, XmlNode second)
    {
        var node = first.NextSibling;
        while (node is not null)
        {
            if (ReferenceEquals(node, second))
            {
                return true;
            }

            if (node.NodeType == XmlNodeType.Element)
            {
                return false;
            }

            if (node.NodeType == XmlNodeType.Text && !string.IsNullOrWhiteSpace(node.Value))
            {
                return false;
            }

            node = node.NextSibling;
        }

        return false;
    }

    private static int MergeTrackedChangesInContainer(XmlNode container, string localName)
    {
        var tracked = container.ChildNodes.OfType<XmlElement>().Where(child => child.LocalName == localName).ToList();
        if (tracked.Count < 2)
        {
            return 0;
        }

        var count = 0;
        var index = 0;
        while (index < tracked.Count - 1)
        {
            var current = tracked[index];
            var next = tracked[index + 1];
            if (GetAuthor(current) == GetAuthor(next) && AreAdjacent(current, next))
            {
                while (next.FirstChild is not null)
                {
                    current.AppendChild(next.FirstChild);
                }

                container.RemoveChild(next);
                tracked.RemoveAt(index + 1);
                count++;
            }
            else
            {
                index++;
            }
        }

        return count;
    }

    private static string GetAuthor(XmlElement element) =>
        element.GetAttribute("w:author") switch
        {
            { Length: > 0 } author => author,
            _ => element.Attributes?.Cast<XmlAttribute>().FirstOrDefault(attribute => attribute.LocalName == "author")?.Value ?? string.Empty
        };

    private static string? ResolveRelationshipTarget(string unpackedDirectory, string relsFile, string target)
    {
        try
        {
            if (target.StartsWith("/", StringComparison.Ordinal))
            {
                return Path.GetFullPath(Path.Combine(unpackedDirectory, target.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
            }

            if (Path.GetFileName(relsFile).Equals(".rels", StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFullPath(Path.Combine(unpackedDirectory, target.Replace('/', Path.DirectorySeparatorChar)));
            }

            var baseDirectory = Directory.GetParent(Path.GetDirectoryName(relsFile)!)?.FullName;
            if (baseDirectory is null)
            {
                return null;
            }

            return Path.GetFullPath(Path.Combine(baseDirectory, target.Replace('/', Path.DirectorySeparatorChar)));
        }
        catch
        {
            return null;
        }
    }

    private static string Preview(string value)
    {
        var text = value.Replace(Environment.NewLine, " ", StringComparison.Ordinal);
        return text.Length <= 50 ? text : $"{text[..50]}...";
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        }

        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }
    }
}

internal sealed record ValidationResult(bool Success, string Output);

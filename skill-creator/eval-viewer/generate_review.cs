#:property PublishAot=false
#:include ..\scripts\SkillCreatorSupport.cs
#:include ReviewViewerSupport.cs

using System.Net;
using System.Text;
using System.Text.Json;

return await GenerateReviewCli.RunAsync(args);

static class GenerateReviewCli
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args.Any(IsHelpToken))
        {
            PrintUsage();
            return args.Length == 0 ? 1 : 0;
        }

        var workspace = Path.GetFullPath(args[0]);
        var port = 3117;
        string? skillName = null;
        string? previousWorkspace = null;
        string? benchmarkPath = null;
        string? staticOutput = null;

        for (var index = 1; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--port":
                case "-p":
                    port = int.Parse(RequireValue(args, ref index, args[index]));
                    break;
                case "--skill-name":
                case "-n":
                    skillName = RequireValue(args, ref index, args[index]);
                    break;
                case "--previous-workspace":
                    previousWorkspace = RequireValue(args, ref index, "--previous-workspace");
                    break;
                case "--benchmark":
                    benchmarkPath = RequireValue(args, ref index, "--benchmark");
                    break;
                case "--static":
                case "-s":
                    staticOutput = RequireValue(args, ref index, args[index]);
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {args[index]}");
            }
        }

        if (!Directory.Exists(workspace))
        {
            Console.Error.WriteLine($"Error: {workspace} is not a directory");
            return 1;
        }

        var runs = ReviewViewerSupport.FindRuns(workspace);
        if (runs.Count == 0)
        {
            Console.Error.WriteLine($"No runs found in {workspace}");
            return 1;
        }

        skillName ??= Path.GetFileName(workspace).Replace("-workspace", string.Empty, StringComparison.Ordinal);
        var feedbackPath = Path.Combine(workspace, "feedback.json");
        var previous = !string.IsNullOrWhiteSpace(previousWorkspace) && Directory.Exists(Path.GetFullPath(previousWorkspace))
            ? ReviewViewerSupport.LoadPreviousIteration(Path.GetFullPath(previousWorkspace))
            : null;

        JsonElement? benchmark = null;
        if (!string.IsNullOrWhiteSpace(benchmarkPath) && File.Exists(Path.GetFullPath(benchmarkPath)))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(Path.GetFullPath(benchmarkPath)));
            benchmark = document.RootElement.Clone();
        }

        if (!string.IsNullOrWhiteSpace(staticOutput))
        {
            var html = ReviewViewerSupport.GenerateHtml(runs, skillName, previous, benchmark);
            var fullOutput = Path.GetFullPath(staticOutput);
            var directory = Path.GetDirectoryName(fullOutput);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(fullOutput, html);
            Console.WriteLine();
            Console.WriteLine($"  Static viewer written to: {fullOutput}");
            Console.WriteLine();
            return 0;
        }

        HttpListener listener;
        try
        {
            listener = CreateListener(port);
        }
        catch (HttpListenerException)
        {
            port = ReviewViewerSupport.GetAvailablePort();
            listener = CreateListener(port);
        }

        var url = $"http://localhost:{port}/";
        Console.WriteLine();
        Console.WriteLine("  Eval Viewer");
        Console.WriteLine("  ─────────────────────────────────");
        Console.WriteLine($"  URL:       {url}");
        Console.WriteLine($"  Workspace: {workspace}");
        Console.WriteLine($"  Feedback:  {feedbackPath}");
        if (previous is not null)
        {
            Console.WriteLine($"  Previous:  {Path.GetFullPath(previousWorkspace!)} ({previous.Count} runs)");
        }

        if (!string.IsNullOrWhiteSpace(benchmarkPath))
        {
            Console.WriteLine($"  Benchmark: {Path.GetFullPath(benchmarkPath)}");
        }

        Console.WriteLine();
        Console.WriteLine("  Press Ctrl+C to stop.");
        Console.WriteLine();

        SkillCreatorSupport.TryOpenBrowser(url);

        using var shutdown = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            shutdown.Cancel();
            listener.Stop();
        };

        try
        {
            while (!shutdown.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = await listener.GetContextAsync();
                }
                catch (HttpListenerException) when (shutdown.IsCancellationRequested)
                {
                    break;
                }
                catch (ObjectDisposedException) when (shutdown.IsCancellationRequested)
                {
                    break;
                }

                _ = Task.Run(() => HandleRequestAsync(context, workspace, feedbackPath, skillName, previous, benchmark), shutdown.Token);
            }
        }
        finally
        {
            listener.Close();
        }

        Console.WriteLine("Stopped.");
        return 0;
    }

    private static HttpListener CreateListener(int port)
    {
        var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        return listener;
    }

    private static async Task HandleRequestAsync(
        HttpListenerContext context,
        string workspace,
        string feedbackPath,
        string skillName,
        IReadOnlyDictionary<string, PreviousRunData>? previous,
        JsonElement? benchmark)
    {
        try
        {
            var requestPath = context.Request.Url?.AbsolutePath ?? "/";
            switch (requestPath)
            {
                case "/":
                case "/index.html":
                    var runs = ReviewViewerSupport.FindRuns(workspace);
                    await WriteResponseAsync(context.Response, "text/html; charset=utf-8", ReviewViewerSupport.GenerateHtml(runs, skillName, previous, benchmark));
                    break;

                case "/api/feedback" when context.Request.HttpMethod == "GET":
                    var content = File.Exists(feedbackPath) ? File.ReadAllText(feedbackPath) : "{}";
                    await WriteResponseAsync(context.Response, "application/json", content);
                    break;

                case "/api/feedback" when context.Request.HttpMethod == "POST":
                    using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding ?? Encoding.UTF8))
                    {
                        var body = await reader.ReadToEndAsync();
                        using var document = JsonDocument.Parse(body);
                        if (!document.RootElement.TryGetProperty("reviews", out _))
                        {
                            throw new InvalidOperationException("Expected JSON object with 'reviews' key");
                        }

                        File.WriteAllText(feedbackPath, JsonSerializer.Serialize(document.RootElement, SkillCreatorSupport.PrettyJson) + Environment.NewLine);
                    }

                    await WriteResponseAsync(context.Response, "application/json", "{\"ok\":true}");
                    break;

                default:
                    context.Response.StatusCode = 404;
                    await WriteResponseAsync(context.Response, "text/plain; charset=utf-8", "Not Found");
                    break;
            }
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = 500;
            await WriteResponseAsync(context.Response, "application/json", JsonSerializer.Serialize(new { error = ex.Message }));
        }
    }

    private static async Task WriteResponseAsync(HttpListenerResponse response, string contentType, string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        response.ContentType = contentType;
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes);
        response.OutputStream.Close();
    }

    private static string RequireValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"Missing value for {option}");
        }

        index++;
        return args[index];
    }

    private static bool IsHelpToken(string value) =>
        value is "-h" or "--help" or "/?";

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: dotnet run --file eval-viewer\\generate_review.cs -- <workspace> [--port <n>] [--skill-name <name>] [--previous-workspace <path>] [--benchmark <path>] [--static <output>]");
    }
}

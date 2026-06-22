#:property PublishAot=false
#:package DocumentFormat.OpenXml@3.2.0
#:include office\OfficeSupport.cs
#:include DocxCommentSupport.cs

if (args.Length < 3)
{
    Console.Error.WriteLine("Usage: dotnet run --file comment.cs -- <unpacked-dir> <comment-id> <text> [--author <name>] [--initials <initials>] [--parent <id>]");
    Environment.Exit(2);
}

var unpackedDir = args[0];
if (!int.TryParse(args[1], out var commentId))
{
    Console.Error.WriteLine($"Error: Invalid comment ID: {args[1]}");
    Environment.Exit(1);
}

var text = args[2];
var author = "Claude";
var initials = "C";
int? parentId = null;
for (var index = 3; index < args.Length; index++)
{
    switch (args[index])
    {
        case "--author":
            author = args[++index];
            break;
        case "--initials":
            initials = args[++index];
            break;
        case "--parent":
            parentId = int.Parse(args[++index]);
            break;
        default:
            Console.Error.WriteLine($"Error: Unknown argument: {args[index]}");
            Environment.Exit(2);
            break;
    }
}

var (paraId, message) = DocxCommentSupport.AddComment(unpackedDir, commentId, text, author, initials, parentId);
if (message.StartsWith("Error:", StringComparison.Ordinal))
{
    Console.Error.WriteLine(message);
    Environment.Exit(1);
}

Console.WriteLine(message);
Console.WriteLine(parentId is null
    ? DocxCommentSupport.GetCommentMarkerTemplate(commentId)
    : DocxCommentSupport.GetReplyMarkerTemplate(parentId.Value, commentId));

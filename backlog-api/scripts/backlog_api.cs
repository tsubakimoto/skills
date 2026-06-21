#:property PublishAot=false

using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

Console.OutputEncoding = System.Text.Encoding.UTF8;

return await BacklogCli.RunAsync(args);

static class BacklogCli
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args.Any(IsHelpToken))
        {
            PrintUsage();
            return args.Length == 0 ? 1 : 0;
        }

        try
        {
            var (globalOptions, command, commandArgs) = ParseArguments(args);

            using var client = new BacklogClient(globalOptions.Space, globalOptions.ApiKey, globalOptions.Tld);
            var result = command switch
            {
                "get-space" => await client.GetSpaceAsync(),
                "get-users" => await client.GetUsersAsync(),
                "get-projects" => await client.GetProjectsAsync(),
                "get-issues" => await ExecuteGetIssuesAsync(client, commandArgs),
                "get-issue" => await ExecuteGetIssueAsync(client, commandArgs),
                "add-issue" => await ExecuteAddIssueAsync(client, commandArgs),
                "update-issue" => await ExecuteUpdateIssueAsync(client, commandArgs),
                "add-comment" => await ExecuteAddCommentAsync(client, commandArgs),
                _ => throw new CliException($"Unknown command '{command}'.")
            };

            Console.WriteLine(result?.ToJsonString(JsonOptions) ?? "null");
            return 0;
        }
        catch (CliException ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine();
            PrintUsage();
            return 1;
        }
        catch (BacklogApiException ex)
        {
            Console.Error.WriteLine($"HTTPエラー: {(int)ex.StatusCode} {ex.ResponseBody}");
            return 1;
        }
    }

    private static async Task<JsonNode?> ExecuteGetIssuesAsync(BacklogClient client, IReadOnlyList<string> args)
    {
        int? projectId = null;
        List<int>? statusIds = null;
        List<int>? assigneeIds = null;
        string? keyword = null;
        var count = 20;
        var offset = 0;
        var order = "desc";

        for (var index = 0; index < args.Count;)
        {
            switch (args[index])
            {
                case "--project-id":
                    projectId = ReadIntOption(args, ref index, "--project-id");
                    break;
                case "--status-id":
                    statusIds = ReadIntListOption(args, ref index, "--status-id");
                    break;
                case "--assignee-id":
                    assigneeIds = ReadIntListOption(args, ref index, "--assignee-id");
                    break;
                case "--keyword":
                    keyword = ReadStringOption(args, ref index, "--keyword");
                    break;
                case "--count":
                    count = ReadIntOption(args, ref index, "--count");
                    break;
                case "--offset":
                    offset = ReadIntOption(args, ref index, "--offset");
                    break;
                case "--order":
                    order = ReadStringOption(args, ref index, "--order");
                    if (!string.Equals(order, "asc", StringComparison.Ordinal) &&
                        !string.Equals(order, "desc", StringComparison.Ordinal))
                    {
                        throw new CliException("--order must be 'asc' or 'desc'.");
                    }

                    break;
                default:
                    throw new CliException($"Unknown option '{args[index]}' for get-issues.");
            }
        }

        return await client.GetIssuesAsync(projectId, statusIds, assigneeIds, keyword, count, offset, order);
    }

    private static async Task<JsonNode?> ExecuteGetIssueAsync(BacklogClient client, IReadOnlyList<string> args)
    {
        if (args.Count != 1)
        {
            throw new CliException("get-issue requires ISSUE_KEY.");
        }

        return await client.GetIssueAsync(args[0]);
    }

    private static async Task<JsonNode?> ExecuteAddIssueAsync(BacklogClient client, IReadOnlyList<string> args)
    {
        int? projectId = null;
        string? summary = null;
        int? issueTypeId = null;
        int? priorityId = null;
        string? description = null;
        int? assigneeId = null;
        string? dueDate = null;

        for (var index = 0; index < args.Count;)
        {
            switch (args[index])
            {
                case "--project-id":
                    projectId = ReadIntOption(args, ref index, "--project-id");
                    break;
                case "--summary":
                    summary = ReadStringOption(args, ref index, "--summary");
                    break;
                case "--issue-type-id":
                    issueTypeId = ReadIntOption(args, ref index, "--issue-type-id");
                    break;
                case "--priority-id":
                    priorityId = ReadIntOption(args, ref index, "--priority-id");
                    break;
                case "--description":
                    description = ReadStringOption(args, ref index, "--description");
                    break;
                case "--assignee-id":
                    assigneeId = ReadIntOption(args, ref index, "--assignee-id");
                    break;
                case "--due-date":
                    dueDate = ReadStringOption(args, ref index, "--due-date");
                    break;
                default:
                    throw new CliException($"Unknown option '{args[index]}' for add-issue.");
            }
        }

        if (projectId is null || summary is null || issueTypeId is null || priorityId is null)
        {
            throw new CliException("add-issue requires --project-id, --summary, --issue-type-id, and --priority-id.");
        }

        return await client.AddIssueAsync(projectId.Value, summary, issueTypeId.Value, priorityId.Value, description, assigneeId, dueDate);
    }

    private static async Task<JsonNode?> ExecuteUpdateIssueAsync(BacklogClient client, IReadOnlyList<string> args)
    {
        if (args.Count == 0)
        {
            throw new CliException("update-issue requires ISSUE_KEY.");
        }

        var issueKey = args[0];
        string? summary = null;
        int? statusId = null;
        int? assigneeId = null;
        int? priorityId = null;
        string? comment = null;

        for (var index = 1; index < args.Count;)
        {
            switch (args[index])
            {
                case "--summary":
                    summary = ReadStringOption(args, ref index, "--summary");
                    break;
                case "--status-id":
                    statusId = ReadIntOption(args, ref index, "--status-id");
                    break;
                case "--assignee-id":
                    assigneeId = ReadIntOption(args, ref index, "--assignee-id");
                    break;
                case "--priority-id":
                    priorityId = ReadIntOption(args, ref index, "--priority-id");
                    break;
                case "--comment":
                    comment = ReadStringOption(args, ref index, "--comment");
                    break;
                default:
                    throw new CliException($"Unknown option '{args[index]}' for update-issue.");
            }
        }

        var fields = new List<KeyValuePair<string, string?>>
        {
            new("summary", summary),
            new("statusId", statusId?.ToString()),
            new("assigneeId", assigneeId?.ToString()),
            new("priorityId", priorityId?.ToString()),
            new("comment", comment)
        };

        if (fields.All(pair => pair.Value is null))
        {
            throw new CliException("update-issue requires at least one field to update.");
        }

        return await client.UpdateIssueAsync(issueKey, fields);
    }

    private static async Task<JsonNode?> ExecuteAddCommentAsync(BacklogClient client, IReadOnlyList<string> args)
    {
        if (args.Count == 0)
        {
            throw new CliException("add-comment requires ISSUE_KEY.");
        }

        var issueKey = args[0];
        string? content = null;

        for (var index = 1; index < args.Count;)
        {
            switch (args[index])
            {
                case "--content":
                    content = ReadStringOption(args, ref index, "--content");
                    break;
                default:
                    throw new CliException($"Unknown option '{args[index]}' for add-comment.");
            }
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new CliException("add-comment requires --content.");
        }

        return await client.AddCommentAsync(issueKey, content);
    }

    private static (GlobalOptions GlobalOptions, string Command, IReadOnlyList<string> CommandArgs) ParseArguments(IReadOnlyList<string> args)
    {
        string? space = null;
        string? apiKey = null;
        var tld = "com";

        var index = 0;
        while (index < args.Count && args[index].StartsWith("--", StringComparison.Ordinal))
        {
            switch (args[index])
            {
                case "--space":
                    space = ReadStringOption(args, ref index, "--space");
                    break;
                case "--api-key":
                    apiKey = ReadStringOption(args, ref index, "--api-key");
                    break;
                case "--tld":
                    tld = ReadStringOption(args, ref index, "--tld");
                    if (!string.Equals(tld, "com", StringComparison.Ordinal) &&
                        !string.Equals(tld, "jp", StringComparison.Ordinal))
                    {
                        throw new CliException("--tld must be 'com' or 'jp'.");
                    }

                    break;
                default:
                    throw new CliException($"Unknown global option '{args[index]}'.");
            }
        }

        if (space is null || apiKey is null)
        {
            throw new CliException("Both --space and --api-key are required.");
        }

        if (index >= args.Count)
        {
            throw new CliException("Command is required.");
        }

        var command = args[index];
        var commandArgs = args.Skip(index + 1).ToArray();
        return (new GlobalOptions(space, apiKey, tld), command, commandArgs);
    }

    private static string ReadStringOption(IReadOnlyList<string> args, ref int index, string optionName)
    {
        if (index + 1 >= args.Count || IsOptionToken(args[index + 1]))
        {
            throw new CliException($"{optionName} requires a value.");
        }

        var value = args[index + 1];
        index += 2;
        return value;
    }

    private static int ReadIntOption(IReadOnlyList<string> args, ref int index, string optionName)
    {
        var rawValue = ReadStringOption(args, ref index, optionName);
        if (!int.TryParse(rawValue, out var value))
        {
            throw new CliException($"{optionName} requires an integer value.");
        }

        return value;
    }

    private static List<int> ReadIntListOption(IReadOnlyList<string> args, ref int index, string optionName)
    {
        var values = new List<int>();
        index++;

        while (index < args.Count && !IsOptionToken(args[index]))
        {
            if (!int.TryParse(args[index], out var value))
            {
                throw new CliException($"{optionName} accepts integer values only.");
            }

            values.Add(value);
            index++;
        }

        if (values.Count == 0)
        {
            throw new CliException($"{optionName} requires one or more values.");
        }

        return values;
    }

    private static bool IsOptionToken(string token) => token.StartsWith("--", StringComparison.Ordinal);

    private static bool IsHelpToken(string token) =>
        string.Equals(token, "--help", StringComparison.Ordinal) ||
        string.Equals(token, "-h", StringComparison.Ordinal);

    private static void PrintUsage()
    {
        Console.Error.WriteLine("Backlog API v2 CLI");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Usage:");
        Console.Error.WriteLine("  dotnet run --file scripts\\backlog_api.cs -- --space SPACE_ID --api-key API_KEY [--tld com|jp] <command> [options]");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Commands:");
        Console.Error.WriteLine("  get-space               スペース情報を取得");
        Console.Error.WriteLine("  get-projects            プロジェクト一覧を取得");
        Console.Error.WriteLine("  get-issues              課題一覧を取得");
        Console.Error.WriteLine("  get-issue ISSUE_KEY     課題を取得");
        Console.Error.WriteLine("  add-issue               課題を登録");
        Console.Error.WriteLine("  update-issue ISSUE_KEY  課題を更新");
        Console.Error.WriteLine("  add-comment ISSUE_KEY   コメントを追加");
        Console.Error.WriteLine("  get-users               ユーザー一覧を取得");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Examples:");
        Console.Error.WriteLine("  dotnet run --file scripts\\backlog_api.cs -- --space myspace --api-key YOUR_KEY get-space");
        Console.Error.WriteLine("  dotnet run --file scripts\\backlog_api.cs -- --space myspace --api-key YOUR_KEY get-issues --project-id 12345");
        Console.Error.WriteLine("  dotnet run --file scripts\\backlog_api.cs -- --space myspace --api-key YOUR_KEY get-issue PROJECT-123");
        Console.Error.WriteLine("  dotnet run --file scripts\\backlog_api.cs -- --space myspace --api-key YOUR_KEY add-issue --project-id 12345 --summary \"バグ修正\" --issue-type-id 1 --priority-id 2");
        Console.Error.WriteLine("  dotnet run --file scripts\\backlog_api.cs -- --space myspace --api-key YOUR_KEY update-issue PROJECT-123 --status-id 2 --comment \"対応中\"");
    }

    private sealed record GlobalOptions(string Space, string ApiKey, string Tld);
}

sealed class BacklogClient : IDisposable
{
    private readonly string _baseUrl;
    private readonly string _apiKey;
    private readonly HttpClient _httpClient;

    public BacklogClient(string space, string apiKey, string tld = "com")
    {
        _baseUrl = $"https://{space}.backlog.{tld}/api/v2";
        _apiKey = apiKey;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public Task<JsonNode?> GetSpaceAsync() =>
        SendAsync(HttpMethod.Get, "/space");

    public Task<JsonNode?> GetUsersAsync() =>
        SendAsync(HttpMethod.Get, "/users");

    public Task<JsonNode?> GetProjectsAsync() =>
        SendAsync(HttpMethod.Get, "/projects");

    public Task<JsonNode?> GetIssuesAsync(
        int? projectId = null,
        IEnumerable<int>? statusIds = null,
        IEnumerable<int>? assigneeIds = null,
        string? keyword = null,
        int count = 20,
        int offset = 0,
        string order = "desc")
    {
        var query = new List<KeyValuePair<string, string?>>
        {
            new("count", count.ToString()),
            new("offset", offset.ToString()),
            new("order", order)
        };

        if (projectId is not null)
        {
            query.Add(new("projectId[]", projectId.Value.ToString()));
        }

        if (statusIds is not null)
        {
            query.AddRange(statusIds.Select(statusId => new KeyValuePair<string, string?>("statusId[]", statusId.ToString())));
        }

        if (assigneeIds is not null)
        {
            query.AddRange(assigneeIds.Select(assigneeId => new KeyValuePair<string, string?>("assigneeId[]", assigneeId.ToString())));
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query.Add(new("keyword", keyword));
        }

        return SendAsync(HttpMethod.Get, "/issues", query);
    }

    public Task<JsonNode?> GetIssueAsync(string issueIdOrKey) =>
        SendAsync(HttpMethod.Get, $"/issues/{issueIdOrKey}");

    public Task<JsonNode?> AddIssueAsync(
        int projectId,
        string summary,
        int issueTypeId,
        int priorityId,
        string? description = null,
        int? assigneeId = null,
        string? dueDate = null)
    {
        var form = new List<KeyValuePair<string, string?>>
        {
            new("projectId", projectId.ToString()),
            new("summary", summary),
            new("issueTypeId", issueTypeId.ToString()),
            new("priorityId", priorityId.ToString()),
            new("description", description),
            new("assigneeId", assigneeId?.ToString()),
            new("dueDate", dueDate)
        };

        return SendAsync(HttpMethod.Post, "/issues", form: form);
    }

    public Task<JsonNode?> UpdateIssueAsync(string issueIdOrKey, IEnumerable<KeyValuePair<string, string?>> fields) =>
        SendAsync(HttpMethod.Patch, $"/issues/{issueIdOrKey}", form: fields);

    public Task<JsonNode?> AddCommentAsync(string issueIdOrKey, string content) =>
        SendAsync(HttpMethod.Post, $"/issues/{issueIdOrKey}/comments", form: new[]
        {
            new KeyValuePair<string, string?>("content", content)
        });

    private async Task<JsonNode?> SendAsync(
        HttpMethod method,
        string path,
        IEnumerable<KeyValuePair<string, string?>>? query = null,
        IEnumerable<KeyValuePair<string, string?>>? form = null)
    {
        using var request = new HttpRequestMessage(method, BuildUri(path, query));

        if (form is not null)
        {
            request.Content = new FormUrlEncodedContent(
                form
                    .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
                    .Select(pair => new KeyValuePair<string, string>(pair.Key, pair.Value!)));
        }

        using var response = await _httpClient.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new BacklogApiException(response.StatusCode, responseBody);
        }

        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return null;
        }

        return JsonNode.Parse(responseBody);
    }

    private Uri BuildUri(string path, IEnumerable<KeyValuePair<string, string?>>? query)
    {
        var parameters = new List<KeyValuePair<string, string?>>
        {
            new("apiKey", _apiKey)
        };

        if (query is not null)
        {
            parameters.AddRange(query.Where(pair => !string.IsNullOrWhiteSpace(pair.Value)));
        }

        var queryString = string.Join(
            "&",
            parameters.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value!)}"));

        return new Uri($"{_baseUrl}{path}?{queryString}");
    }

    public void Dispose() => _httpClient.Dispose();
}

sealed class BacklogApiException(HttpStatusCode statusCode, string responseBody) : Exception
{
    public HttpStatusCode StatusCode { get; } = statusCode;

    public string ResponseBody { get; } = responseBody;
}

sealed class CliException(string message) : Exception(message);

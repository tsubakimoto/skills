# My agent skills

## Installation

### Plugins

1. Launch Copilot CLI or Claude Code
2. Add the marketplace:
   ```
   /plugin marketplace add tsubakimoto/skills
   ```
3. Install a plugin:
   ```
   /plugin install <plugin>@tsubakimoto-skills
   ```
4. Restart to load the new plugins
5. View available skills:
   ```
   /skills
   ```
6. Update plugin (on demand):
   ```
   /plugin update <plugin>@tsubakimoto-skills
   ```

### GitHub CLI

1. Install GitHub CLI from [GitHub CLI](https://cli.github.com/)
2. Install a skill:
   ```
   gh skill install tsubakimoto/skills
   ```

## Skill list

| Name | Discription |
| --- | --- |
| [azure-resource-abbreviations](./azure-resource-abbreviations/) | [Abbreviation recommendations for Azure resources](https://learn.microsoft.com/en-us/azure/cloud-adoption-framework/ready/azure-best-practices/resource-abbreviations). |
| [backlog-api](./backlog-api/) | Backlog API v2 based on [sugimomoto/backlogPostmanCollection](https://github.com/sugimomoto/backlogPostmanCollection) |
| [csharp-file-based-apps](./csharp-file-based-apps/) | C# file-based apps guidance based on [.NET file-based apps](https://learn.microsoft.com/en-us/dotnet/core/sdk/file-based-apps). |
| [devblog-updates](./devblog-updates/) | Summarize the [Microsoft Developer Blogs](https://devblogs.microsoft.com/landing). |
| [github-changelog](./github-changelog/) | Summarize the [GitHub Changelog](https://github.blog/changelog/). |
| [github-entra-federated-credentials](./github-entra-federated-credentials/) | Sets up Entra ID federated credentials so GitHub Actions can authenticate to Azure using OIDC (workload identity federation) — no client secrets required. |
| [marp-css](./marp-css/) | Marp design. |
| [marp-deck](./marp-deck/) | Marp deck. |

## Favorite agent skills

| Name | Repository |
| --- | --- |
| .NET | https://github.com/dotnet/skills |
| Anthropics | https://github.com/anthropics/skills |
| Awesome GitHub Copilot | https://github.com/github/awesome-copilot |
| Azure | https://github.com/microsoft/azure-skills |
| freee | https://github.com/freee/freee-mcp/tree/main/skills%2Ffreee-api-skill |
| Microsoft | https://github.com/microsoft/skills |
| Microsoft Fabric | https://github.com/microsoft/skills-for-fabric |
| Microsoft Work IQ | https://github.com/microsoft/work-iq |
| WinUI | https://github.com/microsoft/win-dev-skills |

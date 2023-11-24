# GitRunner

[![MIT licensed](https://img.shields.io/badge/license-MIT-blue.svg)](https://gitlab.aiursoft.cn/aiursoft/gitrunner/-/blob/master/LICENSE)
[![Pipeline stat](https://gitlab.aiursoft.cn/aiursoft/gitrunner/badges/master/pipeline.svg)](https://gitlab.aiursoft.cn/aiursoft/CSTools/-/pipelines)
[![Test Coverage](https://gitlab.aiursoft.cn/aiursoft/gitrunner/badges/master/coverage.svg)](https://gitlab.aiursoft.cn/aiursoft/gitrunner/-/pipelines)
[![NuGet version (Aiursoft.GitRunner)](https://img.shields.io/nuget/v/Aiursoft.gitrunner.svg)](https://www.nuget.org/packages/Aiursoft.gitrunner/)
[![ManHours](https://manhours.aiursoft.cn/gitlab/gitlab.aiursoft.cn/aiursoft/gitrunner.svg)](https://gitlab.aiursoft.cn/aiursoft/gitrunner/-/commits/master?ref_type=heads)

GitRunner is a tool to help you run git commands in C#.

## Installation

To install `Aiursoft.GitRunner` to your project from [nuget.org](https://www.nuget.org/packages/Aiursoft.GitRunner/):

```bash
dotnet add package Aiursoft.GitRunner
```

## How to use

You can use this tool to Clone\Push\Commit git repositories.

```csharp
var serviceProvider = new ServiceCollection()
    .AddLogging()
    .AddGitRunner()
    .BuildServiceProvider();
var repoPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
Directory.CreateDirectory(repoPath);

var workspaceManager = serviceProvider.GetRequiredService<WorkspaceManager>();
await workspaceManager.Clone(
    repoPath, 
    "master", 
    "https://gitlab.aiursoft.cn/aiursoft/gitrunner.git",
    mode);

var branch = await workspaceManager.GetBranch(repoPath); // master
var remote = await workspaceManager.GetRemoteUrl(repoPath); // https://gitlab.aiursoft.cn/aiursoft/gitrunner.git
```

## How to contribute

There are many ways to contribute to the project: logging bugs, submitting pull requests, reporting issues, and creating suggestions.

Even if you with push rights on the repository, you should create a personal fork and create feature branches there when you need them. This keeps the main repository clean and your workflow cruft out of sight.

We're also interested in your feedback on the future of this project. You can submit a suggestion or feature request through the issue tracker. To make this process more effective, we're asking that these include more information to help define them more clearly.

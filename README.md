# Aiursoft GitRunner

<a name="gitrunner"></a>

[![MIT licensed](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/AiursoftWeb/gitrunner/blob/master/LICENSE)
[![Pipeline stat](https://badge-proxy.aiursoft.com/aiursoft/gitrunner/badges/master/pipeline.svg)](https://gitlab.aiursoft.com/aiursoft/gitrunner/-/pipelines)
[![Test Coverage](https://badge-proxy.aiursoft.com/aiursoft/gitrunner/badges/master/coverage.svg)](https://gitlab.aiursoft.com/aiursoft/gitrunner/-/pipelines)
[![S360 compliance](https://s360.aiursoft.com/badge.svg?project=aiursoft%2Fgitrunner)](https://s360.aiursoft.com/SupplyChain?tab=projects&project=aiursoft%2Fgitrunner&scope=baseline&ecosystem=all)
[![NuGet version (Aiursoft.GitRunner)](https://img.shields.io/nuget/v/Aiursoft.gitrunner.svg)](https://www.nuget.org/packages/Aiursoft.gitrunner/)
[![Man hours](https://manhours.aiursoft.com/r/github.com/aiursoftweb/gitrunner.svg)](https://manhours.aiursoft.com/r/github.com/aiursoftweb/gitrunner.html)

GitRunner is a tool to help you run git commands in C#.

## Overview

### What is GitRunner?

Run Git repository operations from typed .NET services, including clone, commits, branches and remote operations.

## Installation

Add the package to your .NET project:

<a name="how-to-install"></a>

To install `Aiursoft.GitRunner` to your project from [nuget.org](https://www.nuget.org/packages/Aiursoft.GitRunner/):

```bash
dotnet add package Aiursoft.GitRunner
```

**Standalone console example**

The console example also uses the standard Microsoft service registrations:

```bash
dotnet add package Microsoft.Extensions.Logging
```

Requires .NET 10 or later.

## Usage

<a name="how-to-use"></a>

You can use this tool to Clone\Push\Commit git repositories.

```csharp
using Aiursoft.GitRunner;
using Aiursoft.GitRunner.Models;
using Microsoft.Extensions.DependencyInjection;

using var serviceProvider = new ServiceCollection()
    .AddLogging()
    .AddGitRunner()
    .BuildServiceProvider();
var repoPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
Directory.CreateDirectory(repoPath);

var workspaceManager = serviceProvider.GetRequiredService<WorkspaceManager>();
await workspaceManager.Clone(
    repoPath,
    "master",
    "https://github.com/AiursoftWeb/gitrunner.git",
    CloneMode.Full);

var branch = await workspaceManager.GetBranch(repoPath); // master
var remote = await workspaceManager.GetRemoteUrl(repoPath, "origin"); // https://github.com/AiursoftWeb/gitrunner.git
```

### API Overview

<a name="api-overview"></a>

Below is a brief summary of all the methods available in **WorkspaceManager**:

- **GetBranch(string path)**
    - Retrieves the current branch of the repository.
    - *Usage:* `var branch = await workspaceManager.GetBranch(path);`

- **GetCommitTimes(string path)**
    - Returns an array of commit times (as `DateTime[]`), in descending order.
    - *Usage:* `var times = await workspaceManager.GetCommitTimes(path);`

- **GetCommits(string path)**
    - Retrieves commit details (author, email, message, time, hash) as an array of commit objects.
    - *Usage:* `var commits = await workspaceManager.GetCommits(path);`

- **SwitchToBranch(string sourcePath, string targetBranch, bool enforceCurrentContent)**
    - Switches the repository to the specified branch. If needed, it can enforce a fresh branch creation.
    - *Usage:* `await workspaceManager.SwitchToBranch(sourcePath, "develop", true);`

- **GetRemoteUrl(string path, string remoteName)**
    - Retrieves the remote origin URL from the Git configuration.
    - *Usage:* `var remoteUrl = await workspaceManager.GetRemoteUrl(path, "origin");`

- **Init(string path)**
    - Initializes a new Git repository at the given path (`git init`).
    - *Usage:* `await workspaceManager.Init(path);`

- **AddAndCommit(string path, string message)**
    - Stages all changes and commits them. Auto-configures user info if not set.
    - *Usage:* `await workspaceManager.AddAndCommit(path, "Initial commit");`

- **GetCurrentUserEmail(string path)**
    - Returns the current Git user email from configuration.
    - *Usage:* `var email = await workspaceManager.GetCurrentUserEmail(path);`

- **Clone(string path, string? branch, string endPoint, CloneMode cloneMode)**
    - Clones a repository using different modes:
        - **Full:** Standard clone.
        - **OnlyCommits:** Downloads commits only.
        - **CommitsAndTrees:** Clones commits and tree info.
        - **Depth1:** Shallow clone.
        - **Bare:** Bare repository.
        - **BareWithOnlyCommits:** Bare clone with commit filter.
    - *Usage:* `await workspaceManager.Clone(path, "master", endpoint, CloneMode.Full);`

- **IsBareRepo(string path)**
    - Determines if the repository is a bare repository.
    - *Usage:* `bool isBare = await workspaceManager.IsBareRepo(path);`

- **ResetRepo(string path, string? branch, string endPoint, CloneMode cloneMode)**
    - Resets a repository to match the remote state. If the repo isn’t valid, it deletes and re-clones.
    - *Usage:* `await workspaceManager.ResetRepo(path, "master", endpoint, CloneMode.Full);`

- **CommitToBranch(string sourcePath, string message, string branch)**
    - Stages changes, switches to the specified branch, and commits with the provided message.
    - *Usage:* `bool committed = await workspaceManager.CommitToBranch(sourcePath, "Update", "feature");`

- **SetUserConfig(string sourcePath, string username, string email)**
    - Sets the Git user name and email configuration for commits.
    - *Usage:* `await workspaceManager.SetUserConfig(sourcePath, "Your Name", "you@example.com");`

- **Push(string sourcePath, string branch, string endpoint, bool force = false)**
    - Pushes the local branch to the remote repository. Can force push if required.
    - *Usage:* `await workspaceManager.Push(sourcePath, "master", endpoint, true);`

- **PendingCommit(string sourcePath)**
    - Checks if there are uncommitted changes in the repository.
    - *Usage:* `bool hasPending = await workspaceManager.PendingCommit(sourcePath);`

- **Fetch(string path)**
    - Fetches updates from the remote with a built-in retry mechanism.
    - *Usage:* `await workspaceManager.Fetch(path);`

### Error Handling & Retry

<a name="error-handling--retry"></a>

- **GitCommandException**: Methods throw this exception on Git command errors.
- **RetryEngine**: Used (e.g., in the `Fetch` method) to automatically retry operations on failure.

## Configuration

**Runtime and registration**

Install Git and make the `git` executable available on the process PATH. `AddGitRunner()` registers the workspace manager and command/retry services; add logging before resolving them. Supply the repository path, branch, endpoint and `CloneMode` to operations. Remote access uses the environment's Git authentication configuration; this package does not require an application-specific configuration file. Error behavior and retry semantics are retained in Usage.

## Development

Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

From the repository root, restore, build and test:

```bash
dotnet restore Aiursoft.GitRunner.sln
dotnet build Aiursoft.GitRunner.sln --no-restore
dotnet test Aiursoft.GitRunner.sln --no-restore
```

Run `./lint.sh` for the existing static checks.

## How to contribute

<a name="how-to-contribute"></a>

There are many ways to contribute to the project: logging bugs, submitting pull requests, reporting issues, and creating suggestions.

Even if you have push rights on the repository, you should create a personal fork and create feature branches there when you need them. This keeps the main repository clean and your workflow cruft out of sight.

We're also interested in your feedback on the future of this project. You can submit a suggestion or feature request through the issue tracker. To make this process more effective, we're asking that these include more information to help define them more clearly.

## License

Licensed under the [MIT License](https://github.com/AiursoftWeb/gitrunner/blob/master/LICENSE).

# GitRunner

[![MIT licensed](https://img.shields.io/badge/license-MIT-blue.svg)](https://gitlab.aiursoft.cn/aiursoft/gitrunner/-/blob/master/LICENSE)
[![Pipeline stat](https://gitlab.aiursoft.cn/aiursoft/gitrunner/badges/master/pipeline.svg)](https://gitlab.aiursoft.cn/aiursoft/gitrunner/-/pipelines)
[![Test Coverage](https://gitlab.aiursoft.cn/aiursoft/gitrunner/badges/master/coverage.svg)](https://gitlab.aiursoft.cn/aiursoft/gitrunner/-/pipelines)
[![NuGet version (Aiursoft.GitRunner)](https://img.shields.io/nuget/v/Aiursoft.gitrunner.svg)](https://www.nuget.org/packages/Aiursoft.gitrunner/)
[![ManHours](https://manhours.aiursoft.cn/r/gitlab.aiursoft.cn/aiursoft/gitrunner.svg)](https://gitlab.aiursoft.cn/aiursoft/gitrunner/-/commits/master?ref_type=heads)

GitRunner is a tool to help you run git commands in C#.

## How to install

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

## API Overview

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

- **GetRemoteUrl(string path)**
    - Retrieves the remote origin URL from the Git configuration.
    - *Usage:* `var remoteUrl = await workspaceManager.GetRemoteUrl(path);`

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

## Error Handling & Retry

- **GitCommandException**: Methods throw this exception on Git command errors.
- **RetryEngine**: Used (e.g., in the `Fetch` method) to automatically retry operations on failure.

## How to contribute

There are many ways to contribute to the project: logging bugs, submitting pull requests, reporting issues, and creating suggestions.

Even if you with push rights on the repository, you should create a personal fork and create feature branches there when you need them. This keeps the main repository clean and your workflow cruft out of sight.

We're also interested in your feedback on the future of this project. You can submit a suggestion or feature request through the issue tracker. To make this process more effective, we're asking that these include more information to help define them more clearly.

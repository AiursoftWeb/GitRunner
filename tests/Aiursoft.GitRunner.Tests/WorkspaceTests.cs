using System.Text;
using System.ComponentModel;
using Aiursoft.CSTools.Services;
using Aiursoft.CSTools.Tools;
using Aiursoft.GitRunner;
using Aiursoft.GitRunner.Exceptions;
using Aiursoft.GitRunner.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[assembly: DoNotParallelize]

namespace Aiursoft.Canon.Tests;

[TestClass]
public class WorkspaceTests
{
    private IServiceProvider? _serviceProvider;
    private string? _tempPath;

    [TestInitialize]
    public void Init()
    {
        _serviceProvider = new ServiceCollection()
            .AddLogging(l => l.AddConsole())
            .AddGitRunner()
            .BuildServiceProvider();
        _tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempPath);
    }

    [TestCleanup]
    public void Clean()
    {
        // Delete the tempPath
        if (Directory.Exists(_tempPath))
        {
            FolderDeleter.DeleteByForce(_tempPath);
        }
    }

    [TestMethod]
    [DataRow(CloneMode.Depth1)]
    [DataRow(CloneMode.OnlyCommits)]
    [DataRow(CloneMode.CommitsAndTrees)]
    [DataRow(CloneMode.Full)]
    public async Task TestClone(CloneMode mode)
    {
        var workspaceManager = _serviceProvider!.GetRequiredService<WorkspaceManager>();
        await workspaceManager.Clone(
            _tempPath!,
            "master",
            "https://github.com/AiursoftWeb/gitrunner.git",
            mode);
        Assert.IsTrue(Directory.Exists(_tempPath));
    }

    [TestMethod]
    public async Task TestCloneDefaultBranch()
    {
        var workspaceManager = _serviceProvider!.GetRequiredService<WorkspaceManager>();
        await workspaceManager.Clone(
            _tempPath!,
            null,
            "https://github.com/AiursoftWeb/gitrunner.git",
            CloneMode.Depth1);
        Assert.IsTrue(Directory.Exists(_tempPath));
        var branch = await workspaceManager.GetBranch(_tempPath!);
        Assert.AreEqual("master", branch);
    }

    [TestMethod]
    public async Task TestReset()
    {
        var workspaceManager = _serviceProvider!.GetRequiredService<WorkspaceManager>();
        await workspaceManager.ResetRepo(_tempPath!, "master", "https://github.com/AiursoftWeb/gitrunner.git",
            CloneMode.Depth1);
        Assert.IsTrue(Directory.Exists(_tempPath));
    }

    [TestMethod]
    public async Task TestResetTwice()
    {
        var workspaceManager = _serviceProvider!.GetRequiredService<WorkspaceManager>();
        await workspaceManager.ResetRepo(_tempPath!, "master", "https://github.com/AiursoftWeb/gitrunner.git",
            CloneMode.Depth1);
        await workspaceManager.ResetRepo(_tempPath!, "master", "https://github.com/AiursoftWeb/manhours.git",
            CloneMode.Depth1);
        var actualRemote = await workspaceManager.GetRemoteUrl(_tempPath!, "origin");
        Assert.AreEqual("https://github.com/AiursoftWeb/manhours.git", actualRemote);
        Assert.IsTrue(Directory.Exists(_tempPath));
    }

    [TestMethod]
    public async Task TestResetDefaultBranch()
    {
        var workspaceManager = _serviceProvider!.GetRequiredService<WorkspaceManager>();
        await workspaceManager.ResetRepo(_tempPath!, null, "https://github.com/AiursoftWeb/gitrunner.git",
            CloneMode.Depth1);
        Assert.IsTrue(Directory.Exists(_tempPath));
    }

    [TestMethod]
    public async Task TestResetBareDefaultBranch()
    {
        var workspaceManager = _serviceProvider!.GetRequiredService<WorkspaceManager>();
        await workspaceManager.ResetRepo(_tempPath!, null, "https://github.com/AiursoftWeb/gitrunner.git",
            CloneMode.BareWithOnlyCommits);
        await workspaceManager.ResetRepo(_tempPath!, null, "https://github.com/AiursoftWeb/gitrunner.git",
            CloneMode.BareWithOnlyCommits);
        Assert.IsTrue(Directory.Exists(_tempPath));
    }

    [TestMethod]
    public async Task TestGetBranch()
    {
        var workspaceManager = _serviceProvider!.GetRequiredService<WorkspaceManager>();
        await workspaceManager.ResetRepo(_tempPath!, "master", "https://github.com/AiursoftWeb/gitrunner.git",
            CloneMode.Depth1);
        var branch = await workspaceManager.GetBranch(_tempPath!);
        Assert.AreEqual("master", branch);
    }

    [TestMethod]
    public async Task TestGetRemoteUrl()
    {
        var workspaceManager = _serviceProvider!.GetRequiredService<WorkspaceManager>();
        await workspaceManager.ResetRepo(_tempPath!, "master", "https://github.com/AiursoftWeb/gitrunner.git",
            CloneMode.Depth1);
        var remote = await workspaceManager.GetRemoteUrl(_tempPath!, "origin");
        Assert.AreEqual("https://github.com/AiursoftWeb/gitrunner.git", remote);
    }

    [TestMethod]
    public async Task TestGetCommitTimes()
    {
        var workspaceManager = _serviceProvider!.GetRequiredService<WorkspaceManager>();
        await workspaceManager.ResetRepo(_tempPath!, "master", "https://github.com/AiursoftWeb/gitrunner.git",
            CloneMode.OnlyCommits);
        var commits = await workspaceManager.GetCommitTimes(_tempPath!);
        Assert.IsGreaterThan(8, commits.Length);
        Assert.IsTrue(commits[0] > commits[1]);
    }

    [TestMethod]
    public async Task TestGetCommitTimesFromEdi()
    {
        var workspaceManager = _serviceProvider!.GetRequiredService<WorkspaceManager>();
        await workspaceManager.ResetRepo(_tempPath!, "master", "https://github.com/ediwang/elf.git",
            CloneMode.Full);
        var commits = await workspaceManager.GetCommitTimes(_tempPath!);
        Assert.IsGreaterThan(8, commits.Length);
        Assert.IsTrue(commits[0] > commits[1]);
    }

    [TestMethod]
    public async Task TestGetCommitsFromEdi()
    {
        var workspaceManager = _serviceProvider!.GetRequiredService<WorkspaceManager>();
        await workspaceManager.ResetRepo(_tempPath!, "master", "https://github.com/ediwang/elf.git",
            CloneMode.Full);
        var commits = await workspaceManager.GetCommits(_tempPath!);
        Assert.IsGreaterThan(8, commits.Length);
        Assert.AreEqual("Initial commit", commits.Last().Message);
    }

    [TestMethod]
    public async Task TestGetRemoteUrlWithOnlyCommits()
    {
        var workspaceManager = _serviceProvider!.GetRequiredService<WorkspaceManager>();
        await workspaceManager.ResetRepo(_tempPath!, "master", "https://github.com/AiursoftWeb/gitrunner.git",
            CloneMode.OnlyCommits);
        var remote = await workspaceManager.GetRemoteUrl(_tempPath!, "origin");
        Assert.AreEqual("https://github.com/AiursoftWeb/gitrunner.git", remote);
    }

    [TestMethod]
    public async Task TestResetRepoTwoTimes()
    {
        var workspaceManager = _serviceProvider!.GetRequiredService<WorkspaceManager>();
        var commandService = _serviceProvider!.GetRequiredService<GitRunner.Services.GitCommandRunner>();
        await workspaceManager.ResetRepo(_tempPath!, null, "https://github.com/AiursoftWeb/gitrunner.git",
            CloneMode.Depth1);
        Assert.IsTrue(Directory.Exists(_tempPath));

        try
        {
            await commandService.RunGit(_tempPath!, "remote remove origin");
            await workspaceManager.ResetRepo(_tempPath!, null, "https://github.com/AiursoftWeb/gitrunner.git",
                CloneMode.Depth1);
            Assert.Fail("After removing the remote, we should not be able to reset the repo.");
        }
        catch (GitCommandException e)
        {
            Console.WriteLine(e);
        }
    }

    [TestMethod]
    public async Task TestFetchTimeoutKillsGitProcessTreeBeforeRetry()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("This process-tree regression test uses a POSIX shell script.");
            return;
        }

        var workspaceManager = _serviceProvider!.GetRequiredService<WorkspaceManager>();
        var commandRunner = _serviceProvider!.GetRequiredService<GitRunner.Services.GitCommandRunner>();
        var remotePath = Path.Combine(Path.GetTempPath(), "GitRunnerRemote_" + Guid.NewGuid());
        var uploadPackScript = Path.Combine(_tempPath!, "slow-upload-pack.sh");
        var childMarkerPath = Path.Combine(_tempPath!, "child-survived");

        Directory.CreateDirectory(remotePath);
        try
        {
            await commandRunner.RunGit(remotePath, "init --bare");
            await workspaceManager.Init(_tempPath!);
            await commandRunner.RunGit(_tempPath!, $"remote add origin {remotePath}");

            await File.WriteAllTextAsync(
                uploadPackScript,
                $"#!/bin/sh\n(sleep 1; touch \"{childMarkerPath}\") &\nwait\n");
            File.SetUnixFileMode(
                uploadPackScript,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            await commandRunner.RunGit(_tempPath!, $"config remote.origin.uploadpack {uploadPackScript}");

            await Assert.ThrowsExactlyAsync<TimeoutException>(async () =>
            {
                await workspaceManager.Fetch(_tempPath!, TimeSpan.FromMilliseconds(100));
            });

            await Task.Delay(TimeSpan.FromSeconds(2));
            Assert.IsFalse(
                File.Exists(childMarkerPath),
                "A git upload-pack child survived a timed-out fetch attempt.");
        }
        finally
        {
            FolderDeleter.DeleteByForce(remotePath);
        }
    }

    [TestMethod]
    public async Task TestAuthenticatedBranchSynchronizationAndPush()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("This authenticated transport regression uses a POSIX upload-pack script.");
            return;
        }

        var workspace = _serviceProvider!.GetRequiredService<WorkspaceManager>();
        var runner = _serviceProvider!.GetRequiredService<GitRunner.Services.GitCommandRunner>();
        var source = Path.Combine(_tempPath!, "source");
        var cache = Path.Combine(_tempPath!, "cache");
        var target = Path.Combine(_tempPath!, "target.git");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(cache);
        Directory.CreateDirectory(target);
        await runner.RunGit(source, "init --initial-branch=master");
        await workspace.SetUserConfig(source, "Tester", "tester@example.test");
        await File.WriteAllTextAsync(Path.Combine(source, "README.md"), "Initial");
        await runner.RunGit(source, "add .");
        await runner.RunGit(source, "-c commit.gpgsign=false commit -m Initial");
        await workspace.Clone(cache, null, source, CloneMode.Full);

        await runner.RunGit(source, "checkout -b feature");
        await File.WriteAllTextAsync(Path.Combine(source, "README.md"), "Feature");
        await runner.RunGit(source, "add .");
        await runner.RunGit(source, "-c commit.gpgsign=false commit -m Feature");
        await runner.RunGit(source, "-c tag.gpgsign=false tag v1");

        var uploadPack = Path.Combine(_tempPath!, "authenticated-upload-pack.sh");
        await File.WriteAllTextAsync(uploadPack,
            "#!/bin/sh\n[ \"$GITRUNNER_TEST_CREDENTIAL\" = test-source-token ] || exit 1\nexec git-upload-pack \"$@\"\n");
        File.SetUnixFileMode(uploadPack, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        await runner.RunGit(cache, $"config remote.origin.uploadpack \"{uploadPack}\"");
        await Assert.ThrowsExactlyAsync<GitCommandException>(() => workspace.Fetch(cache, TimeSpan.FromSeconds(3)));

        var environment = new Dictionary<string, string?>
        {
            ["GITRUNNER_TEST_CREDENTIAL"] = "test-source-token",
            ["GIT_CONFIG_COUNT"] = "1",
            ["GIT_CONFIG_KEY_0"] = "credential.helper",
            ["GIT_CONFIG_VALUE_0"] = string.Empty
        };
        await workspace.EnsureAllLocalBranchesUpToDateWithRemote(cache, "origin", environment);

        await runner.RunGit(target, "init --bare");
        var receiveHook = Path.Combine(target, "hooks", "pre-receive");
        await File.WriteAllTextAsync(receiveHook,
            "#!/bin/sh\n[ -z \"$GITRUNNER_TEST_CREDENTIAL\" ] || exit 1\n");
        File.SetUnixFileMode(receiveHook, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        await workspace.AddOrSetRemoteUrl(cache, "target", target);
        await workspace.PushAllBranchesAndTags(cache, "target", force: true);

        var expected = await runner.RunGit(source, "show-ref --heads --tags");
        var actual = await runner.RunGit(target, "show-ref --heads --tags");
        Assert.AreEqual(expected, actual);
        Assert.Contains("refs/heads/feature", actual);
        Assert.Contains("refs/tags/v1", actual);
        Assert.IsFalse(environment.ContainsKey("AIURSOFT_GITRUNNER_SAFE_DIRECTORY"));
        Assert.IsNull(Environment.GetEnvironmentVariable("GITRUNNER_TEST_CREDENTIAL"));
        var config = await File.ReadAllTextAsync(Path.Combine(cache, ".git", "config"));
        Assert.DoesNotContain("test-source-token", config);
    }

    [TestMethod]
    [DataRow(CloneMode.Full)]
    [DataRow(CloneMode.Bare)]
    public async Task ResetRepoUsesCurrentCredentialsAfterRotation(CloneMode mode)
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("This authenticated transport regression uses a POSIX Git wrapper.");
            return;
        }

        const string endpoint = "https://rotation.example.test/repository.git";
        const string oldToken = "expired-token-a";
        var previousGlobalConfig = Environment.GetEnvironmentVariable("GIT_CONFIG_GLOBAL");
        var previousAuthentication = Environment.GetEnvironmentVariable("GIT_CONFIG_VALUE_3");
        var globalConfig = Path.Combine(_tempPath!, "isolated.gitconfig");
        var source = Path.Combine(_tempPath!, "source");
        var cache = Path.Combine(_tempPath!, "cache");
        var fresh = Path.Combine(_tempPath!, "fresh");
        var expectedHeader = Path.Combine(_tempPath!, "expected-header");
        var toolsPath = Path.Combine(_tempPath!, "tools");
        var gitWrapper = Path.Combine(toolsPath, "git");
        var previousPath = Environment.GetEnvironmentVariable("PATH");
        var realGit = previousPath!.Split(Path.PathSeparator).Select(folder => Path.Combine(folder, "git")).First(File.Exists);
        var accepted = Path.Combine(_tempPath!, "accepted-requests");
        var logs = new CredentialLogCapture();
        using var services = new ServiceCollection()
            .AddLogging(builder => builder.SetMinimumLevel(LogLevel.Trace).AddProvider(logs))
            .AddGitRunner()
            .BuildServiceProvider();
        var workspace = services.GetRequiredService<WorkspaceManager>();
        var runner = services.GetRequiredService<GitRunner.Services.GitCommandRunner>();
        try
        {
            await File.WriteAllTextAsync(globalConfig, string.Empty);
            Environment.SetEnvironmentVariable("GIT_CONFIG_GLOBAL", globalConfig);
            Directory.CreateDirectory(source);
            Directory.CreateDirectory(cache);
            Directory.CreateDirectory(fresh);
            await runner.RunGit(source, "init --initial-branch=master");
            await workspace.SetUserConfig(source, "Tester", "tester@example.test");
            await File.WriteAllTextAsync(Path.Combine(source, "README.md"), "Initial");
            await runner.RunGit(source, "add .");
            await runner.RunGit(source, "-c commit.gpgsign=false commit -m Initial");
            await workspace.Clone(cache, "master", source, mode);
            await runner.RunGit(cache, $"remote set-url origin https://bot:{oldToken}@rotation.example.test/repository.git");

            var sourceUrl = new Uri(source + Path.DirectorySeparatorChar).AbsoluteUri.TrimEnd('/');
            await runner.RunGit(source, $"config --file \"{globalConfig}\" url.\"{sourceUrl}\".insteadOf {endpoint}");
            // Authenticate clone/fetch at the client boundary, then execute the real Git transfer.
            Directory.CreateDirectory(toolsPath);
            await File.WriteAllTextAsync(gitWrapper,
                "#!/bin/sh\ncase \" $* \" in\n*' fetch '*|*' clone '*)\n" +
                $"actual=$('{realGit}' config --get-urlmatch http.extraHeader '{endpoint}')\n" +
                $"[ \"$actual\" = \"$(cat '{expectedHeader}')\" ] || exit 1\n" +
                $"echo accepted >> '{accepted}'\n;;\nesac\nexec '{realGit}' \"$@\"\n");
            File.SetUnixFileMode(gitWrapper, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            Environment.SetEnvironmentVariable("PATH", toolsPath + Path.PathSeparator + previousPath);

            foreach (var token in new[] { "current-token-b", "current-token-c" })
            {
                var header = "Authorization: Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("bot:" + token));
                await File.WriteAllTextAsync(expectedHeader, header);
                await File.WriteAllTextAsync(Path.Combine(source, "README.md"), token);
                await runner.RunGit(source, "add .");
                await runner.RunGit(source, "-c commit.gpgsign=false commit -m Update");
                await workspace.ResetRepo(cache, "master", endpoint, mode, "bot:" + token);
                Assert.AreEqual(await runner.RunGit(source, "rev-parse HEAD"), await runner.RunGit(cache, "rev-parse HEAD"));
                var configPath = mode == CloneMode.Bare ? Path.Combine(cache, "config") : Path.Combine(cache, ".git", "config");
                var config = await File.ReadAllTextAsync(configPath);
                Assert.Contains(oldToken, config, "The existing working copy must be reused, rather than recloned.");
                Assert.DoesNotContain(token, config);
                Assert.DoesNotContain(header, config);
                Assert.IsFalse(logs.Messages.Any(message => message.Contains(token) || message.Contains(header) || message.Contains(oldToken)));
            }

            Assert.HasCount(2, await File.ReadAllLinesAsync(accepted));
            await workspace.Clone(fresh, "master", endpoint, mode, "bot:current-token-c");
            Assert.AreEqual(await runner.RunGit(source, "rev-parse HEAD"), await runner.RunGit(fresh, "rev-parse HEAD"));
            var freshConfig = await File.ReadAllTextAsync(mode == CloneMode.Bare
                ? Path.Combine(fresh, "config") : Path.Combine(fresh, ".git", "config"));
            Assert.DoesNotContain("current-token-c", freshConfig);
            Assert.AreEqual(previousAuthentication, Environment.GetEnvironmentVariable("GIT_CONFIG_VALUE_3"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("GIT_CONFIG_GLOBAL", previousGlobalConfig);
            Environment.SetEnvironmentVariable("PATH", previousPath);
        }
    }

    [TestMethod]
    [DataRow("https://bot:credential-sentinel@mirror.example.test/repository.git")]
    [DataRow("https://credential-sentinel@mirror.example.test/repository.git")]
    [DataRow("HTTPS://bot:credential%2Dsentinel@mirror.example.test/repository.git")]
    [DataRow("ssh://bot:credential-sentinel@mirror.example.test/repository.git")]
    public async Task RemoteDiagnosticsRedactCredentialsWithoutChangingStoredUrls(string remoteUrl)
    {
        var logs = new CredentialLogCapture();
        using var services = new ServiceCollection()
            .AddLogging(builder => builder.SetMinimumLevel(LogLevel.Trace).AddProvider(logs))
            .AddGitRunner().BuildServiceProvider();
        var runner = services.GetRequiredService<GitRunner.Services.GitCommandRunner>();
        var workspace = services.GetRequiredService<WorkspaceManager>();
        await runner.RunGit(_tempPath!, "init");
        await workspace.AddRemote(_tempPath!, "target", remoteUrl);
        Assert.AreEqual(remoteUrl, await workspace.GetRemoteUrl(_tempPath!, "target"));
        var updatedUrl = remoteUrl.Replace("repository.git", "updated.git");
        await workspace.SetRemoteUrl(_tempPath!, "target", updatedUrl);
        await workspace.AddOrSetRemoteUrl(_tempPath!, "target", updatedUrl);
        Assert.AreEqual(updatedUrl, await workspace.GetRemoteUrl(_tempPath!, "target"));
        Assert.IsTrue(logs.Messages.Any(message => message.Contains("mirror.example.test/updated.git")));
        Assert.IsFalse(logs.Messages.Any(message => message.Contains("credential-sentinel") || message.Contains("credential%2Dsentinel")),
            "Rendered logs, structured fields and exceptions must not expose remote credentials.");
    }

    [TestMethod]
    [DataRow("failure")]
    [DataRow("lfs")]
    [DataRow("missing")]
    [DataRow("timeout")]
    public async Task CommandFailuresRedactCredentialsInAllDiagnostics(string failure)
    {
        const string remoteUrl = "https://bot:credential-sentinel@mirror.example.test/repository.git";
        var command = new DiagnosticCommandService(failure, remoteUrl);
        var logs = new CredentialLogCapture();
        using var loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Trace).AddProvider(logs));
        var runner = new GitRunner.Services.GitCommandRunner(command, loggerFactory.CreateLogger<GitRunner.Services.GitCommandRunner>());
        Exception? caught = null;
        try
        {
            await runner.RunGit(_tempPath!, "fetch " + remoteUrl, setSafeDirectoryFirst: false);
        }
        catch (Exception exception)
        {
            caught = exception;
        }
        Assert.IsNotNull(caught);
        Assert.AreEqual("fetch " + remoteUrl, command.Arguments, "Execution must receive the original arguments.");
        Assert.DoesNotContain("credential-sentinel", caught.ToString());
        if (failure == "timeout")
            Assert.IsInstanceOfType<TimeoutException>(caught);
        else
        {
            Assert.IsInstanceOfType<GitCommandException>(caught);
            var gitException = (GitCommandException)caught;
            foreach (var field in new[] { gitException.Command, gitException.GitOutput, gitException.GitError, gitException.Path })
                Assert.DoesNotContain("credential-sentinel", field);
        }
        Assert.IsFalse(logs.Messages.Any(message => message.Contains("credential-sentinel")));
    }

    [TestMethod]
    public async Task SuccessfulCommandReturnsOriginalOutputButRedactsItsLog()
    {
        const string remoteUrl = "https://bot:credential-sentinel@mirror.example.test/repository.git";
        var logs = new CredentialLogCapture();
        using var loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Trace).AddProvider(logs));
        var runner = new GitRunner.Services.GitCommandRunner(new DiagnosticCommandService("success", remoteUrl), loggerFactory.CreateLogger<GitRunner.Services.GitCommandRunner>());
        Assert.AreEqual(remoteUrl + remoteUrl,
            await runner.RunGit(_tempPath!, "remote get-url target", setSafeDirectoryFirst: false));
        Assert.IsFalse(logs.Messages.Any(message => message.Contains("credential-sentinel")));
    }

    [TestMethod]
    public async Task FailedPushRedactsItsEndpointAndException()
    {
        const string remoteUrl = "https://bot:credential-sentinel@mirror.example.test/repository.git";
        var logs = new CredentialLogCapture();
        using var loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Trace).AddProvider(logs));
        var runner = new GitRunner.Services.GitCommandRunner(new DiagnosticCommandService("push", remoteUrl), loggerFactory.CreateLogger<GitRunner.Services.GitCommandRunner>());
        var workspace = new WorkspaceManager(loggerFactory.CreateLogger<WorkspaceManager>(),
            _serviceProvider!.GetRequiredService<RetryEngine>(), runner);
        var exception = await Assert.ThrowsExactlyAsync<GitCommandException>(() => workspace.Push(_tempPath!, "master", remoteUrl));
        Assert.DoesNotContain("credential-sentinel", exception.ToString());
        Assert.IsFalse(logs.Messages.Any(message => message.Contains("credential-sentinel")));
        Assert.IsTrue(logs.Messages.Any(message => message.Contains("Git push failed")));
    }

    [TestMethod]
    public void GitExceptionsRedactEveryExposedDiagnosticField()
    {
        const string remoteUrl = "https://bot:credential-sentinel@mirror.example.test/repository.git";
        var exception = new GitCommandException(remoteUrl, remoteUrl, remoteUrl, remoteUrl, remoteUrl);
        foreach (var field in new[] { exception.ToString(), exception.Command, exception.GitOutput, exception.GitError, exception.Path })
        {
            Assert.DoesNotContain("credential-sentinel", field);
            Assert.Contains("mirror.example.test/repository.git", field);
        }
    }

    private sealed class DiagnosticCommandService(string failure, string remoteUrl) : CommandService
    {
        public string? Arguments { get; private set; }
        public override Task<(int code, string output, string error)> RunCommandAsync(string bin, string arg, string path,
            TimeSpan? timeout = null, bool killTimeoutProcess = true, IDictionary<string, string?>? environmentVariables = null)
        {
            Arguments = arg;
            if (failure == "push" && arg.Contains("remote set-url"))
                return Task.FromResult((0, string.Empty, string.Empty));
            if (failure == "timeout")
                throw new TimeoutException("Execute command: git " + arg + " timed out.", new Exception(remoteUrl));
            if (failure == "missing")
                throw new Win32Exception("git was not found");
            return Task.FromResult((failure == "success" ? 0 : 1,
                failure == "lfs" ? "'git-lfs' was not found " + remoteUrl : remoteUrl, remoteUrl));
        }
    }

    private sealed class CredentialLogCapture : ILoggerProvider, ILogger
    {
        public List<string> Messages { get; } = [];
        public ILogger CreateLogger(string categoryName) => this;
        public bool IsEnabled(LogLevel logLevel) => true;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public void Dispose() { }
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
            if (state is IEnumerable<KeyValuePair<string, object?>> fields)
                Messages.AddRange(fields.Select(field => field.Value?.ToString() ?? string.Empty));
            if (exception != null)
                Messages.Add(exception.ToString());
        }
    }

    [TestMethod]
    public void FetchKeepsBinaryCompatibleOverloads()
    {
        Assert.IsNotNull(typeof(WorkspaceManager).GetMethod(nameof(WorkspaceManager.Fetch), [typeof(string)]));
        Assert.IsNotNull(typeof(WorkspaceManager).GetMethod(
            nameof(WorkspaceManager.Fetch),
            [typeof(string), typeof(TimeSpan?)]));
    }

    [TestMethod]
    public async Task TestCloneEditCommitThenPush()
    {
        var workspaceManager = _serviceProvider!.GetRequiredService<WorkspaceManager>();
        Assert.IsNotNull(_tempPath);
        await workspaceManager.ResetRepo(_tempPath!, null, "https://github.com/AiursoftWeb/gitrunner.git",
            CloneMode.Depth1);
        Assert.IsTrue(Directory.Exists(_tempPath));

        var readmePath = Path.Combine(_tempPath!, "READMETest.md");
        await File.WriteAllTextAsync(readmePath, "Hello world!");
        var pendingCommit = await workspaceManager.PendingCommit(_tempPath!);
        Assert.IsTrue(pendingCommit);
        await workspaceManager.SetUserConfig(_tempPath!, "tester", "tester@dotnet");
        var committed = await workspaceManager.CommitToBranch(_tempPath!, "Test commit", "master");
        Assert.IsTrue(committed);
        try
        {
            await workspaceManager.Push(_tempPath!, "master", "https://bad/", true);
            Assert.Fail();
        }
        catch (GitCommandException e)
        {
            Console.WriteLine(e);
            Assert.Contains("fatal: unable to access 'https://bad/': Could not resolve host: bad", e.Message);
        }
    }

    [TestMethod]
    [DataRow("repo")]
    [DataRow("repo with spaces")]
    public async Task GetCommitsSupportsConcurrentReadersWhileGlobalConfigIsLocked(string directoryName)
    {
        var previousConfig = Environment.GetEnvironmentVariable("GIT_CONFIG_GLOBAL");
        var configPath = Path.Combine(_tempPath!, "isolated.gitconfig");
        var repoPath = Path.Combine(_tempPath!, directoryName);
        Environment.SetEnvironmentVariable("GIT_CONFIG_GLOBAL", configPath);
        try
        {
            Directory.CreateDirectory(repoPath);
            var workspaceManager = _serviceProvider!.GetRequiredService<WorkspaceManager>();
            var commandRunner = _serviceProvider!.GetRequiredService<GitRunner.Services.GitCommandRunner>();
            await commandRunner.RunGit(repoPath, "init", setSafeDirectoryFirst: false);
            await commandRunner.RunGit(repoPath, "config user.name Tester", setSafeDirectoryFirst: false);
            await commandRunner.RunGit(repoPath, "config user.email tester@example.test", setSafeDirectoryFirst: false);
            await File.WriteAllTextAsync(Path.Combine(repoPath, "README.md"), "Concurrent readers");
            await commandRunner.RunGit(repoPath, "add README.md", setSafeDirectoryFirst: false);
            await commandRunner.RunGit(repoPath, "-c commit.gpgsign=false commit -m \"Initial commit\"", setSafeDirectoryFirst: false);
            await File.WriteAllTextAsync(configPath + ".lock", "Another Git process owns this lock.");

            var results = await Task.WhenAll(Enumerable.Range(0, 32)
                .Select(_ => workspaceManager.GetCommits(repoPath)));

            foreach (var commits in results)
            {
                Assert.HasCount(1, commits);
                Assert.AreEqual("Initial commit", commits[0].Message);
                Assert.AreEqual(results[0][0].Hash, commits[0].Hash);
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("GIT_CONFIG_GLOBAL", previousConfig);
        }
    }

    [TestMethod]
    public async Task TestInitAddAndCommit()
    {
        var workspaceManager = _serviceProvider!.GetRequiredService<WorkspaceManager>();
        Assert.IsNotNull(_tempPath);
        await workspaceManager.Init(_tempPath!);
        Assert.IsTrue(Directory.Exists(_tempPath));

        // Create a new file
        var readmePath = Path.Combine(_tempPath!, "README.md");
        await File.WriteAllTextAsync(readmePath, "Hello world!");
        Assert.IsTrue(File.Exists(readmePath));

        // Add the file and commit
        await workspaceManager.AddAndCommit(_tempPath!, "Add README.md");

        // Check the commit
        var commits = await workspaceManager.GetCommits(_tempPath!);
        Assert.HasCount(1, commits);
    }

    [TestMethod]
    public async Task TestMirrorAllBranches()
    {
        var workspaceManager = _serviceProvider!.GetRequiredService<WorkspaceManager>();
        Assert.IsNotNull(_tempPath);
        await workspaceManager.Init(_tempPath!);
        Assert.IsTrue(Directory.Exists(_tempPath));

        await workspaceManager.AddOrSetRemoteUrl(
            path: _tempPath!,
            remoteName: "origin",
            remoteUrl: "https://github.com/Anduin2017/AnduinOS.git");
        await workspaceManager.EnsureAllLocalBranchesUpToDateWithRemote(
            path: _tempPath!,
            remote: "origin");

        var branches = await workspaceManager.GetAllLocalBranches(_tempPath!);
        Assert.IsGreaterThan(1, branches.Length);
    }

    [TestMethod]
    public async Task TestRemoteManagement()
    {
        var workspaceManager = _serviceProvider!.GetRequiredService<WorkspaceManager>();
        Assert.IsNotNull(_tempPath);
        await workspaceManager.Init(_tempPath!);
        Assert.IsTrue(Directory.Exists(_tempPath));

        // Get remotes (no remote)
        var remotes = await workspaceManager.GetRemoteNames(_tempPath!);
        Assert.IsEmpty(remotes);

        // Add a remote.
        await workspaceManager.AddRemote(_tempPath!, "origin", "https://github.com/Anduin2017/AnduinOS.git");
        remotes = await workspaceManager.GetRemoteNames(_tempPath!);
        Assert.HasCount(1, remotes);

        // Change the remote URL.
        await workspaceManager.SetRemoteUrl(_tempPath!, "origin", "https://github.com/Anduin2017/AnduinOS.git");
        var newUrl = await workspaceManager.GetRemoteUrl(_tempPath!, "origin");
        Assert.AreEqual("https://github.com/Anduin2017/AnduinOS.git", newUrl);

        // Remove the remote.
        await workspaceManager.DeleteRemote(_tempPath!, "origin");
        var remotesAfterDelete = await workspaceManager.GetRemoteNames(_tempPath!);
        Assert.IsEmpty(remotesAfterDelete);
    }
}

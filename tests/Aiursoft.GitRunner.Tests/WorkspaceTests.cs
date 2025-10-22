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
            "https://gitlab.aiursoft.cn/aiursoft/gitrunner.git",
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
            "https://gitlab.aiursoft.cn/aiursoft/gitrunner.git",
            CloneMode.Depth1);
        Assert.IsTrue(Directory.Exists(_tempPath));
        var branch = await workspaceManager.GetBranch(_tempPath!);
        Assert.AreEqual("master", branch);
    }

    [TestMethod]
    public async Task TestReset()
    {
        var workspaceManager = _serviceProvider!.GetRequiredService<WorkspaceManager>();
        await workspaceManager.ResetRepo(_tempPath!, "master", "https://gitlab.aiursoft.cn/aiursoft/gitrunner.git",
            CloneMode.Depth1);
        Assert.IsTrue(Directory.Exists(_tempPath));
    }

    [TestMethod]
    public async Task TestResetTwice()
    {
        var workspaceManager = _serviceProvider!.GetRequiredService<WorkspaceManager>();
        await workspaceManager.ResetRepo(_tempPath!, "master", "https://gitlab.aiursoft.cn/aiursoft/gitrunner.git",
            CloneMode.Depth1);
        await workspaceManager.ResetRepo(_tempPath!, "master", "https://gitlab.aiursoft.cn/aiursoft/manhours.git",
            CloneMode.Depth1);
        var actualRemote = await workspaceManager.GetRemoteUrl(_tempPath!, "origin");
        Assert.AreEqual("https://gitlab.aiursoft.cn/aiursoft/manhours.git", actualRemote);
        Assert.IsTrue(Directory.Exists(_tempPath));
    }

    [TestMethod]
    public async Task TestResetDefaultBranch()
    {
        var workspaceManager = _serviceProvider!.GetRequiredService<WorkspaceManager>();
        await workspaceManager.ResetRepo(_tempPath!, null, "https://gitlab.aiursoft.cn/aiursoft/gitrunner.git",
            CloneMode.Depth1);
        Assert.IsTrue(Directory.Exists(_tempPath));
    }

    [TestMethod]
    public async Task TestResetBareDefaultBranch()
    {
        var workspaceManager = _serviceProvider!.GetRequiredService<WorkspaceManager>();
        await workspaceManager.ResetRepo(_tempPath!, null, "https://gitlab.aiursoft.cn/aiursoft/gitrunner.git",
            CloneMode.BareWithOnlyCommits);
        await workspaceManager.ResetRepo(_tempPath!, null, "https://gitlab.aiursoft.cn/aiursoft/gitrunner.git",
            CloneMode.BareWithOnlyCommits);
        Assert.IsTrue(Directory.Exists(_tempPath));
    }

    [TestMethod]
    public async Task TestGetBranch()
    {
        var workspaceManager = _serviceProvider!.GetRequiredService<WorkspaceManager>();
        await workspaceManager.ResetRepo(_tempPath!, "master", "https://gitlab.aiursoft.cn/aiursoft/gitrunner.git",
            CloneMode.Depth1);
        var branch = await workspaceManager.GetBranch(_tempPath!);
        Assert.AreEqual("master", branch);
    }

    [TestMethod]
    public async Task TestGetRemoteUrl()
    {
        var workspaceManager = _serviceProvider!.GetRequiredService<WorkspaceManager>();
        await workspaceManager.ResetRepo(_tempPath!, "master", "https://gitlab.aiursoft.cn/aiursoft/gitrunner.git",
            CloneMode.Depth1);
        var remote = await workspaceManager.GetRemoteUrl(_tempPath!, "origin");
        Assert.AreEqual("https://gitlab.aiursoft.cn/aiursoft/gitrunner.git", remote);
    }

    [TestMethod]
    public async Task TestGetCommitTimes()
    {
        var workspaceManager = _serviceProvider!.GetRequiredService<WorkspaceManager>();
        await workspaceManager.ResetRepo(_tempPath!, "master", "https://gitlab.aiursoft.cn/aiursoft/gitrunner.git",
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
        await workspaceManager.ResetRepo(_tempPath!, "master", "https://gitlab.aiursoft.cn/aiursoft/gitrunner.git",
            CloneMode.OnlyCommits);
        var remote = await workspaceManager.GetRemoteUrl(_tempPath!, "origin");
        Assert.AreEqual("https://gitlab.aiursoft.cn/aiursoft/gitrunner.git", remote);
    }

    [TestMethod]
    public async Task TestResetRepoTwoTimes()
    {
        var workspaceManager = _serviceProvider!.GetRequiredService<WorkspaceManager>();
        var commandService = _serviceProvider!.GetRequiredService<GitRunner.Services.GitCommandRunner>();
        await workspaceManager.ResetRepo(_tempPath!, null, "https://gitlab.aiursoft.cn/aiursoft/gitrunner.git",
            CloneMode.Depth1);
        Assert.IsTrue(Directory.Exists(_tempPath));

        try
        {
            await commandService.RunGit(_tempPath!, "remote remove origin");
            await workspaceManager.ResetRepo(_tempPath!, null, "https://gitlab.aiursoft.cn/aiursoft/gitrunner.git",
                CloneMode.Depth1);
            Assert.Fail("After removing the remote, we should not be able to reset the repo.");
        }
        catch (GitCommandException e)
        {
            Console.WriteLine(e);
        }
    }

    [TestMethod]
    public async Task TestCloneEditCommitThenPush()
    {
        var workspaceManager = _serviceProvider!.GetRequiredService<WorkspaceManager>();
        Assert.IsNotNull(_tempPath);
        await workspaceManager.ResetRepo(_tempPath!, null, "https://gitlab.aiursoft.cn/aiursoft/gitrunner.git",
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
            remoteUrl: "https://gitlab.aiursoft.cn/anduin/anduinos.git");
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
        await workspaceManager.AddRemote(_tempPath!, "origin", "https://gitlab.aiursoft.cn/anduin/anduinos.git");
        remotes = await workspaceManager.GetRemoteNames(_tempPath!);
        Assert.HasCount(1, remotes);

        // Change the remote URL.
        await workspaceManager.SetRemoteUrl(_tempPath!, "origin", "https://gitlab.aiursoft.cn/anduin/anduinos.git");
        var newUrl = await workspaceManager.GetRemoteUrl(_tempPath!, "origin");
        Assert.AreEqual("https://gitlab.aiursoft.cn/anduin/anduinos.git", newUrl);

        // Remove the remote.
        await workspaceManager.DeleteRemote(_tempPath!, "origin");
        var remotesAfterDelete = await workspaceManager.GetRemoteNames(_tempPath!);
        Assert.IsEmpty(remotesAfterDelete);
    }
}

using Aiursoft.GitRunner.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aiursoft.GitRunner.Tests;

[TestClass]
public class WorkspaceTests
{
    private IServiceProvider? _serviceProvider;
    private string? tempPath;

    [TestInitialize]
    public void Init()
    {
        _serviceProvider = new ServiceCollection()
            .AddLogging()
            .AddGitRunner()
            .BuildServiceProvider();
        tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempPath);
    }

    [TestCleanup]
    public void Clean()
    {
        // Delete the tempPath
        if (Directory.Exists(tempPath))
        {
            // Delete it, suppress error
            try
            {
                Directory.Delete(tempPath, true);
            }
            catch
            {
                // ignored
            }
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
            tempPath!, 
            "master", 
            "https://gitlab.aiursoft.cn/aiursoft/gitrunner.git",
            mode);
        Assert.IsTrue(Directory.Exists(tempPath));
    }
    
    [TestMethod]
    public async Task TestReset()
    {
        var workspaceManager = _serviceProvider!.GetRequiredService<WorkspaceManager>();
        await workspaceManager.ResetRepo(tempPath!, "master", "https://gitlab.aiursoft.cn/aiursoft/gitrunner.git",
            CloneMode.Depth1);
        Assert.IsTrue(Directory.Exists(tempPath));
    }
    
    [TestMethod]
    public async Task TestGetBranch()
    {
        var workspaceManager = _serviceProvider!.GetRequiredService<WorkspaceManager>();
        await workspaceManager.ResetRepo(tempPath!, "master", "https://gitlab.aiursoft.cn/aiursoft/gitrunner.git",
            CloneMode.Depth1);
        var branch = await workspaceManager.GetBranch(tempPath!);
        Assert.AreEqual("master", branch);
    }
    
    [TestMethod]
    public async Task TestGetRemoteUrl()
    {
        var workspaceManager = _serviceProvider!.GetRequiredService<WorkspaceManager>();
        await workspaceManager.ResetRepo(tempPath!, "master", "https://gitlab.aiursoft.cn/aiursoft/gitrunner.git",
            CloneMode.Depth1);
        var remote = await workspaceManager.GetRemoteUrl(tempPath!);
        Assert.AreEqual("https://gitlab.aiursoft.cn/aiursoft/gitrunner.git", remote);
    }
    
    [TestMethod]
    public async Task TestGetCommitTimes()
    {
        var workspaceManager = _serviceProvider!.GetRequiredService<WorkspaceManager>();
        await workspaceManager.ResetRepo(tempPath!, "master", "https://gitlab.aiursoft.cn/aiursoft/gitrunner.git",
            CloneMode.OnlyCommits);
        var commits = await workspaceManager.GetCommitTimes(tempPath!);
        Assert.IsTrue(commits.Length > 8);
        Assert.IsTrue(commits[0] > commits[1]);
    }
}

using Aiursoft.Canon;
using Aiursoft.CSTools.Services;
using Aiursoft.GitRunner.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aiursoft.GitRunner;

public static class Extensions
{
    /// <summary>
    /// Register git runners.
    /// 
    /// (If your project is using Aiursoft.Scanner, you do NOT have to call this!)
    /// </summary>
    /// <param name="services">Services to be injected.</param>
    /// <returns>The original services.</returns>
    public static IServiceCollection AddGitRunner(this IServiceCollection services)
    {
        services.AddTransient<CommandService>();
        services.AddTaskCanon();
        services.AddTransient<WorkspaceManager>();
        services.AddTransient<GitCommandRunner>();
        return services;
    }
}

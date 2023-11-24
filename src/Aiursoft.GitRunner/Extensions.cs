using Aiursoft.Canon;
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
        services.AddTaskCanon();
        services.AddTransient<WorkspaceManager>();
        services.AddTransient<CommandRunner>();
        return services;
    }
}

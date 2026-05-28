using AutoInjectGenerator;
using LoggerProviderExtensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;

namespace UpdateTool;

internal class HostBuilder
{
    public static HostApplicationBuilder Create(Action<IServiceCollection>? configService = null)
    {
        var configuration = new ConfigurationManager();
        configuration.AddEnvironmentVariables("ASPNETCORE_");
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings()
        {
            Configuration = configuration
        });
        builder.Logging.AddHubLogger()
            .AddLocalFileLogger(f =>
            {
                f.SaveByCategory = true;
                f.SaveByLevel = true;
            });
        builder.AutoInject();
        configService?.Invoke(builder.Services);
        return builder;
    }
}

[AutoInjectContext]
internal static partial class AutoInjectContext
{
    public static partial void AutoInject(this IHostApplicationBuilder builder);
}
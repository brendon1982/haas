using HaaS.Adapters.Agent;
using HaaS.Adapters.Store;
using HaaS.Domain.Ports;
using HaaS.Infrastructure;
using HaaS.Host.CLI.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace HaaS.Host.CLI;

public static class HaasCliServiceExtensions
{
    public static HaasBuilder WithSpectreConsole(this HaasBuilder builder, CliLayoutManager? layoutManager = null)
    {
        if (layoutManager != null)
        {
            builder.Services.AddSingleton<CliLogSink>(sp => layoutManager.LogSink);
            builder.Services.AddSingleton<CliLayoutManager>(sp => layoutManager);
        }
        else
        {
            builder.Services.AddSingleton<CliLogSink>();
            builder.Services.AddSingleton<CliLayoutManager>();
        }
        builder.Services.AddSingleton<CliSignalPresenter>();
        
        // Replace existing ILogger with SpectreLogger
        builder.Services.RemoveAll<HaaS.Domain.Ports.ILogger>();
        builder.Services.AddSingleton<HaaS.Domain.Ports.ILogger, SpectreLogger>();

        // Redirect Microsoft.Extensions.Logging to CliLogSink
        builder.Services.AddLogging(logging => logging.ClearProviders());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, SpectreLoggingProvider>());
        
        return builder;
    }
}

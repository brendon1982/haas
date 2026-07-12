using HaaS.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HaaS.Infrastructure;

public class SignalWorkerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly int _workerCount;

    public SignalWorkerService(IServiceProvider serviceProvider, int workerCount = 1)
    {
        _serviceProvider = serviceProvider;
        _workerCount = workerCount;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var tasks = Enumerable.Range(0, _workerCount)
            .Select(_ => RunWorkerAsync(stoppingToken));
        
        return Task.WhenAll(tasks);
    }

    private async Task RunWorkerAsync(CancellationToken stoppingToken)
    {
        // Each worker gets its own scope if needed
        using var scope = _serviceProvider.CreateScope();
        var worker = scope.ServiceProvider.GetRequiredService<SignalWorker>();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await worker.ProcessNextAsync(stoppingToken);
            }
            catch (Exception)
            {
                // Silent catch to prevent one worker from killing the pool
            }

            // Small delay to avoid tight loop when queue is empty
            await Task.Delay(50, stoppingToken);
        }
    }
}

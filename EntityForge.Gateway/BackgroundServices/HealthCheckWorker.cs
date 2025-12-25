using EntityForge.Gateway.Services;
using EntityForge.Shared.Models;

namespace EntityForge.Gateway.BackgroundServices;

public class HealthCheckWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<HealthCheckWorker> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(120);

    public HealthCheckWorker(IServiceProvider serviceProvider, ILogger<HealthCheckWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Health Check background service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var registryService = scope.ServiceProvider.GetRequiredService<ServiceRegistryService>();
                var httpClientFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();

                var services = await registryService.GetAllServicesAsync();

                var runningServices = services
                    .Where(s => s.Status == ServiceStatus.Running)
                    .ToList();

                if (runningServices.Count == 0)
                {
                    await Task.Delay(_checkInterval, stoppingToken);
                    continue;
                }

                _logger.LogDebug("Checking health for {Count} running services", runningServices.Count);

                var healthCheckTasks = runningServices.Select(async service =>
                {
                    var isHealthy = await CheckServiceHealthAsync(service.Url, httpClientFactory, stoppingToken);
                    return new { Service = service, IsHealthy = isHealthy };
                }).ToList();

                var results = await Task.WhenAll(healthCheckTasks);

                foreach (var result in results.Where(r => r.Service.IsHealthy != r.IsHealthy))
                {
                    _logger.LogWarning("Service {ServiceName} health changed: {OldStatus} -> {NewStatus}",
                        result.Service.Name,
                        result.Service.IsHealthy ? "Healthy" : "Unhealthy",
                        result.IsHealthy ? "Healthy" : "Unhealthy");

                    await registryService.UpdateHealthStatusAsync(result.Service.Name, result.IsHealthy);
                }

                var healthyCount = results.Count(r => r.IsHealthy);
                _logger.LogInformation("Health check completed: {Healthy}/{Total} services are healthy",
                    healthyCount, results.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during health check");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Health Check stopped");
    }

    private async Task<bool> CheckServiceHealthAsync(string serviceUrl, IHttpClientFactory httpClientFactory,
        CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);

            var healthUrl = $"{serviceUrl}/health";
            var response = await client.GetAsync(healthUrl, ct);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Health check failed for {Url}: {Message}", serviceUrl, ex.Message);
            return false;
        }
    }
}
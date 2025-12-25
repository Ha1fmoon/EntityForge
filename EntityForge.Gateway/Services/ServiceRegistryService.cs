using System.Text.Json;
using EntityForge.Shared.Models;
using StackExchange.Redis;

namespace EntityForge.Gateway.Services;

public class ServiceRegistryService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<ServiceRegistryService> _logger;
    private const string ServiceKeyPrefix = "service:";

    public ServiceRegistryService(IConnectionMultiplexer redis, ILogger<ServiceRegistryService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task RegisterServiceAsync(ServiceInfo service)
    {
        var db = _redis.GetDatabase();
        var key = $"{ServiceKeyPrefix}{service.Name}";
        var json = JsonSerializer.Serialize(service);

        await db.StringSetAsync(key, json);
        _logger.LogInformation("Service {ServiceName} registered at {Url}", service.Name, service.Url);
    }

    public async Task<ServiceInfo?> GetServiceAsync(string serviceName)
    {
        var db = _redis.GetDatabase();
        var key = $"{ServiceKeyPrefix}{serviceName}";
        var json = await db.StringGetAsync(key);

        if (json.IsNullOrEmpty)
        {
            _logger.LogWarning("Service {ServiceName} not found in registry", serviceName);
            return null;
        }

        return JsonSerializer.Deserialize<ServiceInfo>(json!);
    }

    public async Task<ServiceInfo?> GetServiceByEntityAsync(string entityName)
    {
        var services = await GetAllServicesAsync();
        return services.FirstOrDefault(s => 
            s.EntityName.Equals(entityName, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<List<ServiceInfo>> GetAllServicesAsync()
    {
        var db = _redis.GetDatabase();
        var server = _redis.GetServer(_redis.GetEndPoints().First());
        var keys = server.Keys(pattern: $"{ServiceKeyPrefix}*").ToList();

        var tasks = keys.Select(async key =>
        {
            var json = await db.StringGetAsync(key);
            return json.IsNullOrEmpty ? null : JsonSerializer.Deserialize<ServiceInfo>(json!);
        });

        var results = await Task.WhenAll(tasks);
        return results.Where(s => s != null).ToList()!;
    }

    public async Task<bool> UnregisterServiceAsync(string serviceName)
    {
        var db = _redis.GetDatabase();
        var key = $"{ServiceKeyPrefix}{serviceName}";
        var deleted = await db.KeyDeleteAsync(key);

        if (deleted)
            _logger.LogInformation("Service {ServiceName} unregistered", serviceName);
        else
            _logger.LogWarning("Failed to unregister service {ServiceName}", serviceName);

        return deleted;
    }

    public async Task UpdateHealthStatusAsync(string serviceName, bool isHealthy)
    {
        var service = await GetServiceAsync(serviceName);
        if (service == null) return;

        service.IsHealthy = isHealthy;
        service.LastHealthCheck = DateTime.UtcNow;

        await RegisterServiceAsync(service);
    }
}
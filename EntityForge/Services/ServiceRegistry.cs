using System.Collections.Concurrent;
using System.Text.Json;
using EntityForge.Shared.Models;
using StackExchange.Redis;

namespace EntityForge.Services;

public class ServiceRegistry
{
    private readonly ConcurrentDictionary<string, ServiceInfo> _services = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<int> _reservedPorts = [];
    private readonly object _portLock = new();
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<ServiceRegistry> _logger;
    private const string ServiceKeyPrefix = "service:";

    public ServiceRegistry(IConnectionMultiplexer redis, ILogger<ServiceRegistry> logger)
    {
        _redis = redis;
        _logger = logger;
        LoadFromRedis();
    }

    private void LoadFromRedis()
    {
        try
        {
            var db = _redis.GetDatabase();
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var keys = server.Keys(pattern: $"{ServiceKeyPrefix}*").ToList();

            foreach (var key in keys)
            {
                var json = db.StringGet(key);

                if (json.IsNullOrEmpty) continue;

                var service = JsonSerializer.Deserialize<ServiceInfo>(json!);
                if (service != null) _services[service.Name] = service;
            }

            _logger.LogInformation("Loaded {Count} services from Redis", _services.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load services from Redis");
        }
    }

    public void AddOrUpdate(ServiceInfo info)
    {
        if (string.IsNullOrWhiteSpace(info.Name)) return;

        _services[info.Name] = info;

        Task.Run(async () =>
        {
            try
            {
                var db = _redis.GetDatabase();
                var key = $"{ServiceKeyPrefix}{info.Name}";
                var json = JsonSerializer.Serialize(info);
                await db.StringSetAsync(key, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save service {ServiceName} to Redis", info.Name);
            }
        });
    }

    public bool TryGet(string name, out ServiceInfo? info)
    {
        var ok = _services.TryGetValue(name, out var value);
        info = value;
        return ok;
    }

    public IEnumerable<ServiceInfo> GetAll()
    {
        return _services.Values.OrderBy(s => s.Name);
    }

    public async Task<bool> RemoveAsync(string name)
    {
        var removedFromCache = _services.TryRemove(name, out _);

        var db = _redis.GetDatabase();
        var key = $"{ServiceKeyPrefix}{name}";
        var removedFromRedis = await db.KeyDeleteAsync(key);

        return removedFromCache || removedFromRedis;
    }

    public async Task<ServiceInfo?> GetByNameAsync(string name)
    {
        if (_services.TryGetValue(name, out var cached))
            return cached;

        var db = _redis.GetDatabase();
        var key = $"{ServiceKeyPrefix}{name}";
        var json = await db.StringGetAsync(key);

        if (json.IsNullOrEmpty) return null;

        var service = JsonSerializer.Deserialize<ServiceInfo>(json!);
        if (service != null) _services[service.Name] = service;
        return service;
    }

    public int GetNextVersion(string serviceName)
    {
        if (_services.TryGetValue(serviceName, out var existing)) return existing.Version + 1;
        return 1;
    }

    public HashSet<int> GetUsedPorts()
    {
        lock (_portLock)
        {
            var usedPorts = new HashSet<int>();
            foreach (var service in _services.Values.Where(s => s.Status == ServiceStatus.Running))
            {
                usedPorts.Add(service.AppPort);
                usedPorts.Add(service.DbPort);
            }

            foreach (var port in _reservedPorts) usedPorts.Add(port);
            return usedPorts;
        }
    }

    public void ReservePorts(int appPort, int dbPort)
    {
        lock (_portLock)
        {
            _reservedPorts.Add(appPort);
            _reservedPorts.Add(dbPort);
        }
    }

    public void ReleasePorts(int appPort, int dbPort)
    {
        lock (_portLock)
        {
            _reservedPorts.Remove(appPort);
            _reservedPorts.Remove(dbPort);
        }
    }
}
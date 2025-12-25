using System.Text.Json;
using EntityForge.Models;
using StackExchange.Redis;

namespace EntityForge.Data;

public class EntityRepository
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<EntityRepository> _logger;
    private const string EntityKeyPrefix = "entity:";

    public EntityRepository(IConnectionMultiplexer redis, ILogger<EntityRepository> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<IEnumerable<EntityConfig>> GetAllAsync()
    {
        var db = _redis.GetDatabase();
        var server = _redis.GetServer(_redis.GetEndPoints().First());
        var keys = server.Keys(pattern: $"{EntityKeyPrefix}*").ToList();

        var entities = new List<EntityConfig>();

        foreach (var key in keys)
        {
            var json = await db.StringGetAsync(key);

            if (json.IsNullOrEmpty) continue;

            var entity = JsonSerializer.Deserialize<EntityConfig>(json!);
            if (entity != null) entities.Add(entity);
        }

        return entities.OrderBy(e => e.Name);
    }

    public async Task<EntityConfig?> GetByNameAsync(string name)
    {
        var db = _redis.GetDatabase();
        var key = $"{EntityKeyPrefix}{name}";
        var json = await db.StringGetAsync(key);

        if (json.IsNullOrEmpty) return null;

        return JsonSerializer.Deserialize<EntityConfig>(json!);
    }

    public async Task<EntityConfig> CreateAsync(EntityConfig entity)
    {
        var db = _redis.GetDatabase();
        var key = $"{EntityKeyPrefix}{entity.Name}";
        var json = JsonSerializer.Serialize(entity);

        await db.StringSetAsync(key, json);
        _logger.LogInformation("Entity {EntityName} created", entity.Name);

        return entity;
    }

    public async Task<EntityConfig> UpdateAsync(EntityConfig entity)
    {
        var db = _redis.GetDatabase();
        var key = $"{EntityKeyPrefix}{entity.Name}";
        var json = JsonSerializer.Serialize(entity);

        await db.StringSetAsync(key, json);
        _logger.LogInformation("Entity {EntityName} updated", entity.Name);

        return entity;
    }

    public async Task DeleteAsync(string name)
    {
        var db = _redis.GetDatabase();
        var key = $"{EntityKeyPrefix}{name}";
        var deleted = await db.KeyDeleteAsync(key);

        if (deleted) _logger.LogInformation("Entity {EntityName} deleted", name);
    }
}
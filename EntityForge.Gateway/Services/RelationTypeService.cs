using System.Text.Json;
using EntityForge.Gateway.Models;
using StackExchange.Redis;

namespace EntityForge.Gateway.Services;

public class RelationTypeService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RelationTypeService> _logger;
    private const string RelationTypeKeyPrefix = "relation-type:";

    public RelationTypeService(IConnectionMultiplexer redis, ILogger<RelationTypeService> logger)
    {
        _redis = redis;
        _logger = logger;
    }
    
    public async Task<RelationTypeDefinition?> GetByIdAsync(string id)
    {
        var db = _redis.GetDatabase();
        var key = $"{RelationTypeKeyPrefix}{id}";
        var json = await db.StringGetAsync(key);

        if (json.IsNullOrEmpty) return null;

        return JsonSerializer.Deserialize<RelationTypeDefinition>(json!);
    }

    public async Task<IEnumerable<RelationTypeDefinition>> GetForEntityAsync(string entityName)
    {
        var db = _redis.GetDatabase();
        var server = _redis.GetServer(_redis.GetEndPoints().First());
        var keys = server.Keys(pattern: $"{RelationTypeKeyPrefix}*").ToList();

        var tasks = keys.Select(async key =>
        {
            var json = await db.StringGetAsync(key);
            if (json.IsNullOrEmpty) return null;

            var relationType = JsonSerializer.Deserialize<RelationTypeDefinition>(json!);
            if (relationType == null) return null;

            if (relationType.Entity1.Equals(entityName, StringComparison.OrdinalIgnoreCase) ||
                relationType.Entity2.Equals(entityName, StringComparison.OrdinalIgnoreCase))
                return relationType;

            return null;
        });

        var results = await Task.WhenAll(tasks);
        return results.Where(r => r != null).ToList()!;
    }

    public async Task CreateAsync(RelationTypeDefinition relationType)
    {
        var db = _redis.GetDatabase();
        var entities = new[] { relationType.Entity1, relationType.Entity2 }.OrderBy(e => e).ToArray();
        relationType.Id = $"{entities[0]}-{entities[1]}";
        var existing = await GetByIdAsync(relationType.Id);

        if (existing != null) throw new InvalidOperationException($"Relation type '{relationType.Id}' already exists");

        var key = $"{RelationTypeKeyPrefix}{relationType.Id}";
        var json = JsonSerializer.Serialize(relationType);

        await db.StringSetAsync(key, json);

        _logger.LogInformation("Relation type created: {Id} ({Entity1} <-> {Entity2}, {Cardinality})",
            relationType.Id, relationType.Entity1, relationType.Entity2, relationType.Cardinality);
    }

    public async Task DeleteAsync(string id)
    {
        var db = _redis.GetDatabase();
        var key = $"{RelationTypeKeyPrefix}{id}";

        var deleted = await db.KeyDeleteAsync(key);

        if (deleted) _logger.LogInformation("Relation type deleted: {Id}", id);
    }

    public static string GetRelatedEntityName(RelationTypeDefinition relationType, string entityName)
    {
        if (relationType.Entity1.Equals(entityName, StringComparison.OrdinalIgnoreCase))
            return relationType.Entity2;
        if (relationType.Entity2.Equals(entityName, StringComparison.OrdinalIgnoreCase))
            return relationType.Entity1;

        throw new ArgumentException($"Entity '{entityName}' is not in '{relationType.Id}'");
    }
}
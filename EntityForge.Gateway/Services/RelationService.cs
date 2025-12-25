using System.Text.Json;
using EntityForge.Gateway.Models;
using StackExchange.Redis;

namespace EntityForge.Gateway.Services;

public class RelationService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RelationService> _logger;
    private const string RelationKeyPrefix = "relation:";

    public RelationService(IConnectionMultiplexer redis, ILogger<RelationService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    private async Task<EntityRelation?> GetRelationAsync(string sourceEntity, string sourceId, string relatedEntity)
    {
        var db = _redis.GetDatabase();
        var key = BuildRelationKey(sourceEntity, sourceId, relatedEntity);
        var json = await db.StringGetAsync(key);

        if (json.IsNullOrEmpty) return null;

        return JsonSerializer.Deserialize<EntityRelation>(json!);
    }

    public async Task<Dictionary<string, List<string>>> GetAllRelationsAsync(string sourceEntity, string sourceId)
    {
        var db = _redis.GetDatabase();
        var server = _redis.GetServer(_redis.GetEndPoints().First());
        var pattern = $"{RelationKeyPrefix}{sourceEntity.ToLower()}:{sourceId}:*";
        var keys = server.Keys(pattern: pattern).ToList();

        var tasks = keys.Select(async key =>
        {
            var json = await db.StringGetAsync(key);
            if (json.IsNullOrEmpty) return null;
            return JsonSerializer.Deserialize<EntityRelation>(json!);
        });

        var results = await Task.WhenAll(tasks);

        return results
            .Where(r => r != null)
            .ToDictionary(r => r!.RelatedEntity, r => r!.RelatedIds);
    }

    private async Task RemoveRelationAsync(string sourceEntity, string sourceId, string relatedEntity,
        string? relatedId = null)
    {
        var db = _redis.GetDatabase();
        var key = BuildRelationKey(sourceEntity, sourceId, relatedEntity);

        if (relatedId != null)
        {
            var relation = await GetRelationAsync(sourceEntity, sourceId, relatedEntity);
            if (relation == null) return;

            relation.RelatedIds.Remove(relatedId);

            if (relation.RelatedIds.Count == 0)
            {
                await db.KeyDeleteAsync(key);
                _logger.LogInformation("Relation fully removed: {Key}", key);
            }
            else
            {
                var json = JsonSerializer.Serialize(relation);
                await db.StringSetAsync(key, json);
                _logger.LogInformation("Related ID {Id} removed from {Key}", relatedId, key);
            }
        }
        else
        {
            var deleted = await db.KeyDeleteAsync(key);
            if (deleted) _logger.LogInformation("Relation removed: {Key}", key);
        }
    }

    private async Task RemoveAllRelationsForEntityAsync(string entity, string entityId)
    {
        var db = _redis.GetDatabase();
        var server = _redis.GetServer(_redis.GetEndPoints().First());

        var pattern = $"{RelationKeyPrefix}{entity.ToLower()}:{entityId}:*";
        var keys = server.Keys(pattern: pattern).ToArray();

        if (keys.Length > 0)
        {
            await db.KeyDeleteAsync(keys);
            _logger.LogInformation("Removed {Count} relations for {Entity}:{Id}", keys.Length, entity, entityId);
        }
    }

    public async Task AddAsync(string sourceEntity, string sourceId, string relatedEntity, List<string> relatedIds)
    {
        await AddOrUpdateRelationAsync(sourceEntity, sourceId, relatedEntity, relatedIds);

        var reverseTasks = relatedIds.Select(relatedId =>
            AddOrUpdateRelationAsync(relatedEntity, relatedId, sourceEntity, [sourceId]));
        await Task.WhenAll(reverseTasks);

        _logger.LogInformation("Relation added: {Source}:{SourceId} - {Related}:{RelatedIds}",
            sourceEntity, sourceId, relatedEntity, string.Join(",", relatedIds));
    }

    private async Task AddOrUpdateRelationAsync(string sourceEntity, string sourceId, string relatedEntity,
        List<string> newRelatedIds)
    {
        var db = _redis.GetDatabase();
        var key = BuildRelationKey(sourceEntity, sourceId, relatedEntity);

        var existing = await GetRelationAsync(sourceEntity, sourceId, relatedEntity);
        var mergedIds = existing != null ? existing.RelatedIds.Union(newRelatedIds).ToList() : newRelatedIds;

        var relation = new EntityRelation
        {
            SourceEntity = sourceEntity,
            SourceId = sourceId,
            RelatedEntity = relatedEntity,
            RelatedIds = mergedIds
        };

        var json = JsonSerializer.Serialize(relation);
        await db.StringSetAsync(key, json);
    }

    public async Task RemoveAsync(string sourceEntity, string sourceId, string relatedEntity, string relatedId)
    {
        await Task.WhenAll(
            RemoveRelationAsync(sourceEntity, sourceId, relatedEntity, relatedId),
            RemoveRelationAsync(relatedEntity, relatedId, sourceEntity, sourceId)
        );

        _logger.LogInformation("Relation removed: {Source}:{SourceId} - {Related}:{RelatedId}",
            sourceEntity, sourceId, relatedEntity, relatedId);
    }

    public async Task RemoveAllAsync(string entity, string entityId)
    {
        var relations = await GetAllRelationsAsync(entity, entityId);

        var removeTasks = relations
            .SelectMany(r => r.Value.Select(relatedId =>
                RemoveRelationAsync(r.Key, relatedId, entity, entityId)));
        await Task.WhenAll(removeTasks);

        await RemoveAllRelationsForEntityAsync(entity, entityId);

        _logger.LogInformation("All relations removed for {Entity}:{Id}", entity, entityId);
    }

    public async Task RemoveAllRelationsForEntityTypeAsync(string entityType)
    {
        var db = _redis.GetDatabase();
        var server = _redis.GetServer(_redis.GetEndPoints().First());
        var entityLower = entityType.ToLower();

        var sourceKeys = server.Keys(pattern: $"{RelationKeyPrefix}{entityLower}:*").ToList();

        var allKeys = server.Keys(pattern: $"{RelationKeyPrefix}*").ToList();
        var targetKeys = allKeys.Where(k => k.ToString().EndsWith($":{entityLower}")).ToList();

        var allKeysToDelete = sourceKeys.Concat(targetKeys).Distinct().ToArray();

        if (allKeysToDelete.Length > 0)
        {
            await db.KeyDeleteAsync(allKeysToDelete);
            _logger.LogInformation("Removed {Count} relations for entity type {EntityType}",
                allKeysToDelete.Length, entityType);
        }
    }

    public async Task RemoveAllRelationsBetweenEntitiesAsync(string entity1, string entity2)
    {
        var db = _redis.GetDatabase();
        var server = _redis.GetServer(_redis.GetEndPoints().First());
        var entity1Lower = entity1.ToLower();
        var entity2Lower = entity2.ToLower();

        var keys1 = server.Keys(pattern: $"{RelationKeyPrefix}{entity1Lower}:*:{entity2Lower}").ToList();
        var keys2 = server.Keys(pattern: $"{RelationKeyPrefix}{entity2Lower}:*:{entity1Lower}").ToList();

        var allKeysToDelete = keys1.Concat(keys2).Distinct().ToArray();

        if (allKeysToDelete.Length > 0)
        {
            await db.KeyDeleteAsync(allKeysToDelete);
            _logger.LogInformation("Removed {Count} relations between {Entity1} and {Entity2}",
                allKeysToDelete.Length, entity1, entity2);
        }
    }

    private static string BuildRelationKey(string sourceEntity, string sourceId, string relatedEntity)
    {
        return $"{RelationKeyPrefix}{sourceEntity.ToLower()}:{sourceId}:{relatedEntity.ToLower()}";
    }
}
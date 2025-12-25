using System.Text;
using System.Text.Json;
using EntityForge.Gateway.Services;
using Microsoft.AspNetCore.Mvc;

namespace EntityForge.Gateway.Controllers;

[ApiController]
[Route("api/gateway")]
public class GatewayController : ControllerBase
{
    private readonly RoutingService _routing;
    private readonly RelationService _relationService;
    private readonly ILogger<GatewayController> _logger;

    public GatewayController(RoutingService routing, RelationService relationService, ILogger<GatewayController> logger)
    {
        _routing = routing;
        _relationService = relationService;
        _logger = logger;
    }

    [HttpGet("{entity}/{id}")]
    public async Task<IActionResult> GetById(string entity, string id, CancellationToken ct)
    {
        _logger.LogInformation("GET /{Entity}/{Id}", entity, id);

        var response =
            await _routing.RouteRequestAsync(entity, $"api/{entity.ToLower()}s?ids={id}", HttpMethod.Get, null, ct);

        if (!response.IsSuccessStatusCode)
            return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync(ct));

        var content = await response.Content.ReadAsStringAsync(ct);

        var array = JsonSerializer.Deserialize<JsonElement>(content);
        if (array.ValueKind == JsonValueKind.Array && array.GetArrayLength() > 0)
        {
            var firstElement = array[0].GetRawText();
            var enrichedContent = await FillWithRelationsAsync(entity, id, firstElement, ct);
            return Content(enrichedContent, "application/json");
        }

        return NotFound($"{entity} with id '{id}' not found");
    }

    [HttpGet("{entity}")]
    public async Task<IActionResult> GetMultiple(string entity, [FromQuery] string? ids, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(ids))
        {
            _logger.LogInformation("GET /{Entity} (all)", entity);
            var response =
                await _routing.RouteRequestAsync(entity, $"api/{entity.ToLower()}s", HttpMethod.Get, null, ct);

            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync(ct));

            return Content(await response.Content.ReadAsStringAsync(ct), "application/json");
        }

        _logger.LogInformation("GET /{Entity}?ids={Ids}", entity, ids);

        var multipleResponse = await _routing.RouteMultipleRequestsAsync(entity, ids, ct);

        if (!multipleResponse.IsSuccessStatusCode)
            return StatusCode((int)multipleResponse.StatusCode, await multipleResponse.Content.ReadAsStringAsync(ct));

        return Content(await multipleResponse.Content.ReadAsStringAsync(ct), "application/json");
    }

    [HttpPost("{entity}")]
    public async Task<IActionResult> Create(string entity, [FromBody] JsonElement data, CancellationToken ct)
    {
        if (data.ValueKind != JsonValueKind.Object)
            return BadRequest(new { message = "Request body must be a JSON object" });
        
        _logger.LogInformation("POST /{Entity}", entity);

        var (cleanData, relations) = ExtractRelations(data);

        var json = cleanData.GetRawText();
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response =
            await _routing.RouteRequestAsync(entity, $"api/{entity.ToLower()}s", HttpMethod.Post, content, ct);

        if (!response.IsSuccessStatusCode)
            return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync(ct));

        var responseContent = await response.Content.ReadAsStringAsync(ct);

        if (relations == null || relations.Count <= 0)
            return Content(responseContent, "application/json");

        var createdEntity = JsonSerializer.Deserialize<JsonElement>(responseContent);

        string? createdId = null;
        if (createdEntity.ValueKind == JsonValueKind.String)
            createdId = createdEntity.GetString();
        else if (createdEntity.ValueKind == JsonValueKind.Object && createdEntity.TryGetProperty("id", out var idProp))
            createdId = idProp.ValueKind == JsonValueKind.String ? idProp.GetString() : idProp.ToString();

        if (string.IsNullOrEmpty(createdId))
            return Content(responseContent, "application/json");

        await SaveRelationsAsync(entity, createdId, relations);

        return Content(responseContent, "application/json");
    }

    [HttpPut("{entity}/{id}")]
    public async Task<IActionResult> Update(string entity, string id, [FromBody] JsonElement data, CancellationToken ct)
    {
        if (data.ValueKind != JsonValueKind.Object)
            return BadRequest(new { message = "Request body must be a JSON object" });

        _logger.LogInformation("PUT /{Entity}/{Id}", entity, id);

        var (cleanData, relations) = ExtractRelations(data);

        var json = cleanData.GetRawText();
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response =
            await _routing.RouteRequestAsync(entity, $"api/{entity.ToLower()}s/{id}", HttpMethod.Put, content, ct);

        if (!response.IsSuccessStatusCode)
            return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync(ct));

        if (relations != null && relations.Count > 0)
            await UpdateRelationsAsync(entity, id, relations);

        return NoContent();
    }

    [HttpDelete("{entity}/{id}")]
    public async Task<IActionResult> Delete(string entity, string id, CancellationToken ct)
    {
        _logger.LogInformation("DELETE /{Entity}/{Id}", entity, id);

        var response =
            await _routing.RouteRequestAsync(entity, $"api/{entity.ToLower()}s/{id}", HttpMethod.Delete, null, ct);

        if (!response.IsSuccessStatusCode)
            return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync(ct));

        await _relationService.RemoveAllAsync(entity, id);

        return NoContent();
    }

    private (JsonElement cleanData, Dictionary<string, List<string>>? relations) ExtractRelations(JsonElement data)
    {
        if (!data.TryGetProperty("relations", out var relationsElement))
            return (data, null);

        var relations = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var prop in relationsElement.EnumerateObject())
        {
            var ids = new List<string>();
            foreach (var idElement in prop.Value.EnumerateArray())
                ids.Add(idElement.GetString() ?? idElement.ToString());
            relations[prop.Name.ToLowerInvariant()] = ids;
        }

        var cleanProperties = new Dictionary<string, object>();
        foreach (var prop in data.EnumerateObject())
            if (prop.Name != "relations")
                cleanProperties[prop.Name] = JsonSerializer.Deserialize<object>(prop.Value.GetRawText())!;

        var cleanJson = JsonSerializer.Serialize(cleanProperties);
        return (JsonSerializer.Deserialize<JsonElement>(cleanJson), relations);
    }

    private async Task SaveRelationsAsync(string entity, string entityId, Dictionary<string, List<string>> relations)
    {
        foreach (var (relatedEntity, relatedIds) in relations)
        {
            if (relatedIds.Count <= 0) continue;

            var relatedEntityLower = relatedEntity.ToLowerInvariant();
            await _relationService.AddAsync(entity, entityId, relatedEntityLower, relatedIds);
            _logger.LogInformation("Relations saved: {Entity}:{Id} -> {RelatedEntity}:{RelatedIds}",
                entity, entityId, relatedEntityLower, string.Join(",", relatedIds));
        }
    }

    private async Task UpdateRelationsAsync(string entity, string entityId,
        Dictionary<string, List<string>> newRelations)
    {
        var currentRelations = await _relationService.GetAllRelationsAsync(entity, entityId);
        var currentRelationsLower = currentRelations.ToDictionary(
            keyValue => keyValue.Key.ToLowerInvariant(), 
            keyValue => keyValue.Value,
            StringComparer.OrdinalIgnoreCase);

        foreach (var (relatedEntity, newIds) in newRelations)
        {
            var relatedEntityLower = relatedEntity.ToLowerInvariant();
            var currentIds = currentRelationsLower.GetValueOrDefault(relatedEntityLower, []);

            var toRemove = currentIds.Except(newIds).ToList();
            foreach (var idToRemove in toRemove)
                await _relationService.RemoveAsync(entity, entityId, relatedEntityLower, idToRemove);

            var toAdd = newIds.Except(currentIds).ToList();
            if (toAdd.Count > 0)
                await _relationService.AddAsync(entity, entityId, relatedEntityLower, toAdd);
        }

        _logger.LogInformation("Relations updated for {Entity}:{Id}", entity, entityId);
    }

    private async Task<string> FillWithRelationsAsync(string entity, string entityId, string originalContent,
        CancellationToken ct)
    {
        try
        {
            var entityData = JsonSerializer.Deserialize<Dictionary<string, object>>(originalContent);
            if (entityData == null) return originalContent;

            var relations = await _relationService.GetAllRelationsAsync(entity, entityId);
            if (relations.Count == 0) return originalContent;

            var relationsData = new Dictionary<string, List<object>>();

            foreach (var (relatedEntity, relatedIds) in relations)
            {
                var relatedItems = new List<object>();

                foreach (var relatedId in relatedIds)
                {
                    var relatedInfo = await GetRelatedEntityInfoAsync(relatedEntity, relatedId, ct);
                    if (relatedInfo != null)
                        relatedItems.Add(relatedInfo);
                }

                if (relatedItems.Count > 0)
                    relationsData[relatedEntity] = relatedItems;
            }

            if (relationsData.Count > 0)
                entityData["relations"] = relationsData;

            return JsonSerializer.Serialize(entityData, new JsonSerializerOptions { WriteIndented = false });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enrich entity with relations");
            return originalContent;
        }
    }

    private async Task<object?> GetRelatedEntityInfoAsync(string entity, string id, CancellationToken ct)
    {
        try
        {
            var response =
                await _routing.RouteRequestAsync(entity, $"api/{entity.ToLower()}s?ids={id}", HttpMethod.Get, null, ct);

            if (!response.IsSuccessStatusCode) return null;

            var content = await response.Content.ReadAsStringAsync(ct);
            var array = JsonSerializer.Deserialize<JsonElement>(content);

            if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() == 0)
                return null;

            var data = array[0];

            string? name = null;
            if (data.TryGetProperty("name", out var nameProp))
                name = nameProp.GetString();
            else if (data.TryGetProperty("title", out var titleProp))
                name = titleProp.GetString();
            else
                foreach (var prop in data.EnumerateObject())
                    if (prop.Name != "id" && prop.Value.ValueKind == JsonValueKind.String)
                    {
                        name = prop.Value.GetString();
                        break;
                    }

            return new
            {
                id,
                name = name ?? $"{entity} #{id}",
                link = $"/api/gateway/{entity.ToLower()}/{id}"
            };
        }
        catch
        {
            return new
            {
                id,
                name = $"{entity} #{id}",
                link = $"/api/gateway/{entity.ToLower()}/{id}"
            };
        }
    }
}
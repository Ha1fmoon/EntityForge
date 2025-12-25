using System.Text;
using System.Text.Json;
using EntityForge.Gateway.Models;
using EntityForge.Gateway.Services;
using Microsoft.AspNetCore.Mvc;

namespace EntityForge.Gateway.Controllers;

[ApiController]
[Route("api/gateway/entities")]
public class EntityGenerationController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly RelationTypeService _relationTypeService;
    private readonly RelationService _relationService;
    private readonly ServiceRegistryService _serviceRegistry;
    private readonly ILogger<EntityGenerationController> _logger;
    private readonly string _coreUrl;

    public EntityGenerationController(IHttpClientFactory httpClientFactory, RelationTypeService relationTypeService,
        RelationService relationService, ServiceRegistryService serviceRegistry, IConfiguration configuration,
        ILogger<EntityGenerationController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _relationTypeService = relationTypeService;
        _relationService = relationService;
        _serviceRegistry = serviceRegistry;
        _logger = logger;
        _coreUrl = configuration["CoreService:Url"] ??
                   throw new InvalidOperationException("CoreService:Url configuration is missing");
    }

    [HttpPost]
    public async Task<IActionResult> CreateEntity([FromBody] JsonElement data, CancellationToken ct)
    {
        if (data.ValueKind != JsonValueKind.Object)
            return BadRequest(new { message = "Request body must be a JSON object" });
        
        _logger.LogInformation("Creating entity with relations handling");

        var (entityData, relations) = ExtractRelations(data);
        var entityName = GetEntityName(entityData);

        if (string.IsNullOrEmpty(entityName))
            return BadRequest(new { message = "Entity name is required" });

        if (relations != null)
        {
            var validationError = await ValidateRelationsAsync(relations);
            if (validationError != null)
                return BadRequest(new { message = validationError });
        }

        var content = new StringContent(entityData.GetRawText(), Encoding.UTF8, "application/json");
        var (success, response) = await SendHttpRequestAsync(HttpMethod.Post, "/api/entities", content, ct);

        if (!success)
            return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync(ct));

        if (relations != null)
            await CreateRelationTypesAsync(entityName, relations);

        return Content(await response.Content.ReadAsStringAsync(ct), "application/json");
    }

    [HttpPut("{name}")]
    public async Task<IActionResult> UpdateEntity(string name, [FromBody] JsonElement data, CancellationToken ct)
    {
        if (data.ValueKind != JsonValueKind.Object)
            return BadRequest(new { message = "Request body must be a JSON object" });

        _logger.LogInformation("Updating entity {Name} with relations handling", name);

        var (cleanData, relations) = ExtractRelations(data);

        if (relations != null)
        {
            var validationError = await ValidateRelationsAsync(relations);
            if (validationError != null)
                return BadRequest(new { message = validationError });
        }

        var content = new StringContent(cleanData.GetRawText(), Encoding.UTF8, "application/json");
        var (success, response) = await SendHttpRequestAsync(HttpMethod.Put, $"/api/entities/{name}", content, ct);

        if (!success)
            return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync(ct));

        if (relations != null)
            await UpdateRelationTypesAsync(name, relations);

        return Content(await response.Content.ReadAsStringAsync(ct), "application/json");
    }

    [HttpDelete("{name}")]
    public async Task<IActionResult> DeleteEntity(string name, CancellationToken ct)
    {
        _logger.LogInformation("Deleting entity {Name} with full cleanup", name);

        var serviceName = $"{name}Service";
        
        await SendHttpRequestAsync(HttpMethod.Delete, $"/api/services/{serviceName}", null, ct);

        var (success, response) = await SendHttpRequestAsync(HttpMethod.Delete, $"/api/entities/{name}", null, ct);

        if (!success)
            return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync(ct));

        await _relationService.RemoveAllRelationsForEntityTypeAsync(name);
        await DeleteRelationTypesForEntityAsync(name);

        _logger.LogInformation("Entity {Name} fully deleted", name);
        return NoContent();
    }

    [HttpPost("{name}/generate")]
    public async Task<IActionResult> GenerateService(string name)
    {
        _logger.LogInformation("Generating service for entity {Name}", name);

        var (success, response) =
            await SendHttpRequestAsync(HttpMethod.Post, $"/api/entities/{name}/generate", null, CancellationToken.None);

        if (!success)
            return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());

        return Content(await response.Content.ReadAsStringAsync(), "application/json");
    }

    [HttpGet]
    public async Task<IActionResult> GetEntities(CancellationToken ct)
    {
        var (success, response) = await SendHttpRequestAsync(HttpMethod.Get, "/api/entities", null, ct);

        if (!success)
            return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync(ct));

        return Content(await response.Content.ReadAsStringAsync(ct), "application/json");
    }

    [HttpGet("{name}")]
    public async Task<IActionResult> GetEntity(string name, CancellationToken ct)
    {
        var (success, response) = await SendHttpRequestAsync(HttpMethod.Get, $"/api/entities/{name}", null, ct);

        if (!success)
            return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync(ct));

        var responseContent = await response.Content.ReadAsStringAsync(ct);
        var enriched = await FillEntityWithRelationTypesAsync(name, responseContent);

        return Content(enriched, "application/json");
    }

    private async Task<(bool Success, HttpResponseMessage Response)> SendHttpRequestAsync(HttpMethod method,
        string path, HttpContent? content, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();
        var request = new HttpRequestMessage(method, $"{_coreUrl}{path}") { Content = content };
        var response = await client.SendAsync(request, ct);
        return (response.IsSuccessStatusCode, response);
    }

    private static (JsonElement cleanData, List<RelationInput>? relations) ExtractRelations(JsonElement data)
    {
        if (!data.TryGetProperty("relations", out var relationsElement))
            return (data, null);

        var relations = new List<RelationInput>();

        foreach (var item in relationsElement.EnumerateArray())
        {
            var entity = item.GetProperty("entity").GetString()!;
            var cardinality = RelationCardinality.ManyToMany;

            if (item.TryGetProperty("cardinality", out var cardProp))
                Enum.TryParse(cardProp.GetString(), true, out cardinality);

            relations.Add(new RelationInput(entity, cardinality));
        }

        var cleanedEntity = new Dictionary<string, object>();
        foreach (var prop in data.EnumerateObject())
            if (prop.Name != "relations")
                cleanedEntity[prop.Name] = JsonSerializer.Deserialize<object>(prop.Value.GetRawText())!;

        var cleanJson = JsonSerializer.Serialize(cleanedEntity);

        return (JsonSerializer.Deserialize<JsonElement>(cleanJson), relations);
    }

    private static string? GetEntityName(JsonElement data)
    {
        return data.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
    }

    private async Task<string?> ValidateRelationsAsync(List<RelationInput> relations)
    {
        var services = await _serviceRegistry.GetAllServicesAsync();
        var existingEntities = services.Select(s => s.EntityName.ToLower()).ToHashSet();

        foreach (var relation in relations)
            if (!existingEntities.Contains(relation.Entity.ToLower()))
                return $"Related entity '{relation.Entity}' does not exist";

        return null;
    }

    private async Task CreateRelationTypesAsync(string entityName, List<RelationInput> relations)
    {
        foreach (var relation in relations)
            try
            {
                var relationType = new RelationTypeDefinition
                {
                    Entity1 = entityName,
                    Entity2 = relation.Entity,
                    Cardinality = relation.Cardinality
                };

                await _relationTypeService.CreateAsync(relationType);
                _logger.LogInformation("Relation type created: {Entity1} - {Entity2}", entityName, relation.Entity);
            }
            catch (InvalidOperationException)
            {
                _logger.LogInformation("Relation type {Entity1} - {Entity2} already exists", entityName,
                    relation.Entity);
            }
    }

    private async Task UpdateRelationTypesAsync(string entityName, List<RelationInput> newRelations)
    {
        var currentTypes = (await _relationTypeService.GetForEntityAsync(entityName)).ToList();
        var currentRelatedEntities = currentTypes
            .Select(t => RelationTypeService.GetRelatedEntityName(t, entityName).ToLower())
            .ToHashSet();

        var newRelatedEntities = newRelations.Select(r => r.Entity.ToLower()).ToHashSet();

        foreach (var type in currentTypes)
        {
            var relatedEntity = RelationTypeService.GetRelatedEntityName(type, entityName);

            if (newRelatedEntities.Contains(relatedEntity.ToLower())) continue;

            await _relationService.RemoveAllRelationsBetweenEntitiesAsync(entityName, relatedEntity);
            await _relationTypeService.DeleteAsync(type.Id);
            _logger.LogInformation("Relation type and its instances deleted: {Id}", type.Id);
        }

        foreach (var relation in newRelations)
            if (!currentRelatedEntities.Contains(relation.Entity.ToLower()))
                await CreateRelationTypesAsync(entityName, [relation]);
    }

    private async Task DeleteRelationTypesForEntityAsync(string entityName)
    {
        var types = await _relationTypeService.GetForEntityAsync(entityName);

        foreach (var type in types)
        {
            await _relationTypeService.DeleteAsync(type.Id);
            _logger.LogInformation("Relation type deleted: {Id}", type.Id);
        }
    }

    private async Task<string> FillEntityWithRelationTypesAsync(string entityName, string originalContent)
    {
        try
        {
            var entityData = JsonSerializer.Deserialize<Dictionary<string, object>>(originalContent);
            if (entityData == null) return originalContent;

            var relationTypes = await _relationTypeService.GetForEntityAsync(entityName);

            var relationsInfo = relationTypes.Select(rt => new
            {
                entity = RelationTypeService.GetRelatedEntityName(rt, entityName),
                cardinality = rt.Cardinality.ToString()
            }).ToList();

            if (relationsInfo.Count != 0)
                entityData["relations"] = relationsInfo;

            return JsonSerializer.Serialize(entityData);
        }
        catch
        {
            return originalContent;
        }
    }
}

public record RelationInput(string Entity, RelationCardinality Cardinality);
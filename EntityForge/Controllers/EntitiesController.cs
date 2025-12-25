using System.Text.RegularExpressions;
using EntityForge.Data;
using EntityForge.Models;
using EntityForge.Services;
using EntityForge.Services.ProjectGeneration;
using EntityForge.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace EntityForge.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EntitiesController : ControllerBase
{
    private readonly EntityRepository _entityRepository;
    private readonly ILogger<EntitiesController> _logger;
    private readonly GenerationService _generationService;
    private readonly TypeService _type;

    public EntitiesController(EntityRepository entityRepository, GenerationService generationService, TypeService type,
        ILogger<EntitiesController> logger)
    {
        _entityRepository = entityRepository;
        _generationService = generationService;
        _type = type;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<EntityConfig>>> GetEntities()
    {
        try
        {
            var entities = await _entityRepository.GetAllAsync();
            return Ok(entities);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving entities");
            return StatusCode(500, "Error retrieving entities");
        }
    }

    [HttpGet("{name}")]
    public async Task<ActionResult<EntityConfig>> GetEntity(string name)
    {
        try
        {
            var entity = await _entityRepository.GetByNameAsync(name);
            if (entity == null)
                return NotFound($"Entity '{name}' not found");

            return Ok(entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving entity {EntityName}", name);
            return StatusCode(500, $"Error retrieving entity {name}");
        }
    }

    [HttpPost]
    public async Task<ActionResult<EntityConfig>> CreateEntity([FromBody] EntityConfig entityConfig)
    {
        try
        {
            var validation = ValidateEntity(entityConfig);
            if (!validation.IsValid)
                return BadRequest(validation.ErrorMessage);

            var existing = await _entityRepository.GetByNameAsync(entityConfig.Name);
            if (existing != null)
                return BadRequest($"Entity '{entityConfig.Name}' already exists");

            foreach (var field in entityConfig.Fields)
            {
                var type = _type.FindTypeById(field.Type.Id);
                if (type == null)
                    return BadRequest($"Type with ID '{field.Type.Id}' not found");
                
                field.Type = type;
            }

            var created = await _entityRepository.CreateAsync(entityConfig);

            _logger.LogInformation("Entity {EntityName} was created", entityConfig.Name);

            return CreatedAtAction(nameof(GetEntity), new { name = entityConfig.Name }, created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating entity {EntityName}", entityConfig.Name);
            return StatusCode(500, $"Error creating entity {entityConfig.Name}");
        }
    }

    [HttpPut("{name}")]
    public async Task<ActionResult<EntityConfig>> UpdateEntity(string name, [FromBody] EntityConfig entityConfig)
    {
        try
        {
            if (name != entityConfig.Name)
                return BadRequest("Entity name in URL and body should be the same");

            var validation = ValidateEntity(entityConfig);
            if (!validation.IsValid)
                return BadRequest(validation.ErrorMessage);

            var existingEntity = await _entityRepository.GetByNameAsync(name);
            if (existingEntity == null)
                return NotFound($"Entity '{name}' not found");

            foreach (var field in entityConfig.Fields)
            {
                var type = _type.FindTypeById(field.Type.Id);
                if (type == null)
                    return BadRequest($"Type with ID '{field.Type.Id}' not found");
                
                field.Type = type;
            }

            var updated = await _entityRepository.UpdateAsync(entityConfig);

            _logger.LogInformation("Entity {EntityName} was updated", entityConfig.Name);

            return Ok(updated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while updating {EntityName}", name);
            return StatusCode(500, $"Error while updating {name}");
        }
    }

    [HttpDelete("{name}")]
    public async Task<ActionResult> DeleteEntity(string name)
    {
        try
        {
            var entity = await _entityRepository.GetByNameAsync(name);
            if (entity == null)
                return NotFound($"Entity '{name}' not found");

            await _entityRepository.DeleteAsync(name);

            _logger.LogInformation("Entity {EntityName} deleted", name);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while deleting {EntityName}", name);
            return StatusCode(500, $"Error while deleting {name}");
        }
    }

    [HttpPost("{name}/generate")]
    public async Task<ActionResult<ServiceInfo>> GenerateService(string name)
    {
        try
        {
            var entity = await _entityRepository.GetByNameAsync(name);
            if (entity == null)
                return NotFound($"Entity '{name}' not found");

            var serviceInfo = await _generationService.ExecuteAsync(entity, CancellationToken.None);

            entity.IsGenerated = serviceInfo.Status == ServiceStatus.Running;
            entity.LastGenerated = DateTime.UtcNow;

            await _entityRepository.UpdateAsync(entity);

            _logger.LogInformation("Entity {EntityName} was generated. Status = {Status}", name, serviceInfo.Status);

            return Ok(serviceInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while generating {EntityName} service", name);
            return StatusCode(500, $"Error while generating {name} service. {ex.Message}");
        }
    }

    private static (bool IsValid, string ErrorMessage) ValidateEntity(EntityConfig entityConfig)
    {
        if (string.IsNullOrWhiteSpace(entityConfig.Name))
            return (false, "Entity name cant be empty");

        if (!IsEnglishLettersOnly(entityConfig.Name))
            return (false, "Entity name must contain only english letters (A-Z, a-z)");

        if (!entityConfig.Fields.Any())
            return (false, "Entity must have at least one field");

        foreach (var field in entityConfig.Fields)
        {
            if (string.IsNullOrWhiteSpace(field.Name))
                return (false, "Field name cant be empty");

            if (string.IsNullOrWhiteSpace(field.Type.Id))
                return (false, $"Field '{field.Name}' must have type");
        }

        return (true, string.Empty);
    }

    private static bool IsEnglishLettersOnly(string value)
    {
        return Regex.IsMatch(value, @"^[a-zA-Z]+$");
    }
}
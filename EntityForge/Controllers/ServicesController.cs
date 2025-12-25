using EntityForge.Data;
using EntityForge.Services;
using EntityForge.Services.ProjectGeneration;
using EntityForge.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace EntityForge.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ServicesController : ControllerBase
{
    private readonly ServiceRegistry _registry;
    private readonly CleanupService _cleanup;
    private readonly EntityRepository _entityRepository;
    private readonly ILogger<ServicesController> _logger;

    public ServicesController(ServiceRegistry registry, CleanupService cleanup,
        EntityRepository entityRepository, ILogger<ServicesController> logger)
    {
        _registry = registry;
        _cleanup = cleanup;
        _entityRepository = entityRepository;
        _logger = logger;
    }

    [HttpGet]
    public ActionResult<IEnumerable<ServiceInfo>> GetAll()
    {
        try
        {
            return Ok(_registry.GetAll());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving services");
            return StatusCode(500, "Error retrieving services");
        }
    }

    [HttpGet("{name}")]
    public ActionResult<ServiceInfo> GetByName(string name)
    {
        try
        {
            if (_registry.TryGet(name, out var info) && info != null)
                return Ok(info);
            return NotFound($"Service '{name}' not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving service {ServiceName}", name);
            return StatusCode(500, $"Error retrieving service {name}");
        }
    }

    [HttpDelete("{name}")]
    public async Task<IActionResult> Delete(string name)
    {
        try
        {
            var serviceInfo = await _registry.GetByNameAsync(name);
            if (serviceInfo == null)
            {
                _logger.LogWarning("Service {ServiceName} is not found", name);
                return NotFound($"Service '{name}' is not found");
            }

            _logger.LogInformation("Removing service {ServiceName}", name);
            var success = await _cleanup.CleanupServiceAsync(serviceInfo);

            await _registry.RemoveAsync(name);

            var entity = await _entityRepository.GetByNameAsync(serviceInfo.EntityName);
            if (entity != null)
            {
                entity.IsGenerated = false;
                await _entityRepository.UpdateAsync(entity);
                _logger.LogInformation("Entity {EntityName} status now is not generated", serviceInfo.EntityName);
            }

            if (!success) return Ok(new { message = "Cleanup error. Service was not removed" });

            _logger.LogInformation("Service {ServiceName} was removed successfully", name);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while removing service {ServiceName}", name);
            return StatusCode(500, $"Error while removing service {name}: {ex.Message}");
        }
    }
}
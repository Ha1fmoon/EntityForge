using EntityForge.Gateway.Services;
using Microsoft.AspNetCore.Mvc;

namespace EntityForge.Gateway.Controllers;

[ApiController]
[Route("api/gateway/services")]
public class ServiceManagementController : ControllerBase
{
    private readonly ServiceRegistryService _registryService;
    private readonly RelationTypeService _relationTypeService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public ServiceManagementController(ServiceRegistryService registryService, RelationTypeService relationTypeService,
        IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _registryService = registryService;
        _relationTypeService = relationTypeService;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllServices()
    {
        var services = await _registryService.GetAllServicesAsync();
        return Ok(services);
    }

    [HttpGet("{serviceName}")]
    public async Task<IActionResult> GetService(string serviceName)
    {
        var service = await _registryService.GetServiceAsync(serviceName);

        if (service == null) return NotFound(new { message = $"Service '{serviceName}' not found" });

        return Ok(service);
    }

    [HttpDelete("{serviceName}")]
    public async Task<IActionResult> DeleteService(string serviceName, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();
        var coreUrl = _configuration["CoreService:Url"] ??
                      throw new InvalidOperationException("CoreService:Url configuration is missing");

        var response = await client.DeleteAsync($"{coreUrl}/api/services/{serviceName}", ct);

        if (response.IsSuccessStatusCode) return NoContent();

        var errorContent = await response.Content.ReadAsStringAsync(ct);
        return StatusCode((int)response.StatusCode, errorContent);
    }

    [HttpGet("{serviceName}/dependencies")]
    public async Task<IActionResult> GetServiceDependencies(string serviceName)
    {
        var service = await _registryService.GetServiceAsync(serviceName);
        if (service == null) return NotFound(new { message = $"Service '{serviceName}' not found" });

        var relations = await _relationTypeService.GetForEntityAsync(service.EntityName);

        return Ok(new
        {
            serviceName,
            entityName = service.EntityName,
            relations = relations.Select(r => new
            {
                r.Id,
                r.Entity1,
                r.Entity2
            })
        });
    }
}
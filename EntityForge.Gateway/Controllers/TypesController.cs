using Microsoft.AspNetCore.Mvc;

namespace EntityForge.Gateway.Controllers;

[ApiController]
[Route("api/gateway/types")]
public class TypesController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TypesController> _logger;
    private readonly string _coreUrl;

    public TypesController(IHttpClientFactory httpClientFactory, IConfiguration configuration,
        ILogger<TypesController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _coreUrl = configuration["CoreService:Url"] ??
                   throw new InvalidOperationException("CoreService:Url configuration is missing");
    }

    [HttpGet]
    public async Task<IActionResult> GetAllTypes(CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();
        var response = await client.GetAsync($"{_coreUrl}/api/types", ct);

        if (!response.IsSuccessStatusCode)
            return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync(ct));

        return Content(await response.Content.ReadAsStringAsync(ct), "application/json");
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetTypeById(string id, CancellationToken ct)
    {
        _logger.LogInformation("Getting type {Id} from core", id);

        var client = _httpClientFactory.CreateClient();
        var response = await client.GetAsync($"{_coreUrl}/api/types/{id}", ct);

        if (!response.IsSuccessStatusCode)
            return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync(ct));

        return Content(await response.Content.ReadAsStringAsync(ct), "application/json");
    }
}
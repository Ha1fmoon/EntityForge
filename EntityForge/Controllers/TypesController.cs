using EntityForge.Models;
using EntityForge.Services;
using Microsoft.AspNetCore.Mvc;

namespace EntityForge.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TypesController : ControllerBase
{
    private readonly ILogger<TypesController> _logger;
    private readonly TypeService _typeService;

    public TypesController(TypeService typeService, ILogger<TypesController> logger)
    {
        _typeService = typeService;
        _logger = logger;
    }

    [HttpGet]
    public ActionResult<IEnumerable<TypeDefinition>> GetAllTypes()
    {
        try
        {
            var types = _typeService.GetAllTypes();
            return Ok(types);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving types");
            return StatusCode(500, "Error retrieving types");
        }
    }

    [HttpGet("{id}")]
    public ActionResult<TypeDefinition> GetTypeById(string id)
    {
        try
        {
            var type = _typeService.FindTypeById(id);
            if (type == null)
                return NotFound($"Type with ID '{id}' not found");

            return Ok(type);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving type with ID {TypeId}", id);
            return StatusCode(500, $"Error retrieving type with ID {id}");
        }
    }
}
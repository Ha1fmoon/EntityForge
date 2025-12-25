using EntityForge.Models;
using EntityForge.Shared.Models;

namespace EntityForge.Core.Interfaces.Generation;

public class GenerationContext
{
    public EntityConfig Entity { get; init; } = null!;
    public ServiceInfo ServiceInfo { get; set; } = null!;
}
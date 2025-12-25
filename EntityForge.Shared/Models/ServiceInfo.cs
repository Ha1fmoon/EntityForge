namespace EntityForge.Shared.Models;

public class ServiceInfo
{
    public string Name { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public int Version { get; set; }

    public int AppPort { get; set; }
    public int DbPort { get; set; }
    public string Url { get; set; } = string.Empty;

    public string ImageTag { get; set; } = string.Empty;

    public ServiceStatus Status { get; set; }
    public bool IsHealthy { get; set; } = true;
    public DateTime LastHealthCheck { get; set; }

    public DateTime CreatedAt { get; set; }

    public ServicePaths? Paths { get; set; }
}
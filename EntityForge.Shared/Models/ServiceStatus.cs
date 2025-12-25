namespace EntityForge.Shared.Models;

public enum ServiceStatus
{
    Initializing,
    StructureCreated,
    Building,
    DockerBuilding,
    Starting,
    Running,
    BuildFailed,
    StartFailed,
    TestFailed,
    Error
}
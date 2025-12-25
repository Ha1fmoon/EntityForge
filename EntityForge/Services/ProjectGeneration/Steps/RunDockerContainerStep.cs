using EntityForge.Core.Interfaces.Generation;
using EntityForge.Shared.Models;

namespace EntityForge.Services.ProjectGeneration.Steps;

public class RunDockerContainerStep : GenerationStepBase
{
    private readonly DockerService _docker;
    private readonly ServiceRegistry _registry;

    public RunDockerContainerStep(DockerService docker, ServiceRegistry registry,
        ILogger<RunDockerContainerStep> logger) : base(logger)
    {
        _docker = docker;
        _registry = registry;
    }

    public override string Name => "Run Docker Container";
    public override int Order => 9;

    protected override async Task<bool> ExecuteInternalAsync(GenerationContext context, CancellationToken ct)
    {
        if (context.ServiceInfo.Paths == null || string.IsNullOrEmpty(context.ServiceInfo.ImageTag))
        {
            Logger.LogError("Paths not initialized or no ImageTag");
            return false;
        }

        Logger.LogInformation("Starting Docker container for {ServiceName} (App: {AppPort}, DB: {DbPort})",
            context.Entity.ServiceName, context.ServiceInfo.AppPort, context.ServiceInfo.DbPort);

        context.ServiceInfo.Status = ServiceStatus.Starting;
        _registry.AddOrUpdate(context.ServiceInfo);

        var composePath = Path.Combine(context.ServiceInfo.Paths.Root, "docker-compose.yml");

        if (!File.Exists(composePath))
        {
            Logger.LogError("docker-compose.yml not found: {Path}", composePath);
            context.ServiceInfo.Status = ServiceStatus.StartFailed;
            _registry.AddOrUpdate(context.ServiceInfo);
            return false;
        }

        var runResult = await _docker.ComposeUpAsync(
            context.ServiceInfo.Paths.Root,
            context.Entity.ServiceName,
            context.ServiceInfo.AppPort,
            context.ServiceInfo.DbPort,
            ct);

        if (!runResult.Ok)
        {
            Logger.LogError("Container start failed: {Error}", runResult.Error);
            context.ServiceInfo.Status = ServiceStatus.StartFailed;
            _registry.AddOrUpdate(context.ServiceInfo);
            return false;
        }

        Logger.LogInformation("Docker containers for {ServiceName} started", context.Entity.ServiceName);
        return true;
    }

    public override async Task RollbackAsync(GenerationContext context)
    {
        try
        {
            var composePath = context.ServiceInfo.Paths?.Root;
            if (string.IsNullOrWhiteSpace(composePath))
            {
                Logger.LogDebug("No path for RunDockerContainerStep rollback");
                return;
            }

            Logger.LogInformation("Stopping and removing containers for {ServiceName}", context.ServiceInfo.Name);
            await _docker.ComposeDownAsync(composePath, CancellationToken.None);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "RunDockerContainerStep rollback error");
        }
    }
}
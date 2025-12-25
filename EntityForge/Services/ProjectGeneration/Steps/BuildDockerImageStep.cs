using EntityForge.Core.Interfaces.Generation;
using EntityForge.Shared.Models;

namespace EntityForge.Services.ProjectGeneration.Steps;

public class BuildDockerImageStep : GenerationStepBase
{
    private readonly DockerService _docker;
    private readonly ServiceRegistry _registry;

    public BuildDockerImageStep(DockerService docker, ServiceRegistry registry, ILogger<BuildDockerImageStep> logger) :
        base(logger)
    {
        _docker = docker;
        _registry = registry;
    }

    public override string Name => "Build Docker Image";
    public override int Order => 8;

    protected override async Task<bool> ExecuteInternalAsync(GenerationContext context, CancellationToken ct)
    {
        if (context.ServiceInfo.Paths == null)
        {
            Logger.LogError("Paths not initialized");
            return false;
        }

        Logger.LogInformation("Building Docker image for {ServiceName}", context.Entity.ServiceName);

        context.ServiceInfo.Status = ServiceStatus.DockerBuilding;
        _registry.AddOrUpdate(context.ServiceInfo);

        var serviceRoot = context.ServiceInfo.Paths.Root;
        var apiProjectFolder = Path.GetFileName(context.ServiceInfo.Paths.Api);

        var buildResult =
            await _docker.BuildImageAsync(serviceRoot, apiProjectFolder, context.ServiceInfo.ImageTag, ct);

        if (!buildResult.Ok)
        {
            var errorMsg = buildResult.Error ?? buildResult.Output;
            Logger.LogError("Docker image build failed: {Error}", errorMsg);
            context.ServiceInfo.Status = ServiceStatus.BuildFailed;
            _registry.AddOrUpdate(context.ServiceInfo);
            return false;
        }

        Logger.LogInformation("Docker image {ImageTag} built successfully", context.ServiceInfo.ImageTag);
        return true;
    }

    public override async Task RollbackAsync(GenerationContext context)
    {
        try
        {
            var imageTag = context.ServiceInfo.ImageTag;
            if (string.IsNullOrEmpty(imageTag))
            {
                Logger.LogDebug("No ImageTag for BuildDockerImageStep rollback");
                return;
            }

            Logger.LogInformation("Removing Docker image: {ImageTag}", imageTag);

            await _docker.RemoveImageAsync(imageTag, CancellationToken.None);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "BuildDockerImageStep rollback failed");
        }
    }
}
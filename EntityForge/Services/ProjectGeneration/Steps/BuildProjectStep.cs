using EntityForge.Core.Interfaces.Generation;
using EntityForge.Shared.Models;

namespace EntityForge.Services.ProjectGeneration.Steps;

public class BuildProjectStep : GenerationStepBase
{
    private readonly DotnetCliService _dotnet;
    private readonly ServiceRegistry _registry;

    public BuildProjectStep(DotnetCliService dotnet, ServiceRegistry registry, ILogger<BuildProjectStep> logger) :
        base(logger)
    {
        _dotnet = dotnet;
        _registry = registry;
    }

    public override string Name => "Build Project";
    public override int Order => 7;

    protected override async Task<bool> ExecuteInternalAsync(GenerationContext context, CancellationToken ct)
    {
        if (context.ServiceInfo.Paths == null)
        {
            Logger.LogError("Paths not initialized");
            return false;
        }

        Logger.LogInformation("Building project for {ServiceName}", context.Entity.ServiceName);

        context.ServiceInfo.Status = ServiceStatus.Building;
        _registry.AddOrUpdate(context.ServiceInfo);

        var solutionPath = context.ServiceInfo.Paths.Root;
        var buildResult = await _dotnet.BuildSolutionAsync(solutionPath, ct);

        if (!buildResult.Ok)
        {
            Logger.LogError("Project build failed:");
            Logger.LogError("STDOUT: {Output}", buildResult.Stdout);
            Logger.LogError("STDERR: {Error}", buildResult.Stderr);
            Logger.LogError("Exit Code: {Code}", buildResult.ExitCode);
            context.ServiceInfo.Status = ServiceStatus.BuildFailed;
            _registry.AddOrUpdate(context.ServiceInfo);
            return false;
        }

        Logger.LogInformation("Project {ServiceName} built successfully", context.Entity.ServiceName);
        return true;
    }
}
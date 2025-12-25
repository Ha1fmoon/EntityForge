using EntityForge.Core.Interfaces.Generation;
using EntityForge.Shared.Models;

namespace EntityForge.Services.ProjectGeneration.Steps;

public class FinalizeStep : GenerationStepBase
{
    private readonly ServiceRegistry _registry;

    public FinalizeStep(ServiceRegistry registry, ILogger<FinalizeStep> logger) : base(logger)
    {
        _registry = registry;
    }

    public override string Name => "Finalize";
    public override int Order => 12;

    protected override Task<bool> ExecuteInternalAsync(GenerationContext context, CancellationToken ct)
    {
        Logger.LogInformation("Finalizing generation for {ServiceName}", context.Entity.ServiceName);

        context.ServiceInfo.Status = ServiceStatus.Running;
        _registry.AddOrUpdate(context.ServiceInfo);

        _registry.ReleasePorts(context.ServiceInfo.AppPort, context.ServiceInfo.DbPort);

        Logger.LogInformation("Service {ServiceName} created and running", context.Entity.ServiceName);
        Logger.LogInformation("Available at: http://localhost:{Port}", context.ServiceInfo.AppPort);

        return Task.FromResult(true);
    }
}
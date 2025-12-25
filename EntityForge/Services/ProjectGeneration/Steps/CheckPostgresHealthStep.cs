using EntityForge.Core.Interfaces.Generation;
using EntityForge.Shared.Models;

namespace EntityForge.Services.ProjectGeneration.Steps;

public class CheckPostgresHealthStep : GenerationStepBase
{
    private readonly DockerService _docker;
    private readonly ServiceRegistry _registry;

    private const int BaseDelayMs = 1000;
    private const int MaxRetries = 5;

    public CheckPostgresHealthStep(DockerService docker, ServiceRegistry registry,
        ILogger<CheckPostgresHealthStep> logger) : base(logger)
    {
        _docker = docker;
        _registry = registry;
    }

    public override string Name => "Check PostgreSQL Health";
    public override int Order => 10;

    protected override async Task<bool> ExecuteInternalAsync(GenerationContext context, CancellationToken ct)
    {
        if (context.ServiceInfo.Paths == null)
        {
            Logger.LogError("Paths not initialized");
            return false;
        }

        Logger.LogInformation("Checking PostgreSQL health for {ServiceName}", context.Entity.ServiceName);

        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            if (ct.IsCancellationRequested)
            {
                Logger.LogWarning("PostgreSQL health check cancelled");
                return false;
            }

            Logger.LogInformation("Attempt: {Attempt}/{Max}, checking PostgreSQL...", attempt + 1, MaxRetries);

            var healthResult = await _docker.CheckPostgresHealthAsync(
                context.ServiceInfo.Paths.Root,
                context.Entity.ServiceName,
                ct);

            if (healthResult.Ok)
            {
                Logger.LogInformation("PostgreSQL ready for {ServiceName}", context.Entity.ServiceName);
                return true;
            }

            if (attempt < MaxRetries - 1)
            {
                var delayMs = BaseDelayMs * (1 << attempt);
                Logger.LogWarning(
                    "PostgreSQL not ready (attempt {Attempt}/{Max}). Retry in {Delay}ms. Error: {Error}",
                    attempt + 1,
                    MaxRetries,
                    delayMs,
                    healthResult.Error);

                await Task.Delay(delayMs, ct);
            }
            else
            {
                Logger.LogError(
                    "PostgreSQL not ready after {Max} attempts. Last error: {Error}",
                    MaxRetries,
                    healthResult.Error);
            }
        }

        context.ServiceInfo.Status = ServiceStatus.StartFailed;
        _registry.AddOrUpdate(context.ServiceInfo);

        return false;
    }
}
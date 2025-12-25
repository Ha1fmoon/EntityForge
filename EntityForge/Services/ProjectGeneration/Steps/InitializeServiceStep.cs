using EntityForge.Core.Interfaces.Generation;
using EntityForge.Shared.Models;

namespace EntityForge.Services.ProjectGeneration.Steps;

public class InitializeServiceStep : GenerationStepBase
{
    private readonly ServiceRegistry _registry;

    private const int AppPortStart = 5100;
    private const int AppPortEnd = 5900;
    private const int DbPortStart = 5432;
    private const int DbPortEnd = 6432;

    public InitializeServiceStep(ServiceRegistry registry, ILogger<InitializeServiceStep> logger) : base(logger)
    {
        _registry = registry;
    }

    public override string Name => "Initialize service";
    public override int Order => 1;

    protected override Task<bool> ExecuteInternalAsync(GenerationContext context, CancellationToken ct)
    {
        var serviceName = context.Entity.ServiceName;
        var serviceRootPath = Path.Combine(AppContext.BaseDirectory, "Generated", serviceName);

        Logger.LogInformation("Initializing service {ServiceName}", serviceName);

        var version = _registry.GetNextVersion(serviceName);
        var usedPorts = _registry.GetUsedPorts();
        var appPort = DockerService.FindAvailablePort(AppPortStart, AppPortEnd, usedPorts);
        var dbPort = DockerService.FindAvailablePort(DbPortStart, DbPortEnd, usedPorts);
        var imageTag = $"{serviceName.ToLowerInvariant()}:{version}";

        _registry.ReservePorts(appPort, dbPort);

        context.ServiceInfo = new ServiceInfo
        {
            Name = serviceName,
            Version = version,
            EntityName = context.Entity.Name,
            Status = ServiceStatus.Initializing,
            AppPort = appPort,
            DbPort = dbPort,
            Url = $"http://localhost:{appPort}",
            ImageTag = imageTag,
            CreatedAt = DateTime.UtcNow,
            Paths = new ServicePaths
            {
                Root = serviceRootPath
            }
        };

        return Task.FromResult(true);
    }

    public override Task RollbackAsync(GenerationContext context)
    {
        try
        {
            if (context.ServiceInfo.AppPort > 0 && context.ServiceInfo.DbPort > 0)
            {
                _registry.ReleasePorts(context.ServiceInfo.AppPort, context.ServiceInfo.DbPort);
                Logger.LogInformation("Released ports {AppPort} and {DbPort}",
                    context.ServiceInfo.AppPort, context.ServiceInfo.DbPort);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Rollback error in InitializeServiceStep");
        }

        return Task.CompletedTask;
    }
}
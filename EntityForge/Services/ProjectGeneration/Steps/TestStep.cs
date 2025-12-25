using EntityForge.Core.Interfaces.Generation;
using EntityForge.Shared.Models;

namespace EntityForge.Services.ProjectGeneration.Steps;

public class TestStep : GenerationStepBase
{
    private readonly TestService _test;
    private readonly ServiceRegistry _registry;

    public TestStep(TestService test, ServiceRegistry registry, ILogger<TestStep> logger) : base(logger)
    {
        _test = test;
        _registry = registry;
    }

    public override string Name => "Test step";
    public override int Order => 11;

    protected override async Task<bool> ExecuteInternalAsync(GenerationContext context, CancellationToken ct)
    {
        Logger.LogInformation("Running crud tests for {ServiceName}", context.Entity.ServiceName);

        await Task.Delay(5000, ct);

        var healthCheckPassed = await _test.CheckHealthAsync(context.ServiceInfo.Url + "/health", 10);

        if (!healthCheckPassed)
        {
            Logger.LogError("Health check failed for {ServiceName}", context.Entity.ServiceName);
            context.ServiceInfo.Status = ServiceStatus.TestFailed;
            _registry.AddOrUpdate(context.ServiceInfo);
            return false;
        }

        Logger.LogInformation("Health check passed for {ServiceName}", context.Entity.ServiceName);
        return true;
    }
}
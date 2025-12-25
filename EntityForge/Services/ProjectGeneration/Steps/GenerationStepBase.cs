using EntityForge.Core.Interfaces.Generation;

namespace EntityForge.Services.ProjectGeneration.Steps;

public abstract class GenerationStepBase : IGenerationStep
{
    protected readonly ILogger Logger;

    protected GenerationStepBase(ILogger logger)
    {
        Logger = logger;
    }

    public abstract string Name { get; }
    public abstract int Order { get; }

    public async Task<bool> ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        try
        {
            return await ExecuteInternalAsync(context, ct);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Exception in step {StepName}", Name);
            return false;
        }
    }

    protected abstract Task<bool> ExecuteInternalAsync(GenerationContext context, CancellationToken ct);

    public virtual Task RollbackAsync(GenerationContext context)
    {
        return Task.CompletedTask;
    }
}
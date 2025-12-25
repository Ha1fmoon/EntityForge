using System.Diagnostics;
using EntityForge.Core.Interfaces.Generation;
using EntityForge.Models;
using EntityForge.Shared.Models;

namespace EntityForge.Services.ProjectGeneration;

public class GenerationService
{
    private readonly IEnumerable<IGenerationStep> _steps;
    private readonly ILogger<GenerationService> _logger;

    private readonly SemaphoreSlim _generationSemaphore = new(3, 3);

    public GenerationService(IEnumerable<IGenerationStep> steps, ILogger<GenerationService> logger)
    {
        _steps = steps.OrderBy(s => s.Order).ToList();
        _logger = logger;
    }

    public async Task<ServiceInfo> ExecuteAsync(EntityConfig entityConfig, CancellationToken ct = default)
    {
        _logger.LogDebug("Waiting for generation slot. Current generations count: {Count}",
            _generationSemaphore.CurrentCount);

        await _generationSemaphore.WaitAsync(ct);

        try
        {
            _logger.LogInformation("Taking generation slot. Starting generation for {ServiceName}",
                entityConfig.ServiceName);

            var context = new GenerationContext
            {
                Entity = entityConfig,
                ServiceInfo = new ServiceInfo
                {
                    Name = entityConfig.ServiceName,
                    EntityName = entityConfig.Name,
                    Status = ServiceStatus.Initializing
                }
            };

            var executedSteps = new List<IGenerationStep>();
            var stopwatch = Stopwatch.StartNew();

            _logger.LogInformation("Start service generation: {ServiceName} | Steps: {Count}",
                entityConfig.ServiceName, _steps.Count());

            try
            {
                foreach (var step in _steps)
                {
                    ct.ThrowIfCancellationRequested();

                    _logger.LogInformation("Step {Order}: {StepName}", step.Order, step.Name);
                    var stepStopwatch = Stopwatch.StartNew();

                    var success = await step.ExecuteAsync(context, ct);

                    stepStopwatch.Stop();

                    if (!success)
                    {
                        _logger.LogError("Error in step {StepName} | Elapsed time: {Duration:F2}s",
                            step.Name, stepStopwatch.Elapsed.TotalSeconds);

                        await RollbackAsync(context, executedSteps, step);
                        return context.ServiceInfo;
                    }

                    _logger.LogInformation("Successfully execute {StepName} | Elapsed time: {Duration:F2}s",
                        step.Name, stepStopwatch.Elapsed.TotalSeconds);

                    executedSteps.Add(step);
                }

                stopwatch.Stop();
                _logger.LogInformation("Generation completed in {Duration:F2}s", stopwatch.Elapsed.TotalSeconds);

                return context.ServiceInfo;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Generation was canceled bu user");
                await RollbackAsync(context, executedSteps);

                context.ServiceInfo.Status = ServiceStatus.Error;
                return context.ServiceInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Generation failed with exception");
                await RollbackAsync(context, executedSteps);

                context.ServiceInfo.Status = ServiceStatus.Error;
                return context.ServiceInfo;
            }
        }
        finally
        {
            _generationSemaphore.Release();
            _logger.LogDebug("Generation slot released. Current generations count: {Count}",
                _generationSemaphore.CurrentCount);
        }
    }

    private async Task RollbackAsync(GenerationContext context, List<IGenerationStep> executedSteps,
        IGenerationStep? currentFailedStep = null)
    {
        var totalStepsToRollback = executedSteps.Count + (currentFailedStep != null ? 1 : 0);
        _logger.LogWarning("Rollback | Total steps count: {Count}", totalStepsToRollback);

        if (currentFailedStep != null)
            try
            {
                _logger.LogDebug("Rollback current step: {StepName}", currentFailedStep.Name);
                await currentFailedStep.RollbackAsync(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in current step rollback {StepName}", currentFailedStep.Name);
            }

        for (var i = executedSteps.Count - 1; i >= 0; i--)
            try
            {
                _logger.LogDebug("Rollback: {StepName}", executedSteps[i].Name);
                await executedSteps[i].RollbackAsync(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in step rollback: {StepName}", executedSteps[i].Name);
            }

        _logger.LogInformation("Rollback completed successfully");
    }
}
namespace EntityForge.Core.Interfaces.Generation;

public interface IGenerationStep
{
    string Name { get; }
    
    int Order { get; }
    
    Task<bool> ExecuteAsync(GenerationContext context, CancellationToken ct = default);
    
    Task RollbackAsync(GenerationContext context);
}
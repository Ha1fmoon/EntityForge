using EntityForge.Core.Interfaces.Generation;
using EntityForge.Models;
using EntityForge.Services.Templates;

namespace EntityForge.Services.ProjectGeneration.Steps;

public class GenerateDomainStep : GenerationStepBase
{
    private readonly DomainTemplateService _domainTemplateService;

    public GenerateDomainStep(DomainTemplateService domainTemplateService, ILogger<GenerateDomainStep> logger) :
        base(logger)
    {
        _domainTemplateService = domainTemplateService;
    }

    public override string Name => "Generate Domain";
    public override int Order => 3;

    protected override async Task<bool> ExecuteInternalAsync(GenerationContext context, CancellationToken ct)
    {
        if (context.ServiceInfo.Paths == null)
        {
            Logger.LogError("Paths not initialized");
            return false;
        }

        Logger.LogInformation("Generating Domain for {EntityName}", context.Entity.Name);

        try
        {
            var domainPath = context.ServiceInfo.Paths.Domain;

            CreateDomainFolders(domainPath);
            await GenerateAggregatesAsync(context.Entity, domainPath);
            await GenerateValueObjectsAsync(context.Entity, domainPath);
            await GenerateSearchFilterAsync(context.Entity, domainPath);
            await GenerateRepositoriesAsync(context.Entity, domainPath);

            Logger.LogInformation("Domain for {EntityName} created", context.Entity.Name);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error generating Domain for {EntityName}", context.Entity.Name);
            return false;
        }
    }

    private static void CreateDomainFolders(string domainPath)
    {
        var folders = new[] { "Aggregates", "ValueObjects", "Repositories", "Search" };

        foreach (var folder in folders)
        {
            var folderPath = Path.Combine(domainPath, folder);
            Directory.CreateDirectory(folderPath);
        }
    }

    private async Task GenerateAggregatesAsync(EntityConfig entityConfig, string domainPath)
    {
        var code = await _domainTemplateService.GenerateEntityAsync(entityConfig);
        var path = Path.Combine(domainPath, "Aggregates", $"{entityConfig.Name}.g.cs");
        await File.WriteAllTextAsync(path, code);

        var partialPath = Path.Combine(domainPath, "Aggregates", $"{entityConfig.Name}.cs");
        if (!File.Exists(partialPath))
        {
            var partialCode = await _domainTemplateService.GenerateEntityAsync(entityConfig, true);
            await File.WriteAllTextAsync(partialPath, partialCode);
        }
    }

    private async Task GenerateRepositoriesAsync(EntityConfig entityConfig, string domainPath)
    {
        var code = await _domainTemplateService.GenerateRepositoryInterfaceAsync(entityConfig);
        var path = Path.Combine(domainPath, "Repositories", $"I{entityConfig.Name}Repository.cs");
        await File.WriteAllTextAsync(path, code);
    }

    private async Task GenerateValueObjectsAsync(EntityConfig entityConfig, string domainPath)
    {
        var valueObjectFields = entityConfig.Fields.Where(f => f.Type.IsValueObject).ToList();

        foreach (var field in valueObjectFields)
        {
            var code = await _domainTemplateService.GenerateValueObjectAsync(field, entityConfig.ServiceName);
            var path = Path.Combine(domainPath, "ValueObjects", $"{field.Name}VO.cs");
            await File.WriteAllTextAsync(path, code);
        }
    }

    private async Task GenerateSearchFilterAsync(EntityConfig entityConfig, string domainPath)
    {
        if (!entityConfig.Fields.Any(f => f.IsSearchable)) return;

        var code = await _domainTemplateService.GenerateSearchFilterAsync(entityConfig);
        var path = Path.Combine(domainPath, "Search", $"{entityConfig.Name}Filter.cs");
        await File.WriteAllTextAsync(path, code);
    }
}
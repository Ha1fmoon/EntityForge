using EntityForge.Core;
using EntityForge.Core.Interfaces.Generation;
using EntityForge.Models;
using EntityForge.Services.Templates;

namespace EntityForge.Services.ProjectGeneration.Steps;

public class GenerateApplicationStep : GenerationStepBase
{
    private readonly ApplicationTemplateService _applicationTemplateService;

    public GenerateApplicationStep(ApplicationTemplateService applicationTemplateService,
        ILogger<GenerateApplicationStep> logger) : base(logger)
    {
        _applicationTemplateService = applicationTemplateService;
    }

    public override string Name => "Generate Application";
    public override int Order => 4;

    protected override async Task<bool> ExecuteInternalAsync(GenerationContext context, CancellationToken ct)
    {
        if (context.ServiceInfo.Paths == null)
        {
            Logger.LogError("Paths not initialized");
            return false;
        }

        Logger.LogInformation("Generating Application for {EntityName}", context.Entity.Name);

        try
        {
            var applicationPath = context.ServiceInfo.Paths.Application;

            Directory.CreateDirectory(Path.Combine(applicationPath, "DTOs"));
            Directory.CreateDirectory(Path.Combine(applicationPath, "DTOs", "Search"));
            Directory.CreateDirectory(Path.Combine(applicationPath, "UseCases"));
            Directory.CreateDirectory(Path.Combine(applicationPath, "Mappers"));

            await GenerateDtosAsync(context.Entity, applicationPath);
            await GenerateUseCasesAsync(context.Entity, applicationPath);

            Logger.LogInformation("Application for {EntityName} created", context.Entity.Name);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error generating Application for {EntityName}", context.Entity.Name);
            return false;
        }
    }

    private async Task GenerateDtosAsync(EntityConfig entityConfig, string applicationPath)
    {
        var path = Path.Combine(applicationPath, "DTOs");
        var searchPath = Path.Combine(path, "Search");

        var createDto = await _applicationTemplateService.GenerateDtoCreateAsync(entityConfig);
        await File.WriteAllTextAsync(Path.Combine(path, $"Create{entityConfig.Name}Dto.cs"), createDto);

        var updateDto = await _applicationTemplateService.GenerateDtoUpdateAsync(entityConfig);
        await File.WriteAllTextAsync(Path.Combine(path, $"Update{entityConfig.Name}Dto.cs"), updateDto);

        var showDto = await _applicationTemplateService.GenerateDtoShowAsync(entityConfig);
        await File.WriteAllTextAsync(Path.Combine(path, $"Show{entityConfig.Name}Dto.cs"), showDto);

        if (entityConfig.Fields.Any(f => f.IsSearchable))
        {
            var filterDto = await _applicationTemplateService.GenerateDtoSearchFilterAsync(entityConfig);
            await File.WriteAllTextAsync(Path.Combine(searchPath, $"{entityConfig.Name}FilterDto.cs"), filterDto);
        }

        var pagedResultPath = Path.Combine(path, "PagedResultDto.cs");
        if (!File.Exists(pagedResultPath))
        {
            var pagedResultDto = await _applicationTemplateService.GenerateDtoPagedResultAsync(entityConfig);
            await File.WriteAllTextAsync(pagedResultPath, pagedResultDto);
        }

        var mapper = await _applicationTemplateService.GenerateMapperAsync(entityConfig);
        var mapperPath = Path.Combine(applicationPath, "Mappers", $"{entityConfig.Name}Mapper.cs");
        await File.WriteAllTextAsync(mapperPath, mapper);
    }

    private async Task GenerateUseCasesAsync(EntityConfig entityConfig, string applicationPath)
    {
        var path = Path.Combine(applicationPath, "UseCases");
        var plural = NamingHelper.GetPluralName(entityConfig.Name, entityConfig.PluralName);

        var createCode = await _applicationTemplateService.GenerateUseCaseCreateAsync(entityConfig);
        await File.WriteAllTextAsync(Path.Combine(path, $"Create{entityConfig.Name}.g.cs"), createCode);

        var createPartialPath = Path.Combine(path, $"Create{entityConfig.Name}.cs");
        if (!File.Exists(createPartialPath))
        {
            var createPartialCode = await _applicationTemplateService.GenerateUseCaseCreatePartialAsync(entityConfig);
            await File.WriteAllTextAsync(createPartialPath, createPartialCode);
        }

        var updateCode = await _applicationTemplateService.GenerateUseCaseUpdateAsync(entityConfig);
        await File.WriteAllTextAsync(Path.Combine(path, $"Update{entityConfig.Name}.g.cs"), updateCode);

        var updatePartialPath = Path.Combine(path, $"Update{entityConfig.Name}.cs");
        if (!File.Exists(updatePartialPath))
        {
            var updatePartialCode = await _applicationTemplateService.GenerateUseCaseUpdatePartialAsync(entityConfig);
            await File.WriteAllTextAsync(updatePartialPath, updatePartialCode);
        }

        var deleteCode = await _applicationTemplateService.GenerateUseCaseDeleteAsync(entityConfig);
        await File.WriteAllTextAsync(Path.Combine(path, $"Delete{entityConfig.Name}.g.cs"), deleteCode);

        var deletePartialPath = Path.Combine(path, $"Delete{entityConfig.Name}.cs");
        if (!File.Exists(deletePartialPath))
        {
            var deletePartialCode = await _applicationTemplateService.GenerateUseCaseDeletePartialAsync(entityConfig);
            await File.WriteAllTextAsync(deletePartialPath, deletePartialCode);
        }

        var getByIdCode = await _applicationTemplateService.GenerateUseCaseGetByIdAsync(entityConfig);
        await File.WriteAllTextAsync(Path.Combine(path, $"Get{entityConfig.Name}ById.cs"), getByIdCode);

        foreach (var uniqueField in entityConfig.Fields.Where(f => f.IsUnique))
        {
            var getByUniqueCode =
                await _applicationTemplateService.GenerateUseCaseGetByUniqueAsync(entityConfig, uniqueField);
            await File.WriteAllTextAsync(Path.Combine(path, $"Get{entityConfig.Name}By{uniqueField.Name}.cs"),
                getByUniqueCode);
        }

        if (entityConfig.Fields.Any(f => f.IsSearchable))
        {
            var searchCode = await _applicationTemplateService.GenerateUseCaseSearchAsync(entityConfig);
            await File.WriteAllTextAsync(Path.Combine(path, $"Search{plural}.cs"), searchCode);
        }

        var getAllCode = await _applicationTemplateService.GenerateUseCaseGetAllAsync(entityConfig);
        await File.WriteAllTextAsync(Path.Combine(path, $"GetAll{plural}.cs"), getAllCode);
    }
}
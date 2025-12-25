using EntityForge.Core;
using EntityForge.Models;

namespace EntityForge.Services.Templates;

public class ApplicationTemplateService : TemplateServiceBase
{
    public ApplicationTemplateService(ILogger<ApplicationTemplateService> logger, IWebHostEnvironment environment,
        TypeService typeService) : base(logger, environment, typeService)
    {
    }

    public Task<string> GenerateDtoCreateAsync(EntityConfig entityConfig)
    {
        return RenderDtoAsync("CreateDto.cs.liquid", entityConfig);
    }

    public Task<string> GenerateDtoUpdateAsync(EntityConfig entityConfig)
    {
        return RenderDtoAsync("UpdateDto.cs.liquid", entityConfig);
    }

    public Task<string> GenerateDtoShowAsync(EntityConfig entityConfig)
    {
        return RenderDtoAsync("ShowDto.cs.liquid", entityConfig);
    }

    public Task<string> GenerateUseCaseCreateAsync(EntityConfig entityConfig)
    {
        return RenderUseCaseAsync("Create.g.cs.liquid", entityConfig);
    }

    public Task<string> GenerateUseCaseCreatePartialAsync(EntityConfig entityConfig)
    {
        return RenderUseCaseAsync("Create.partial.cs.liquid", entityConfig);
    }

    public Task<string> GenerateUseCaseUpdateAsync(EntityConfig entityConfig)
    {
        return RenderUseCaseAsync("Update.g.cs.liquid", entityConfig);
    }

    public Task<string> GenerateUseCaseUpdatePartialAsync(EntityConfig entityConfig)
    {
        return RenderUseCaseAsync("Update.partial.cs.liquid", entityConfig);
    }

    public Task<string> GenerateUseCaseDeleteAsync(EntityConfig entityConfig)
    {
        return RenderUseCaseAsync("Delete.g.cs.liquid", entityConfig);
    }

    public Task<string> GenerateUseCaseDeletePartialAsync(EntityConfig entityConfig)
    {
        return RenderUseCaseAsync("Delete.partial.cs.liquid", entityConfig);
    }

    public Task<string> GenerateUseCaseGetByIdAsync(EntityConfig entityConfig)
    {
        return RenderUseCaseAsync("GetById.cs.liquid", entityConfig);
    }

    public Task<string> GenerateUseCaseGetByUniqueAsync(EntityConfig entityConfig, FieldConfig uniqueField)
    {
        var path = AppTemplatePath("UseCases", "GetByUnique.cs.liquid");
        var model = BuildModel(entityConfig, uniqueField);
        return RenderAsync(path, model);
    }

    public Task<string> GenerateUseCaseSearchAsync(EntityConfig entityConfig)
    {
        return RenderUseCaseAsync("Search.cs.liquid", entityConfig);
    }

    public Task<string> GenerateUseCaseGetAllAsync(EntityConfig entityConfig)
    {
        return RenderUseCaseAsync("GetAll.cs.liquid", entityConfig);
    }

    public Task<string> GenerateDtoPagedResultAsync(EntityConfig entityConfig)
    {
        return RenderDtoAsync("PagedResultDto.cs.liquid", entityConfig);
    }

    public Task<string> GenerateDtoSearchFilterAsync(EntityConfig entityConfig)
    {
        return RenderDtoAsync(Path.Combine("Search", "FilterDto.cs.liquid"), entityConfig);
    }

    public Task<string> GenerateMapperAsync(EntityConfig entityConfig)
    {
        return RenderMapperAsync("Mapper.cs.liquid", entityConfig);
    }

    private async Task<string> RenderDtoAsync(string templateFile, EntityConfig entityConfig)
    {
        var path = AppTemplatePath("DTOs", templateFile);
        var model = BuildModel(entityConfig);
        return await RenderAsync(path, model);
    }

    private async Task<string> RenderUseCaseAsync(string templateFile, EntityConfig entityConfig)
    {
        var path = AppTemplatePath("UseCases", templateFile);
        var model = BuildModel(entityConfig);
        return await RenderAsync(path, model);
    }

    private async Task<string> RenderMapperAsync(string templateFile, EntityConfig entityConfig)
    {
        var path = AppTemplatePath("Mappers", templateFile);
        var model = BuildModel(entityConfig);
        return await RenderAsync(path, model);
    }

    private object BuildModel(EntityConfig entityConfig, FieldConfig? uniqueField = null)
    {
        var plural = NamingHelper.GetPluralName(entityConfig.Name, entityConfig.PluralName);
        var uniqueFields = entityConfig.Fields.Where(f => f.IsUnique).ToList();
        var searchableFields = entityConfig.Fields.Where(f => f.IsSearchable).ToList();

        return new
        {
            entity_name = entityConfig.Name,
            service_name = entityConfig.ServiceName,
            plural_name = plural,
            id_type = "Guid",
            fields = MapFields(entityConfig.Fields),
            unique_fields = MapFields(uniqueFields),
            searchable_fields = MapFields(searchableFields),
            unique_field = uniqueField is not null ? MapField(uniqueField) : null,
            has_value_objects = entityConfig.Fields.Any(f => f.Type.IsValueObject),
            has_searchable_fields = searchableFields.Count > 0
        };
    }
}
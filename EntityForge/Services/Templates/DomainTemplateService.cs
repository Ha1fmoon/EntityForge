using EntityForge.Core;
using EntityForge.Models;

namespace EntityForge.Services.Templates;

public class DomainTemplateService : TemplateServiceBase
{
    public DomainTemplateService(ILogger<DomainTemplateService> logger, IWebHostEnvironment environment,
        TypeService typeService) : base(logger, environment, typeService)
    {
    }

    public async Task<string> GenerateEntityAsync(EntityConfig entityConfig, bool partial = false)
    {
        var templateName = partial
            ? "AggregateTemplate.partial.cs.liquid"
            : "AggregateTemplate.g.cs.liquid";
        var templatePath = DomainTemplatePath("Aggregates", templateName);
        var model = CreateAggregateModel(entityConfig);
        return await RenderAsync(templatePath, model);
    }

    public async Task<string> GenerateValueObjectAsync(FieldConfig field, string serviceName)
    {
        const string valueObjectTemplate = "ValueObjectTemplate.cs.liquid";
        var templatePath = DomainTemplatePath("ValueObjects", valueObjectTemplate);
        var model = CreateValueObjectModel(field, serviceName);
        return await RenderAsync(templatePath, model);
    }

    public async Task<string> GenerateRepositoryInterfaceAsync(EntityConfig entityConfig)
    {
        const string repositoryTemplate = "RepositoryTemplate.cs.liquid";
        var templatePath = DomainTemplatePath("Repositories", repositoryTemplate);
        var model = CreateRepositoryModel(entityConfig);
        return await RenderAsync(templatePath, model);
    }

    public async Task<string> GenerateSearchFilterAsync(EntityConfig entityConfig)
    {
        const string searchFilterTemplate = "SearchFilterTemplate.cs.liquid";
        var templatePath = DomainTemplatePath("Search", searchFilterTemplate);
        var model = CreateSearchFilterModel(entityConfig);
        return await RenderAsync(templatePath, model);
    }

    private object CreateAggregateModel(EntityConfig entityConfig)
    {
        return new
        {
            entity_name = entityConfig.Name,
            service_name = entityConfig.ServiceName,
            fields = MapFields(entityConfig.Fields),
            has_value_objects = entityConfig.Fields.Any(f => f.Type.IsValueObject)
        };
    }

    private object CreateRepositoryModel(EntityConfig entityConfig)
    {
        var uniqueFields = entityConfig.Fields.Where(f => f.IsUnique).ToList();
        var searchableFields = entityConfig.Fields.Where(f => f.IsSearchable).ToList();

        return new
        {
            entity_name = entityConfig.Name,
            service_name = entityConfig.ServiceName,
            entity_parameter_name = NamingHelper.ToCamelCase(entityConfig.Name),
            unique_fields = MapFields(uniqueFields),
            searchable_fields = MapFields(searchableFields),
            has_value_objects = entityConfig.Fields.Any(f => f.Type.IsValueObject),
            has_searchable_fields = searchableFields.Count > 0
        };
    }

    private object CreateSearchFilterModel(EntityConfig entityConfig)
    {
        var searchableFields = entityConfig.Fields.Where(f => f.IsSearchable).ToList();

        return new
        {
            entity_name = entityConfig.Name,
            service_name = entityConfig.ServiceName,
            fields = MapFields(searchableFields),
            has_value_objects = searchableFields.Any(f => f.Type.IsValueObject)
        };
    }

    private object CreateValueObjectModel(FieldConfig field, string serviceName)
    {
        var typeDefinition = TypeService.FindTypeById(field.Type.Id);

        return new
        {
            type_name = field.Type.Name ?? $"{field.Name}VO",
            base_type = typeDefinition?.BaseType ?? "string",
            validation_pattern = typeDefinition?.ValidationPattern,
            validation_error_message = typeDefinition?.ValidationErrorMessage,
            max_length = typeDefinition?.MaxLength,
            field_name = field.Name,
            service_name = serviceName
        };
    }
}
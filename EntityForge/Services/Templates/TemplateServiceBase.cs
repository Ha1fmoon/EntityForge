using EntityForge.Core;
using EntityForge.Models;
using Scriban;

namespace EntityForge.Services.Templates;

public abstract class TemplateServiceBase
{
    private readonly ILogger _logger;
    private readonly IWebHostEnvironment _environment;

    protected TypeService TypeService { get; }

    protected TemplateServiceBase(ILogger logger, IWebHostEnvironment environment, TypeService typeService)
    {
        _logger = logger;
        _environment = environment;
        TypeService = typeService;
    }

    protected async Task<string> RenderAsync(string fullPath, object model)
    {
        var template = await LoadTemplateAsync(fullPath);
        return await template.RenderAsync(model);
    }

    private async Task<Template> LoadTemplateAsync(string fullPath)
    {
        if (!File.Exists(fullPath))
        {
            _logger.LogError("Template file not found: {TemplatePath}", fullPath);
            throw new FileNotFoundException($"Template not found: {fullPath}");
        }

        var content = await File.ReadAllTextAsync(fullPath);
        var template = Template.Parse(content);

        if (!template.HasErrors) return template;

        var details = string.Join("; ", template.Messages.Select(m => m.Message));
        _logger.LogError("Template parsing error {Path}: {Details}", fullPath, details);
        throw new InvalidOperationException($"Template parsing error {fullPath}: {details}");
    }

    protected string AppTemplatePath(params string[] segments)
    {
        return Path.Combine(new[] { _environment.ContentRootPath, "Templates", "Application" }.Concat(segments)
            .ToArray());
    }

    protected string DomainTemplatePath(params string[] segments)
    {
        return Path.Combine(new[] { _environment.ContentRootPath, "Templates", "Domain" }.Concat(segments).ToArray());
    }

    protected string InfrastructureTemplatePath(params string[] segments)
    {
        return Path.Combine(new[] { _environment.ContentRootPath, "Templates", "Infrastructure" }.Concat(segments)
            .ToArray());
    }

    protected string ApiTemplatePath(params string[] segments)
    {
        return Path.Combine(new[] { _environment.ContentRootPath, "Templates", "API" }.Concat(segments).ToArray());
    }


    private static string GetDomainType(FieldConfig field)
    {
        return field.Type.IsValueObject ? field.Type.Name ?? $"{field.Name}VO" : field.Type.BaseType;
    }

    private static string GetDtoType(FieldConfig field)
    {
        var baseType = field.Type.BaseType;
        return field.IsRequired ? baseType : baseType + "?";
    }

    private string GetSqlType(FieldConfig field)
    {
        var typeDefinition = TypeService.FindTypeById(field.Type.Id);
        return typeDefinition?.DbColumnType ?? "varchar";
    }

    protected object MapField(FieldConfig f)
    {
        var voTypeName = f.Type.IsValueObject ? f.Type.Name ?? f.Type.BaseType : null;
        var filterDtoType = f.Type.IsValueObject ? f.Type.BaseType : f.Type.BaseType + "?";
        var isValueType = f.Type.BaseType is "int" or "decimal" or "bool" or "DateTime" or "Guid";

        return new
        {
            name = f.Name,
            parameter_name = NamingHelper.ToCamelCase(f.Name),
            column_name = NamingHelper.ToSnakeCase(f.Name),
            type_name = GetDomainType(f),
            dto_type = GetDtoType(f),
            filter_dto_type = filterDtoType,
            sql_type = GetSqlType(f),
            base_type = f.Type.BaseType,
            is_required = f.IsRequired,
            is_unique = f.IsUnique,
            is_searchable = f.IsSearchable,
            is_value_object = f.Type.IsValueObject,
            is_value_type = isValueType,
            vo_type_name = voTypeName
        };
    }

    protected List<object> MapFields(IEnumerable<FieldConfig> fields)
    {
        return fields.Select(MapField).ToList();
    }
}
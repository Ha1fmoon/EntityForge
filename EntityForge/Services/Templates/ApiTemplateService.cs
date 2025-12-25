using EntityForge.Core;
using EntityForge.Models;
using EntityForge.Shared.Models;

namespace EntityForge.Services.Templates;

public class ApiTemplateService : TemplateServiceBase
{
    public ApiTemplateService(ILogger<ApiTemplateService> logger, IWebHostEnvironment environment,
        TypeService typeService) : base(logger, environment, typeService)
    {
    }

    public async Task<string> GenerateControllerAsync(EntityConfig entityConfig)
    {
        var templatePath = ApiTemplatePath("Controllers", "ControllerTemplate.cs.liquid");
        var model = CreateControllerModel(entityConfig);
        return await RenderAsync(templatePath, model);
    }

    public async Task<string> GenerateProgramAsync(EntityConfig entityConfig)
    {
        var templatePath = ApiTemplatePath("Program", "ProgramTemplate.cs.liquid");
        var model = CreateProgramModel(entityConfig);
        return await RenderAsync(templatePath, model);
    }

    public async Task<string> GenerateDockerfileAsync(EntityConfig entityConfig)
    {
        var templatePath = ApiTemplatePath("Docker", "DockerfileTemplate.liquid");
        var model = CreateDockerModel(entityConfig);
        return await RenderAsync(templatePath, model);
    }

    public async Task<string> GenerateDockerComposeAsync(EntityConfig entityConfig, ServiceInfo serviceInfo)
    {
        var templatePath = ApiTemplatePath("Docker", "docker-compose.yml.liquid");
        var model = CreateComposeModel(entityConfig, serviceInfo);
        return await RenderAsync(templatePath, model);
    }

    public async Task<string> GenerateAppsettingsAsync(EntityConfig entityConfig)
    {
        var templatePath = ApiTemplatePath("Configuration", "AppsettingsTemplate.json.liquid");
        var model = CreateAppsettingsModel(entityConfig);
        return await RenderAsync(templatePath, model);
    }

    public async Task<string> GenerateMiddlewareAsync(EntityConfig entityConfig)
    {
        var templatePath = ApiTemplatePath("Middleware", "Middleware.cs.liquid");
        var model = new { service_name = entityConfig.ServiceName };
        return await RenderAsync(templatePath, model);
    }

    public async Task<string> GenerateDatabaseInitializerAsync(EntityConfig entityConfig)
    {
        var templatePath = ApiTemplatePath("Initialization", "DatabaseInitializer.cs.liquid");
        var model = new { service_name = entityConfig.ServiceName };
        return await RenderAsync(templatePath, model);
    }

    private object CreateControllerModel(EntityConfig entityConfig)
    {
        var searchableFields = entityConfig.Fields.Where(f => f.IsSearchable).ToList();
        var uniqueFields = entityConfig.Fields.Where(f => f.IsUnique).ToList();
        var plural = NamingHelper.GetPluralName(entityConfig.Name, entityConfig.PluralName);

        return new
        {
            entity_name = entityConfig.Name,
            entity_plural = plural,
            service_name = entityConfig.ServiceName,
            entity_parameter_name = NamingHelper.ToCamelCase(entityConfig.Name),
            entity_plural_parameter = NamingHelper.ToCamelCase(plural),
            route_name = NamingHelper.ToKebabCase(plural),
            fields = MapFields(entityConfig.Fields),
            searchable_fields = MapFields(searchableFields),
            unique_fields = MapFields(uniqueFields)
        };
    }

    private object CreateProgramModel(EntityConfig entityConfig)
    {
        var plural = NamingHelper.GetPluralName(entityConfig.Name, entityConfig.PluralName);

        return new
        {
            entity_name = entityConfig.Name,
            service_name = entityConfig.ServiceName,
            plural_name = plural,
            repository_interface = $"I{entityConfig.Name}Repository",
            repository_implementation = $"{entityConfig.Name}Repository",
            has_search = entityConfig.Fields.Any(f => f.IsSearchable),
            unique_field_names = entityConfig.Fields.Where(f => f.IsUnique).Select(f => f.Name).ToList(),
            db_name = NamingHelper.GetDbName(entityConfig.ServiceName),
            table_name = NamingHelper.ToSnakeCase(plural),
            fields = MapFields(entityConfig.Fields)
        };
    }

    private static object CreateDockerModel(EntityConfig entityConfig)
    {
        return new
        {
            service_name = entityConfig.ServiceName,
            project_name = $"{entityConfig.ServiceName}.Api",
            assembly_name = $"{entityConfig.ServiceName}.Api.dll"
        };
    }

    private static object CreateComposeModel(EntityConfig entityConfig, ServiceInfo serviceInfo)
    {
        var serviceNameLower = entityConfig.ServiceName.ToLowerInvariant();
        return new
        {
            service_name = entityConfig.ServiceName,
            service_name_lower = serviceNameLower,
            db_name = NamingHelper.GetDbName(entityConfig.ServiceName),
            image_tag = serviceInfo.ImageTag,
            app_port = serviceInfo.AppPort,
            db_port = serviceInfo.DbPort
        };
    }

    private static object CreateAppsettingsModel(EntityConfig entityConfig)
    {
        return new
        {
            service_name = entityConfig.ServiceName,
            entity_name = entityConfig.Name,
            db_name = NamingHelper.GetDbName(entityConfig.ServiceName)
        };
    }
}
using EntityForge.Core;
using EntityForge.Models;

namespace EntityForge.Services.Templates;

public class InfrastructureTemplateService : TemplateServiceBase
{
    public InfrastructureTemplateService(ILogger<InfrastructureTemplateService> logger, IWebHostEnvironment environment,
        TypeService typeService) : base(logger, environment, typeService)
    {
    }

    public async Task<string> GenerateConnectionFactoryInterfaceAsync(EntityConfig entityConfig)
    {
        var templatePath = InfrastructureTemplatePath("Data", "IDbConnectionFactory.cs.liquid");
        var model = new { service_name = entityConfig.ServiceName };
        return await RenderAsync(templatePath, model);
    }

    public async Task<string> GenerateNpgsqlConnectionFactoryAsync(EntityConfig entityConfig)
    {
        var templatePath = InfrastructureTemplatePath("Data", "NpgsqlConnectionFactory.cs.liquid");
        var model = new { service_name = entityConfig.ServiceName };
        return await RenderAsync(templatePath, model);
    }

    public async Task<string> GenerateRepositoryAsync(EntityConfig entityConfig)
    {
        var templatePath = InfrastructureTemplatePath("Repositories", "RepositoryImplTemplate.cs.liquid");
        var model = CreateRepositoryModel(entityConfig);
        return await RenderAsync(templatePath, model);
    }

    public async Task<string> GenerateSqlSchemaAsync(EntityConfig entityConfig)
    {
        var templatePath = InfrastructureTemplatePath("Sql", "SchemaTemplate.sql.liquid");
        var model = CreateSqlSchemaModel(entityConfig);
        return await RenderAsync(templatePath, model);
    }

    public async Task<string> GenerateSqlIndexesAsync(EntityConfig entityConfig)
    {
        var templatePath = InfrastructureTemplatePath("Sql", "IndexesTemplate.sql.liquid");
        var model = CreateSqlSchemaModel(entityConfig);
        return await RenderAsync(templatePath, model);
    }

    public async Task<string> GenerateDbInitializerAsync(EntityConfig entityConfig, string schemaSql, string indexesSql)
    {
        var templatePath = InfrastructureTemplatePath("Data", "DbInitializer.cs.liquid");
        var model = new
        {
            service_name = entityConfig.ServiceName,
            schema_sql = EscapeQuotes(schemaSql),
            indexes_sql = EscapeQuotes(indexesSql)
        };
        return await RenderAsync(templatePath, model);
    }

    private static string EscapeQuotes(string s)
    {
        return s.Replace("\"", "\"\"");
    }

    private object CreateRepositoryModel(EntityConfig entityConfig)
    {
        var uniqueFields = entityConfig.Fields.Where(f => f.IsUnique).ToList();
        var searchableFields = entityConfig.Fields.Where(f => f.IsSearchable).ToList();
        var plural = NamingHelper.GetPluralName(entityConfig.Name, entityConfig.PluralName);

        return new
        {
            entity_name = entityConfig.Name,
            service_name = entityConfig.ServiceName,
            table_name = NamingHelper.ToSnakeCase(plural),
            entity_parameter_name = NamingHelper.ToCamelCase(entityConfig.Name),
            unique_fields = MapFields(uniqueFields),
            searchable_fields = MapFields(searchableFields),
            all_fields = MapFields(entityConfig.Fields),
            has_value_objects = entityConfig.Fields.Any(f => f.Type.IsValueObject),
            has_searchable_fields = searchableFields.Count > 0
        };
    }

    private object CreateSqlSchemaModel(EntityConfig entityConfig)
    {
        var plural = NamingHelper.GetPluralName(entityConfig.Name, entityConfig.PluralName);

        return new
        {
            table_name = NamingHelper.ToSnakeCase(plural),
            fields = MapFields(entityConfig.Fields)
        };
    }
}
using EntityForge.Core.Interfaces.Generation;
using EntityForge.Services.Templates;

namespace EntityForge.Services.ProjectGeneration.Steps;

public class GenerateInfrastructureStep : GenerationStepBase
{
    private readonly InfrastructureTemplateService _infrastructureTemplates;
    private readonly DotnetCliService _dotnet;

    public GenerateInfrastructureStep(
        InfrastructureTemplateService infrastructureTemplates,
        DotnetCliService dotnet,
        ILogger<GenerateInfrastructureStep> logger) : base(logger)
    {
        _infrastructureTemplates = infrastructureTemplates;
        _dotnet = dotnet;
    }

    public override string Name => "Generate Infrastructure";
    public override int Order => 5;

    protected override async Task<bool> ExecuteInternalAsync(GenerationContext context, CancellationToken ct)
    {
        if (context.ServiceInfo.Paths == null)
        {
            Logger.LogError("Paths not initialized");
            return false;
        }

        Logger.LogInformation("Generating Infrastructure for {EntityName}", context.Entity.Name);

        var infrastructurePath = context.ServiceInfo.Paths.Infrastructure;
        var dataPath = Path.Combine(infrastructurePath, "Data");
        var repositoriesPath = Path.Combine(infrastructurePath, "Repositories");
        var sqlPath = Path.Combine(infrastructurePath, "Sql");

        try
        {
            Directory.CreateDirectory(infrastructurePath);
            Directory.CreateDirectory(dataPath);
            Directory.CreateDirectory(repositoriesPath);
            Directory.CreateDirectory(sqlPath);

            var iFactoryCode = await _infrastructureTemplates.GenerateConnectionFactoryInterfaceAsync(context.Entity);
            await File.WriteAllTextAsync(Path.Combine(dataPath, "IDbConnectionFactory.cs"), iFactoryCode, ct);

            var npgFactoryCode = await _infrastructureTemplates.GenerateNpgsqlConnectionFactoryAsync(context.Entity);
            await File.WriteAllTextAsync(Path.Combine(dataPath, "NpgsqlConnectionFactory.cs"), npgFactoryCode, ct);

            var repositoryCode = await _infrastructureTemplates.GenerateRepositoryAsync(context.Entity);
            await File.WriteAllTextAsync(Path.Combine(repositoriesPath, $"{context.Entity.Name}Repository.cs"),
                repositoryCode, ct);

            var schemaSql = await _infrastructureTemplates.GenerateSqlSchemaAsync(context.Entity);
            await File.WriteAllTextAsync(Path.Combine(sqlPath, $"CreateTable_{context.Entity.Name}.sql"), schemaSql,
                ct);

            var indexesSql = await _infrastructureTemplates.GenerateSqlIndexesAsync(context.Entity);
            await File.WriteAllTextAsync(Path.Combine(sqlPath, $"CreateIndexes_{context.Entity.Name}.sql"), indexesSql,
                ct);

            var dbInitializerCode =
                await _infrastructureTemplates.GenerateDbInitializerAsync(context.Entity, schemaSql, indexesSql);
            await File.WriteAllTextAsync(Path.Combine(dataPath, "DbInitializer.cs"), dbInitializerCode, ct);

            await AddPackagesAsync(infrastructurePath);

            Logger.LogInformation("Infrastructure for {EntityName} created", context.Entity.Name);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error generating Infrastructure for {EntityName}", context.Entity.Name);
            return false;
        }
    }

    private async Task AddPackagesAsync(string infrastructurePath)
    {
        if (Directory.GetFiles(infrastructurePath, "*.csproj").Length == 0)
        {
            Logger.LogWarning("No .csproj file found in {Path}", infrastructurePath);
            return;
        }

        Logger.LogInformation("Adding NuGet packages to Infrastructure");
        await _dotnet.AddPackagesAsync(new Dictionary<string, string?>
        {
            ["Dapper"] = null,
            ["Npgsql"] = null,
            ["Polly"] = null
        }, infrastructurePath);
    }
}
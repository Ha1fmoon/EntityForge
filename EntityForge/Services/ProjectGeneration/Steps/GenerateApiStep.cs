using EntityForge.Core;
using EntityForge.Core.Interfaces.Generation;
using EntityForge.Models;
using EntityForge.Services.Templates;
using EntityForge.Shared.Models;

namespace EntityForge.Services.ProjectGeneration.Steps;

public class GenerateApiStep : GenerationStepBase
{
    private readonly ApiTemplateService _apiTemplateService;
    private readonly DotnetCliService _dotnet;

    public GenerateApiStep(ApiTemplateService apiTemplateService, DotnetCliService dotnet,
        ILogger<GenerateApiStep> logger) : base(logger)
    {
        _apiTemplateService = apiTemplateService;
        _dotnet = dotnet;
    }

    public override string Name => "Generate API";
    public override int Order => 6;

    protected override async Task<bool> ExecuteInternalAsync(GenerationContext context, CancellationToken ct)
    {
        if (context.ServiceInfo.Paths == null)
        {
            Logger.LogError("Paths not initialized");
            return false;
        }

        Logger.LogInformation("Generating API for {EntityName}", context.Entity.Name);

        try
        {
            var apiPath = context.ServiceInfo.Paths.Api;

            Directory.CreateDirectory(Path.Combine(apiPath, "Controllers"));
            Directory.CreateDirectory(Path.Combine(apiPath, "Middleware"));
            Directory.CreateDirectory(Path.Combine(apiPath, "Initialization"));

            await GenerateControllersAsync(context.Entity, apiPath);
            await GenerateMiddlewareAsync(context.Entity, apiPath);
            await GenerateDatabaseInitializerAsync(context.Entity, apiPath);
            await GenerateProgramAsync(context.Entity, apiPath);
            await GenerateDockerfileAsync(context.Entity, apiPath);
            await GenerateAppsettingsAsync(context.Entity, apiPath);
            await GenerateDockerComposeAsync(context.Entity, context.ServiceInfo);

            Logger.LogInformation("Adding NuGet packages to API");
            await _dotnet.AddPackagesAsync(new Dictionary<string, string?>
            {
                ["Npgsql"] = null,
                ["Polly"] = null
            }, apiPath);

            Logger.LogInformation("API for {EntityName} created", context.Entity.Name);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "API generation failed for {EntityName}", context.Entity.Name);
            return false;
        }
    }

    private async Task GenerateControllersAsync(EntityConfig entityConfig, string apiPath)
    {
        var code = await _apiTemplateService.GenerateControllerAsync(entityConfig);
        var plural = NamingHelper.GetPluralName(entityConfig.Name, entityConfig.PluralName);
        var path = Path.Combine(apiPath, "Controllers", $"{plural}Controller.cs");
        await File.WriteAllTextAsync(path, code);
    }

    private async Task GenerateMiddlewareAsync(EntityConfig entityConfig, string apiPath)
    {
        var code = await _apiTemplateService.GenerateMiddlewareAsync(entityConfig);
        var path = Path.Combine(apiPath, "Middleware", "CorrelationIdMiddleware.cs");
        await File.WriteAllTextAsync(path, code);
    }

    private async Task GenerateDatabaseInitializerAsync(EntityConfig entityConfig, string apiPath)
    {
        var code = await _apiTemplateService.GenerateDatabaseInitializerAsync(entityConfig);
        var path = Path.Combine(apiPath, "Initialization", "DatabaseInitializer.cs");
        await File.WriteAllTextAsync(path, code);
    }

    private async Task GenerateProgramAsync(EntityConfig entityConfig, string apiPath)
    {
        var code = await _apiTemplateService.GenerateProgramAsync(entityConfig);
        var path = Path.Combine(apiPath, "Program.cs");
        await File.WriteAllTextAsync(path, code);
    }

    private async Task GenerateDockerfileAsync(EntityConfig entityConfig, string apiPath)
    {
        var code = await _apiTemplateService.GenerateDockerfileAsync(entityConfig);
        var path = Path.Combine(apiPath, "Dockerfile");
        await File.WriteAllTextAsync(path, code);
    }

    private async Task GenerateAppsettingsAsync(EntityConfig entityConfig, string apiPath)
    {
        var code = await _apiTemplateService.GenerateAppsettingsAsync(entityConfig);
        var path = Path.Combine(apiPath, "appsettings.json");
        await File.WriteAllTextAsync(path, code);
    }

    private async Task GenerateDockerComposeAsync(EntityConfig entityConfig, ServiceInfo serviceInfo)
    {
        var code = await _apiTemplateService.GenerateDockerComposeAsync(entityConfig, serviceInfo);
        var path = Path.Combine(serviceInfo.Paths!.Root, "docker-compose.yml");
        await File.WriteAllTextAsync(path, code);
    }
}
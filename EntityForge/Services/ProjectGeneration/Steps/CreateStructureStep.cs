using EntityForge.Core.Interfaces.Generation;
using EntityForge.Shared.Models;

namespace EntityForge.Services.ProjectGeneration.Steps;

public class CreateStructureStep : GenerationStepBase
{
    private readonly DotnetCliService _cliService;

    private const string SerilogVersion = "8.0.3";

    public CreateStructureStep(DotnetCliService cliService, ILogger<CreateStructureStep> logger) : base(logger)
    {
        _cliService = cliService;
    }

    public override string Name => "Create structure";
    public override int Order => 2;

    protected override async Task<bool> ExecuteInternalAsync(GenerationContext context, CancellationToken ct)
    {
        var serviceName = context.Entity.ServiceName;
        var serviceRootPath = context.ServiceInfo.Paths!.Root;

        Logger.LogInformation("Creating structure in {Path}", serviceRootPath);

        try
        {
            Directory.CreateDirectory(serviceRootPath);

            if (!await _cliService.CreateSolutionAsync(serviceName, serviceRootPath))
            {
                Logger.LogError("Failed to create solution");
                return false;
            }

            var projectPaths = await CreateProjectsAsync(serviceRootPath, serviceName);
            await AddProjectsToSolutionAsync(serviceRootPath, projectPaths);
            await AddProjectReferencesAsync(projectPaths);
            DeleteDefaultClassFiles(projectPaths);

            context.ServiceInfo.Status = ServiceStatus.StructureCreated;

            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error creating project structure for service {ServiceName}", serviceName);
            context.ServiceInfo.Status = ServiceStatus.Error;
            return false;
        }
    }

    private async Task<Dictionary<string, string>> CreateProjectsAsync(string serviceRootPath, string serviceName)
    {
        var projects = new Dictionary<string, string>();

        foreach (var layer in new[] { "Domain", "Application", "Infrastructure" })
        {
            var projectName = $"{serviceName}.{layer}";
            if (!await _cliService.CreateClassLibraryAsync(projectName, serviceRootPath))
                throw new InvalidOperationException($"Failed to create project {projectName}");
            projects[layer] = Path.Combine(serviceRootPath, projectName);
        }

        var apiProjectName = $"{serviceName}.Api";
        if (!await _cliService.CreateWebApiAsync(apiProjectName, serviceRootPath))
            throw new InvalidOperationException($"Failed to create project {apiProjectName}");
        projects["Api"] = Path.Combine(serviceRootPath, apiProjectName);

        await _cliService.AddPackageAsync("Serilog.AspNetCore", projects["Api"], SerilogVersion);

        return projects;
    }

    private async Task AddProjectsToSolutionAsync(string serviceRootPath, Dictionary<string, string> projectPaths)
    {
        foreach (var project in projectPaths.Values)
        {
            var projectFile = Directory.GetFiles(project, "*.csproj").First();
            if (!await _cliService.AddProjectToSolutionAsync(projectFile, serviceRootPath))
                throw new InvalidOperationException($"Failed to add project {projectFile} to solution");
        }
    }

    private async Task AddProjectReferencesAsync(Dictionary<string, string> projectPaths)
    {
        await AddProjectReferenceAsync(projectPaths["Application"], projectPaths["Domain"]);
        await AddProjectReferenceAsync(projectPaths["Infrastructure"], projectPaths["Domain"]);
        await AddProjectReferenceAsync(projectPaths["Infrastructure"], projectPaths["Application"]);
        await AddProjectReferenceAsync(projectPaths["Api"], projectPaths["Application"]);
        await AddProjectReferenceAsync(projectPaths["Api"], projectPaths["Infrastructure"]);
    }

    private async Task AddProjectReferenceAsync(string fromProjectPath, string toProjectPath)
    {
        var toProjectFile = Directory.GetFiles(toProjectPath, "*.csproj").First();
        if (!await _cliService.AddProjectReferenceAsync(toProjectFile, fromProjectPath))
            throw new InvalidOperationException($"Failed to add reference to project {toProjectFile}");
    }

    private static void DeleteDefaultClassFiles(Dictionary<string, string> projectPaths)
    {
        var filesToDelete = new[]
        {
            Path.Combine(projectPaths["Domain"], "Class1.cs"),
            Path.Combine(projectPaths["Application"], "Class1.cs"),
            Path.Combine(projectPaths["Infrastructure"], "Class1.cs"),
            Path.Combine(projectPaths["Api"], "WeatherForecast.cs"),
            Path.Combine(projectPaths["Api"], "Controllers", "WeatherForecastController.cs"),
            Path.Combine(projectPaths["Api"], "Properties", "launchSettings.json")
        };

        foreach (var file in filesToDelete)
            if (File.Exists(file))
                File.Delete(file);
    }

    public override Task RollbackAsync(GenerationContext context)
    {
        try
        {
            var serviceRootPath = context.ServiceInfo.Paths?.Root;
            if (!string.IsNullOrWhiteSpace(serviceRootPath) && Directory.Exists(serviceRootPath))
            {
                Logger.LogInformation("Deleting folder: {Path}", serviceRootPath);
                Directory.Delete(serviceRootPath, true);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Rollback error in CreateStructureStep");
        }

        return Task.CompletedTask;
    }
}
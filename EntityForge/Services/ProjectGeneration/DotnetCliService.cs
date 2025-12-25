using System.Diagnostics;
using System.Text;

namespace EntityForge.Services.ProjectGeneration;

public class DotnetCliService
{
    private readonly ILogger<DotnetCliService> _logger;

    public DotnetCliService(ILogger<DotnetCliService> logger)
    {
        _logger = logger;
    }

    public record CliOutput(bool Ok, string Stdout, string Stderr, int ExitCode);

    private async Task<CliOutput> RunAsync(string arguments, string workingDirectory, CancellationToken ct)
    {
        _logger.LogInformation("dotnet {Arguments}", arguments);
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                }
            };

            process.Start();

            var stdOutTask = process.StandardOutput.ReadToEndAsync(ct);
            var stdErrTask = process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            var stdout = (await stdOutTask).Trim();
            var stderr = (await stdErrTask).Trim();
            var ok = process.ExitCode == 0;
            if (!ok)
                _logger.LogError("dotnet {Arguments} ended with code {Code}", arguments, process.ExitCode);
            return new CliOutput(ok, stdout, stderr, process.ExitCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while executing dotnet {Arguments}", arguments);
            return new CliOutput(false, string.Empty, ex.Message, -1);
        }
    }

    private async Task<bool> ExecuteCommandAsync(string arguments, string workingDirectory)
    {
        var result = await RunAsync(arguments, workingDirectory, CancellationToken.None);
        return result.Ok;
    }


    public async Task<bool> CreateSolutionAsync(string solutionName, string workingDirectory)
    {
        return await ExecuteCommandAsync($"new sln -n {solutionName}", workingDirectory);
    }

    public async Task<bool> CreateClassLibraryAsync(string projectName, string workingDirectory)
    {
        var projectDirectory = Path.Combine(workingDirectory, projectName);
        
        if (Directory.Exists(projectDirectory) && Directory.GetFiles(projectDirectory, "*.csproj").Length != 0)
            return true;
        
        return await ExecuteCommandAsync($"new classlib -n {projectName} -f net8.0", workingDirectory);
    }

    public async Task<bool> CreateWebApiAsync(string projectName, string workingDirectory)
    {
        var projectDirectory = Path.Combine(workingDirectory, projectName);
        
        if (Directory.Exists(projectDirectory) && Directory.GetFiles(projectDirectory, "*.csproj").Length != 0)
            return true;
        
        return await ExecuteCommandAsync($"new webapi -n {projectName} -f net8.0 --no-https", workingDirectory);
    }

    public async Task<bool> AddProjectToSolutionAsync(string projectFilePath, string workingDirectory)
    {
        var sln = Directory.GetFiles(workingDirectory, "*.sln").FirstOrDefault();
        
        if (string.IsNullOrEmpty(sln))
            return await ExecuteCommandAsync($"sln add \"{projectFilePath}\"", workingDirectory);
        
        var slnText = await File.ReadAllTextAsync(sln);
        var projectName = Path.GetFileName(projectFilePath);
        
        if (slnText.Contains(projectName, StringComparison.OrdinalIgnoreCase))
            return true;
        
        return await ExecuteCommandAsync($"sln add \"{projectFilePath}\"", workingDirectory);
    }

    public async Task<bool> AddProjectReferenceAsync(string targetProjectPath, string workingDirectory)
    {
        var csproj = Directory.GetFiles(workingDirectory, "*.csproj").FirstOrDefault();

        if (string.IsNullOrEmpty(csproj))
            return await ExecuteCommandAsync($"add reference \"{targetProjectPath}\"", workingDirectory);
        
        var text = await File.ReadAllTextAsync(csproj);
        var targetFileName = Path.GetFileName(targetProjectPath);
        
        if (text.Contains(targetFileName, StringComparison.OrdinalIgnoreCase))
            return true;
        
        return await ExecuteCommandAsync($"add reference \"{targetProjectPath}\"", workingDirectory);
    }

    public async Task AddPackageAsync(string packageName, string workingDirectory, string? version = null)
    {
        var args = string.IsNullOrEmpty(version) 
            ? $"add package {packageName}" 
            : $"add package {packageName} --version {version}";
        
        await ExecuteCommandAsync(args, workingDirectory);
    }

    public async Task AddPackagesAsync(Dictionary<string, string?> packages, string workingDirectory)
    {
        foreach (var (packageName, version) in packages)
        {
            await AddPackageAsync(packageName, workingDirectory, version);
        }
    }

    public async Task<CliOutput> BuildSolutionAsync(string solutionPath, CancellationToken ct = default)
    {
        _logger.LogInformation("Building solution. Path: {Path}", solutionPath);
        var result = await RunAsync("build --no-restore --configuration Release --verbosity minimal", solutionPath, ct);
        
        if (!result.Ok)
        {
            _logger.LogError("Error in solution build: {Error}", result.Stderr);
        }
        else
        {
            _logger.LogInformation("Solution built successfully.");
        }
        
        return result;
    }
}
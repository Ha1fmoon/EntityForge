using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace EntityForge.Services.ProjectGeneration;

public class DockerService
{
    private readonly ILogger<DockerService> _logger;
    private static readonly TimeSpan DefaultBuildTimeout = TimeSpan.FromMinutes(10);

    public DockerService(ILogger<DockerService> logger)
    {
        _logger = logger;
    }

    public record CmdResult(bool Ok, string Output, string? Error, int ExitCode);

    private async Task<CmdResult> RunAsync(string arguments, string cwd, TimeSpan timeout,
        CancellationToken externalCt = default, IDictionary<string, string>? environment = null)
    {
        _logger.LogInformation("docker {Args}", arguments);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(externalCt);
        cts.CancelAfter(timeout);

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = arguments,
            WorkingDirectory = cwd,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (environment != null)
            foreach (var valuePair in environment)
                process.StartInfo.Environment[valuePair.Key] = valuePair.Value;

        try
        {
            process.Start();
            var stdOutTask = process.StandardOutput.ReadToEndAsync(cts.Token);
            var stdErrTask = process.StandardError.ReadToEndAsync(cts.Token);
            await process.WaitForExitAsync(cts.Token);

            var outStr = (await stdOutTask).Trim();
            var errStr = (await stdErrTask).Trim();

            var ok = process.ExitCode == 0;
            if (ok)
                _logger.LogDebug("Docker ok: {Args}", arguments);
            else
                _logger.LogError("Docker error ({Code}): {Args}\n{Err}", process.ExitCode, arguments, errStr);

            return new CmdResult(ok, outStr, ok ? null : errStr, process.ExitCode);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            var msg = $"Cancelled by user: docker {arguments} ({timeout}).";
            _logger.LogError(msg);
            return new CmdResult(false, string.Empty, msg, -1);
        }
        catch (Exception ex)
        {
            TryKill(process);
            _logger.LogError(ex, "Error: docker {Args}", arguments);
            return new CmdResult(false, string.Empty, ex.Message, -1);
        }
    }

    public async Task<CmdResult> BuildImageAsync(string serviceRoot, string apiProjectName, string tag,
        CancellationToken ct = default)
    {
        var dockerfile = Path.Combine(apiProjectName, "Dockerfile");
        var args = $"build -t {tag} -f \"{dockerfile}\" .";
        return await RunAsync(args, serviceRoot, DefaultBuildTimeout, ct);
    }

    public async Task<CmdResult> ComposeUpAsync(string serviceRoot, string serviceName, int hostPort, int dbPort,
        CancellationToken ct = default)
    {
        var env = new Dictionary<string, string>
        {
            ["HOST_PORT"] = hostPort.ToString(),
            ["DB_PORT"] = dbPort.ToString()
        };
        var up = await RunAsync("compose up -d", serviceRoot, TimeSpan.FromMinutes(3), ct, env);
        if (!up.Ok) return up;

        var serviceNameLower = serviceName.ToLowerInvariant();
        var ps = await RunAsync($"compose ps -q {serviceNameLower}_app", serviceRoot, TimeSpan.FromMinutes(1), ct, env);
        return ps;
    }

    public Task<CmdResult> ComposeDownAsync(string serviceRoot, CancellationToken ct = default)
    {
        return RunAsync("compose down -v", serviceRoot, TimeSpan.FromMinutes(2), ct);
    }

    public Task<CmdResult> RemoveImageAsync(string tag, CancellationToken ct = default)
    {
        return RunAsync($"rmi {tag}", Directory.GetCurrentDirectory(), TimeSpan.FromMinutes(2), ct);
    }

    public async Task<bool> ImageExistsAsync(string imageName, CancellationToken ct = default)
    {
        var result = await RunAsync($"image inspect {imageName}", Directory.GetCurrentDirectory(),
            TimeSpan.FromSeconds(10), ct);
        return result.Ok;
    }

    public async Task<bool> BuildCustomImageAsync(string tag, string dockerfilePath, string buildContext,
        CancellationToken ct = default)
    {
        var args = $"build -t {tag} -f \"{dockerfilePath}\" \"{buildContext}\"";
        var result = await RunAsync(args, buildContext, TimeSpan.FromMinutes(5), ct);
        return result.Ok;
    }

    public async Task<CmdResult> CheckPostgresHealthAsync(string serviceRoot, string serviceName,
        CancellationToken ct = default)
    {
        var serviceNameLower = serviceName.ToLowerInvariant();
        var args = $"compose exec -T {serviceNameLower}_db pg_isready -U postgres";
        return await RunAsync(args, serviceRoot, TimeSpan.FromSeconds(10), ct);
    }

    public static int FindAvailablePort(int start = 5100, int end = 5900, HashSet<int>? usedPorts = null)
    {
        if (usedPorts == null) usedPorts = new HashSet<int>();

        for (var port = start; port <= end; port++)
            if (!usedPorts.Contains(port) && IsPortFree(port))
                return port;
        return start;
    }

    private static bool IsPortFree(int port)
    {
        try
        {
            using var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void TryKill(Process p)
    {
        try
        {
            if (!p.HasExited) p.Kill(true);
        }
        catch
        {
            _logger.LogWarning("Failed to kill process {Pid}", p.Id);
        }
    }
}
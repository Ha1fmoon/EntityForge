using EntityForge.Shared.Models;

namespace EntityForge.Services.ProjectGeneration;

public class CleanupService
{
    private readonly ILogger<CleanupService> _logger;
    private readonly DockerService _docker;

    public CleanupService(ILogger<CleanupService> logger, DockerService docker)
    {
        _logger = logger;
        _docker = docker;
    }

    public async Task<bool> CleanupServiceAsync(ServiceInfo serviceInfo, bool removeImage = true,
        CancellationToken ct = default)
    {
        var serviceRoot = serviceInfo.Paths?.Root;
        var imageName = !string.IsNullOrEmpty(serviceInfo.ImageTag)
            ? serviceInfo.ImageTag
            : null;
        var composePath = !string.IsNullOrWhiteSpace(serviceRoot)
            ? Path.Combine(serviceRoot, "docker-compose.yml")
            : null;

        if (!string.IsNullOrWhiteSpace(composePath) && File.Exists(composePath))
            try
            {
                await _docker.ComposeDownAsync(serviceRoot!, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to stop containers for {ServiceName}", serviceInfo.Name);
            }

        if (removeImage && !string.IsNullOrEmpty(imageName))
            try
            {
                await _docker.RemoveImageAsync(imageName, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to remove image {ImageName}", imageName);
            }

        if (!string.IsNullOrWhiteSpace(serviceRoot) && Directory.Exists(serviceRoot))
            try
            {
                Directory.Delete(serviceRoot, true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete directory {ServiceRoot}", serviceRoot);
            }

        _logger.LogInformation("Service {ServiceName} removed", serviceInfo.Name);
        return true;
    }
}
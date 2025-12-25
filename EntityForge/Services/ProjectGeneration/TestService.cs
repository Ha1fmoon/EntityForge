using System.Text;
using System.Text.Json;
using EntityForge.Core;
using EntityForge.Models;

namespace EntityForge.Services.ProjectGeneration;

public class TestService
{
    private const int HttpTimeoutMs = 6000;
    private const int HealthCheckTimeoutSec = 5;
    private const int DefaultMaxAttempts = 5;
    private const int BaseDelayMs = 1000;
    private const int LogPreviewLength = 1000;
    private const int LogErrorLength = 2000;

    private readonly ILogger<TestService> _logger;

    public TestService(ILogger<TestService> logger)
    {
        _logger = logger;
    }

    public record CheckResult(bool Success, List<string> Errors, List<string> Warnings);

    public async Task<CheckResult> CheckAsync(string baseUrl, EntityConfig entityConfig, CancellationToken ct = default)
    {
        using var http = new HttpClient();
        http.Timeout = TimeSpan.FromMilliseconds(HttpTimeoutMs);
        return await RunCrudTests(http, baseUrl, entityConfig, ct);
    }

    public async Task<bool> CheckHealthAsync(string healthUrl, int maxAttempts = DefaultMaxAttempts)
    {
        using var http = new HttpClient();
        http.Timeout = TimeSpan.FromSeconds(HealthCheckTimeoutSec);

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                _logger.LogInformation("Health check attempt {Attempt}/{Max}: {Url}", attempt + 1, maxAttempts,
                    healthUrl);
                var response = await http.GetAsync(healthUrl);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Health check succeeded on attempt {Attempt}", attempt + 1);
                    return true;
                }

                _logger.LogWarning("Health check returned {StatusCode} on attempt {Attempt}", response.StatusCode,
                    attempt + 1);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Health check error on attempt {Attempt}: {Message}", attempt + 1, ex.Message);
            }

            if (attempt >= maxAttempts - 1) continue;

            var delayMs = BaseDelayMs * (1 << attempt);
            await Task.Delay(delayMs);
        }

        _logger.LogError("Health check failed after {MaxAttempts} attempts", maxAttempts);
        return false;
    }

    private async Task<CheckResult> RunCrudTests(HttpClient http, string baseUrl, EntityConfig entityConfig,
        CancellationToken ct)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        try
        {
            var route = NamingHelper.ToKebabCase(entityConfig.PluralName);
            var apiUrl = $"{baseUrl}/api/{route}";
            _logger.LogInformation("Test target endpoint: {ApiUrl}", apiUrl);

            var payload = CreateTestEntity(entityConfig);
            var json = JsonSerializer.Serialize(payload);
            var jsonPreview = Truncate(json, LogPreviewLength);
            _logger.LogInformation("Test POST data (preview): {Payload}", jsonPreview);

            var createResponse =
                await http.PostAsync(apiUrl, new StringContent(json, Encoding.UTF8, "application/json"), ct);

            var postCode = (int)createResponse.StatusCode;
            string? createdId = null;
            if (createResponse.IsSuccessStatusCode)
            {
                _logger.LogInformation("CRUD POST test passed: {Code}", createResponse.StatusCode);
                var content = await createResponse.Content.ReadAsStringAsync(ct);
                createdId = TryExtractId(content);
            }
            else
            {
                var body = Truncate(await createResponse.Content.ReadAsStringAsync(ct), LogErrorLength);

                _logger.LogError("Test POST {Url} failed: {StatusCode} {Status}. Body: {Body}", apiUrl, postCode,
                    createResponse.StatusCode, body);

                errors.Add($"POST {apiUrl} failed: {postCode} {createResponse.StatusCode}. Body: {body}");

                if (postCode == 400 || postCode == 500)
                    return new CheckResult(false, errors, warnings);
            }

            var getResponse = await http.GetAsync(apiUrl, ct);
            if (!getResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("Test GET {Url} failed: {StatusCode}", apiUrl, getResponse.StatusCode);
                warnings.Add($"GET {apiUrl} failed: {getResponse.StatusCode}");
            }
            else
            {
                _logger.LogInformation("CRUD GET list test passed: {Code}", getResponse.StatusCode);
            }

            if (createResponse.IsSuccessStatusCode && !string.IsNullOrWhiteSpace(createdId))
            {
                try
                {
                    var getByIdUrl = apiUrl + "/by-id/" + createdId;
                    var getByIdResponse = await http.GetAsync(getByIdUrl, ct);
                    if (!getByIdResponse.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("Test GET {Url} failed: {StatusCode}", getByIdUrl,
                            getByIdResponse.StatusCode);
                        warnings.Add($"GET {getByIdUrl} failed: {getByIdResponse.StatusCode}");
                    }
                    else
                    {
                        _logger.LogInformation("CRUD GET by ID test passed: {Code}", getByIdResponse.StatusCode);
                    }

                    var deleteUrl = $"{apiUrl}/{createdId}";
                    var deleteResponse = await http.DeleteAsync(deleteUrl, ct);
                    if (!deleteResponse.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("Test DELETE {Url} failed: {StatusCode}", deleteUrl,
                            deleteResponse.StatusCode);
                        warnings.Add($"DELETE {deleteUrl} failed: {deleteResponse.StatusCode}");
                    }
                    else
                    {
                        _logger.LogInformation("CRUD DELETE test passed: {Code}", deleteResponse.StatusCode);
                    }
                }
                catch (Exception ex)
                {
                    warnings.Add($"Could not test GET by ID or DELETE: {ex.Message}");
                    _logger.LogWarning(ex, "Test GET by ID or DELETE test failed");
                }
            }
            else if (createResponse.IsSuccessStatusCode && string.IsNullOrWhiteSpace(createdId))
            {
                warnings.Add("Could not get ID from POST response.");
                _logger.LogWarning("Could not get ID from POST response.");
            }
        }
        catch (Exception ex)
        {
            errors.Add($"CRUD tests failed: {ex.Message}");
            _logger.LogError(ex, "CRUD tests error");
        }

        return new CheckResult(errors.Count == 0, errors, warnings);
    }

    private static Dictionary<string, object?> CreateTestEntity(EntityConfig entityConfig)
    {
        var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in entityConfig.Fields)
        {
            var typeId = field.Type.Id.ToLowerInvariant();
            object value = typeId switch
            {
                "int" => 1,
                "decimal" => 1.23m,
                "bool" => true,
                "datetime" => DateTime.UtcNow,
                "email" => $"test-{Guid.NewGuid():N}@example.com",
                "phone" => "+1234567890",
                "money" => 999.99m,
                _ => field.Name
            };
            payload[field.Name] = value;
        }

        return payload;
    }

    private string? TryExtractId(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("id", out var idProp))
                return idProp.ToString();

            if (doc.RootElement.TryGetProperty("Id", out idProp))
                return idProp.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract ID from response");
        }

        return null;
    }

    private static string Truncate(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text.Length <= maxLength ? text : text.Substring(0, maxLength) + "...";
    }
}
using System.Net;
using Polly;
using Polly.Retry;

namespace EntityForge.Gateway.Services;

public class RoutingService
{
    private readonly ServiceRegistryService _registry;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<RoutingService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;

    public RoutingService(ServiceRegistryService registry, IHttpClientFactory httpClientFactory,
        ILogger<RoutingService> logger, IHttpContextAccessor httpContextAccessor)
    {
        _registry = registry;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;

        _retryPolicy = Policy
            .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
            .Or<HttpRequestException>()
            .WaitAndRetryAsync(
                3,
                attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)),
                (outcome, timeSpan, retryCount, _) =>
                {
                    _logger.LogWarning("Retry {RetryCount}/3 after {Seconds}s because of: {Reason}",
                        retryCount, timeSpan.TotalSeconds, outcome.Exception?.Message ?? "Non-success status code");
                });
    }

    public Task<HttpResponseMessage> RouteRequestAsync(string entityName, string path, HttpMethod method,
        HttpContent? content = null, CancellationToken ct = default)
    {
        return ExecuteRoutedRequestAsync(entityName, serviceUrl =>
        {
            var url = $"{serviceUrl}/{path}";
            var request = new HttpRequestMessage(method, url);
            if (content != null) request.Content = content;
            return request;
        }, ct);
    }

    public Task<HttpResponseMessage> RouteMultipleRequestsAsync(string entityName, string ids,
        CancellationToken ct = default)
    {
        return ExecuteRoutedRequestAsync(entityName, serviceUrl =>
        {
            var url = $"{serviceUrl}/api/{entityName.ToLower()}s?ids={ids}";
            return new HttpRequestMessage(HttpMethod.Get, url);
        }, ct);
    }

    private async Task<HttpResponseMessage> ExecuteRoutedRequestAsync(string entityName,
        Func<string, HttpRequestMessage> requestFactory, CancellationToken ct)
    {
        var service = await _registry.GetServiceByEntityAsync(entityName);

        if (service == null)
        {
            _logger.LogError("Service for entity {EntityName} not found", entityName);
            return new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent($"Service for entity '{entityName}' not found")
            };
        }

        if (!service.IsHealthy)
            _logger.LogWarning("Service {ServiceName} is unhealthy, but attempting request", service.Name);

        var client = _httpClientFactory.CreateClient();

        try
        {
            var response = await _retryPolicy.ExecuteAsync(async () =>
            {
                var request = requestFactory(service.Url);

                var correlationId = _httpContextAccessor.HttpContext?.Items["CorrelationId"]?.ToString();
                if (!string.IsNullOrEmpty(correlationId)) request.Headers.Add("X-Correlation-Id", correlationId);

                _logger.LogInformation("Routing {Method} request to {Url}",
                    request.Method, request.RequestUri);
                return await client.SendAsync(request, ct);
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to route request for entity {EntityName}", entityName);
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent($"Failed to reach service: {ex.Message}")
            };
        }
    }
}
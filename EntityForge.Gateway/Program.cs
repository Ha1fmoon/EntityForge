using EntityForge.Gateway.BackgroundServices;
using EntityForge.Gateway.Middleware;
using EntityForge.Gateway.Services;
using Serilog;
using Serilog.Events;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
{
    configuration
        .MinimumLevel.Warning()
        .MinimumLevel.Override("EntityForge.Gateway", LogEventLevel.Information)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Service", "Gateway")
        .WriteTo.Console();
});

var redisConnection = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConnection));

builder.Services.AddHttpClient();

builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<ServiceRegistryService>();
builder.Services.AddScoped<RoutingService>();
builder.Services.AddScoped<RelationService>();
builder.Services.AddScoped<RelationTypeService>();

builder.Services.AddHostedService<HealthCheckWorker>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSerilogRequestLogging();

app.UseMiddleware<CorrelationIdMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.MapGet("/", () => Results.Redirect("/swagger"));
}

app.MapGet("/health", () => Results.Ok(new { status = "OK", service = "Gateway" }));

app.MapControllers();


app.Run();
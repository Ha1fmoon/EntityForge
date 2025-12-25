using EntityForge.Core.Interfaces.Generation;
using EntityForge.Data;
using EntityForge.Services;
using EntityForge.Services.ProjectGeneration;
using EntityForge.Services.ProjectGeneration.Steps;
using EntityForge.Services.Templates;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Events;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
{
    configuration
        .MinimumLevel.Warning()
        .MinimumLevel.Override("EntityForge", LogEventLevel.Information)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Service", "EntityForge")
        .WriteTo.Console();
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "EntityForge API", Version = "v1" });
});

var redisConnection = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConnection));

builder.Services.AddSingleton<EntityRepository>();

builder.Services.AddSingleton<TypeService>();

builder.Services.AddSingleton<DomainTemplateService>();
builder.Services.AddSingleton<ApplicationTemplateService>();
builder.Services.AddSingleton<InfrastructureTemplateService>();
builder.Services.AddSingleton<ApiTemplateService>();

builder.Services.AddSingleton<DotnetCliService>();
builder.Services.AddSingleton<DockerService>();
builder.Services.AddSingleton<TestService>();
builder.Services.AddSingleton<CleanupService>();

builder.Services.AddSingleton<ServiceRegistry>();

builder.Services.AddSingleton<GenerationService>();

builder.Services.AddSingleton<IGenerationStep, InitializeServiceStep>();
builder.Services.AddSingleton<IGenerationStep, CreateStructureStep>();
builder.Services.AddSingleton<IGenerationStep, GenerateDomainStep>();
builder.Services.AddSingleton<IGenerationStep, GenerateApplicationStep>();
builder.Services.AddSingleton<IGenerationStep, GenerateInfrastructureStep>();
builder.Services.AddSingleton<IGenerationStep, GenerateApiStep>();
builder.Services.AddSingleton<IGenerationStep, BuildProjectStep>();
builder.Services.AddSingleton<IGenerationStep, BuildDockerImageStep>();
builder.Services.AddSingleton<IGenerationStep, RunDockerContainerStep>();
builder.Services.AddSingleton<IGenerationStep, CheckPostgresHealthStep>();
builder.Services.AddSingleton<IGenerationStep, TestStep>();
builder.Services.AddSingleton<IGenerationStep, FinalizeStep>();

var app = builder.Build();

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();

app.Run();
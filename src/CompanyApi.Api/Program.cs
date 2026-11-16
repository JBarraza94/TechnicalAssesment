using System.Text.Json.Serialization;
using CompanyApi.Api.Errors;
using CompanyApi.Application;
using CompanyApi.Application.Relevance;
using CompanyApi.Infrastructure;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);

builder.Services.Configure<ApiBehaviorOptions>(o => o.InvalidModelStateResponseFactory = ValidationErrorFactory.Create);

builder.Services.AddOptions<RelevanceOptions>()
    .Bind(builder.Configuration.GetSection(RelevanceOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddApplication();
builder.Services.AddInfrastructure();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

const string CorsPolicy = "frontend";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:4200"];
builder.Services.AddCors(o => o.AddPolicy(CorsPolicy, p => p
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .WithMethods("GET", "POST", "PUT", "DELETE")
    .WithExposedHeaders("Location", "X-Total-Count")));

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(o => o.SwaggerEndpoint("/openapi/v1.json", "Company API v1"));
}

app.UseCors(CorsPolicy);
app.MapControllers();

app.Run();

/// <summary>Exposed for WebApplicationFactory in integration tests.</summary>
public partial class Program;

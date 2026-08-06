using RegisterSummary.Api.Endpoints;
using RegisterSummary.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

const string DevCorsPolicy = "DevCors";
const string AppVersion = "1.1";

builder.Services.AddRegisterSummaryInfrastructure(builder.Configuration);

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy(DevCorsPolicy, policy =>
        {
            policy.WithOrigins("http://localhost:5173")
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
    });
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseCors(DevCorsPolicy);
}

app.MapGet("/api/health", () => Results.Ok(new { status = "healthy", version = AppVersion }));

app.MapJobEndpoints();

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

app.Run();

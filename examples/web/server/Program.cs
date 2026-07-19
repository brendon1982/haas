using HaaS.Host.Web;
using HaaS.Infrastructure;
using Microsoft.Extensions.AI;
using OllamaSharp;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var haas = builder.Services.AddHaas();
haas.AddQueuedWorkerPool(workerCount: 2, pool =>
{
    // Sources will be added in later steps
});

// Configure AI Clients (similar to CLI example)
builder.Services.AddSingleton<IChatClient>(sp => 
{
    return new OllamaApiClient(new Uri("http://localhost:11434"), "llama3.2");
});

var app = builder.Build();

app.UseCors();

app.MapHub<HaaSWebHub>("/haasHub");

app.MapGet("/", () => "HaaS Web Host is running.");

app.Run();

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

builder.Services.AddSingleton<WebSignalBus>();

var haas = builder.Services.AddHaas();
haas.WithSqlitePersistence("data", includeConfig: false)
    .AddQueuedWorkerPool(workerCount: 2, pool =>
    {
        pool.AddSignalSource<ChatWebSignalSource, ChatWebSignalPresenter>(config =>
        {
            config.UseProvider("ollama")
                  .UseModel("llama3.2")
                  .UseSystemPrompt("You are an assistant in a web chat. Reply concisely.");
        });

        pool.AddSignalSource<TicTacToeWebSignalSource, TicTacToeWebSignalPresenter>(config =>
        {
            config.UseProvider("ollama")
                  .UseModel("llama3.2")
                  .UseSystemPrompt("You are a TicTacToe player. Use tools to play.");
        });
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

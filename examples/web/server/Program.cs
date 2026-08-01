using HaaS.Host.Web.TicTacToe;
using HaaS.Host.Web.Chat;
using HaaS.Host.Web.Infrastructure;
using HaaS.Domain.Ports;
using HaaS.Infrastructure;

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

// Shared Infrastructure
builder.Services.AddSingleton<WebSignalBus>();
builder.Services.AddSingleton<SessionManager>();

// Module Services
builder.Services.AddTicTacToeServices();

var haas = builder.Services.AddHaas();

haas.WithSqlitePersistence("data", includeConfig: false)
    .WithInMemoryConfig(config =>
    {
        config.UseOllama();
        config.UseOpenRouter();
    })
    .AddQueuedWorkerPool(workerCount: 2, pool =>
    {
        pool.AddChatWebModule();
        pool.AddTicTacToeWebModule();
    });

var app = builder.Build();

// Register tools
app.Services.GetRequiredService<IToolProvider>().RegisterTicTacToeTools();

app.UseCors();

app.MapHub<ChatHub>("/chatHub");
app.MapHub<TicTacToeHub>("/tictactoeHub");

app.MapGet("/", () => "HaaS Web Host is running.");

app.Run();

using HaaS.Application.UseCases;
using HaaS.Adapters.Observability;
using HaaS.Host.Web;
using HaaS.Host.Web.TicTacToe;
using HaaS.Host.Web.Chat;
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

builder.Services.AddSingleton<WebSignalBus>();
builder.Services.AddSingleton<SessionManager>();
builder.Services.AddScoped<ScopedSessionContext>();
builder.Services.AddScoped<WebTicTacToeToolHandlers>();
builder.Services.AddScoped<TicTacToeHubHandlers>();

var haas = builder.Services.AddHaas();

// Decorate IRunSessionUseCase manually since we don't have Scrutor
builder.Services.AddScoped<RunSessionUseCase>();
builder.Services.AddScoped<IRunSessionUseCase>(sp =>
{
    var inner = sp.GetRequiredService<RunSessionUseCase>();
    var logger = sp.GetRequiredService<HaaS.Domain.Ports.ILogger>();
    var observable = new ObservableRunSessionUseCase(inner, logger);
    var context = sp.GetRequiredService<ScopedSessionContext>();
    return new SessionContextRunSessionUseCaseDecorator(observable, context);
});

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

app.MapHub<HaaSWebHub>("/haasHub");

app.MapGet("/", () => "HaaS Web Host is running.");

app.Run();

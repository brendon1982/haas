using HaaS.Application.UseCases;
using HaaS.Adapters.Observability;
using HaaS.Host.Web;
using HaaS.Host.Web.TicTacToe;
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
        pool.AddSignalSource<ChatWebSignalSource, ChatWebSignalPresenter>(config =>
        {
            config.UseProvider("openrouter")
                  .UseModel("cohere/north-mini-code:free")
                  .UseSystemPrompt("You are an assistant in a web chat. Reply concisely.");
        });

        pool.AddSignalSource<TicTacToeWebSignalSource, TicTacToeWebSignalPresenter>(config =>
        {
            config.UseProvider("openrouter")
                  .UseModel("cohere/north-mini-code:free")
                  .UseSystemPrompt("You are a TicTacToe player. Use tools to play.")
                  .AddTool("get_board")
                  .AddTool("get_valid_moves")
                  .AddTool("place_marker");
        });
    });

var app = builder.Build();

// Register tools
var toolProvider = app.Services.GetRequiredService<IToolProvider>();
toolProvider.Register<WebTicTacToeToolHandlers>("get_board", "Returns the current board", h => h.GetBoard);
toolProvider.Register<WebTicTacToeToolHandlers>("get_valid_moves", "Returns valid moves", h => h.GetValidMoves);
toolProvider.Register<WebTicTacToeToolHandlers>("place_marker", "Places a marker", (WebTicTacToeToolHandlers h) => (Func<int, string>)h.PlaceMarker);

app.UseCors();

app.MapHub<HaaSWebHub>("/haasHub");

app.MapGet("/", () => "HaaS Web Host is running.");

app.Run();

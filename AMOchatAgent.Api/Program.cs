using AMOchatAgent.Api.Services;
using AMOchatAgent.Api.Tools;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .Build())
    .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((ctx, services, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration));

// CORS
builder.Services.AddCors(opt =>
    opt.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

builder.Services.AddControllers();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();

// LLM config
var llmConfig = builder.Configuration.GetSection("Llm").Get<LlmConfig>() ?? new LlmConfig();
builder.Services.AddSingleton(llmConfig);
builder.Services.AddSingleton<LlmServiceFactory>();

// Register all Tools
builder.Services.AddScoped<ITool, CreateOrderTool>();
builder.Services.AddScoped<ITool, QueryOrderTool>();
builder.Services.AddScoped<ITool, CancelOrderTool>();
builder.Services.AddScoped<ITool, QueryLogisticsTool>();
builder.Services.AddScoped<ITool, CheckUserKycTool>();

// Attachment services
builder.Services.AddSingleton<AttachmentStore>();
builder.Services.AddScoped<ToolAttachmentContext>();

// Agent service
builder.Services.AddScoped<IAgentService, AgentService>();

var app = builder.Build();

app.UseCors();
app.MapControllers();

app.Run();

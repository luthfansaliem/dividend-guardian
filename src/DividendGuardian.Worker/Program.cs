using DividendGuardian.AI;
using DividendGuardian.Infrastructure;
using DividendGuardian.Notification;
using Microsoft.Extensions.Options;

var builder=Host.CreateApplicationBuilder(args);

builder.Services.Configure<DatabaseOptions>(o=>o.ConnectionString=builder.Configuration["SUPABASE_CONNECTION_STRING"]??"");
builder.Services.Configure<AiOptions>(o=>{
    o.ApiKey=builder.Configuration["OPENAI_API_KEY"]??"";
    o.Model=builder.Configuration["OPENAI_MODEL"]??"gpt-5.6-luna";
});
builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection("Worker"));

builder.Services.AddSingleton(sp=>new Database(sp.GetRequiredService<IOptions<DatabaseOptions>>().Value));
builder.Services.AddSingleton<StockRepository>();
builder.Services.AddHttpClient<AiAnalyst>(c=>c.Timeout=TimeSpan.FromSeconds(60));
builder.Services.AddHttpClient<TelegramNotifier>();
builder.Services.AddHostedService<Worker>();

await builder.Build().RunAsync();
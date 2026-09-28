using DividendGuardian.AI;
using DividendGuardian.Infrastructure;
using DividendGuardian.Notification;
using DividendGuardian.Quant;
using DividendGuardian.Worker;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<DatabaseOptions>(o =>
    o.ConnectionString = builder.Configuration["SUPABASE_CONNECTION_STRING"] ?? "");

builder.Services.Configure<AiOptions>(o =>
{
    o.Provider = builder.Configuration["AI_PROVIDER"] ?? "openai";
    o.ApiKey = builder.Configuration["OPENAI_API_KEY"] ?? "";
    o.GroqApiKey = builder.Configuration["GROQ_API_KEY"] ?? "";
    o.GroqModel = builder.Configuration["GROQ_MODEL"] ?? "openai/gpt-oss-20b";
    o.Model = builder.Configuration["OPENAI_MODEL"] ?? "gpt-5.6-luna";
    var maxAnalysesSetting = builder.Configuration["AI_MAX_ANALYSES_PER_DAY"]
        ?? builder.Configuration["OPENAI_MAX_ANALYSES_PER_DAY"];
    o.MaxAnalysesPerDay = int.TryParse(maxAnalysesSetting, out var maxAnalyses) ? maxAnalyses : 10;
    o.ForceAnalysis = bool.TryParse(builder.Configuration["AI_FORCE_ANALYSIS"], out var forceAnalysis) && forceAnalysis;
    o.MaxRetries = int.TryParse(builder.Configuration["OPENAI_MAX_RETRIES"], out var retries) ? retries : 2;
    o.RetryDelayMs = int.TryParse(builder.Configuration["OPENAI_RETRY_DELAY_MS"], out var retryDelay) ? retryDelay : 500;
});

builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<IOptions<AiOptions>>().Value);

builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection("Worker"));

builder.Services.Configure<TelegramOptions>(o =>
{
    o.BotToken = builder.Configuration["TELEGRAM_BOT_TOKEN"] ?? "";
    o.ChatId = builder.Configuration["TELEGRAM_CHAT_ID"] ?? "";
    o.Enabled = bool.TryParse(builder.Configuration["TELEGRAM_ENABLED"], out var enabled) && enabled;
});

builder.Services.Configure<MarketDataOptions>(o =>
{
    o.Provider = builder.Configuration["MARKET_DATA_PROVIDER"] ?? "none";
    o.ApiKey = builder.Configuration["TWELVE_DATA_API_KEY"] ?? "";
    o.BaseUrl = builder.Configuration["MARKET_DATA_BASE_URL"] ?? "https://api.twelvedata.com";
    o.MicCode = builder.Configuration["MARKET_DATA_MIC"] ?? "XIDX";
    o.LookbackDays = int.TryParse(builder.Configuration["MARKET_DATA_LOOKBACK_DAYS"], out var days) ? days : 14;
    o.HistoryYears = int.TryParse(builder.Configuration["MARKET_DATA_HISTORY_YEARS"], out var historyYears) ? historyYears : 6;
});

builder.Services.Configure<FundamentalDataOptions>(o =>
{
    o.Provider = builder.Configuration["FUNDAMENTALS_PROVIDER"] ?? "none";
    o.ApiKey = builder.Configuration["TWELVE_DATA_API_KEY"] ?? "";
    o.BaseUrl = builder.Configuration["FUNDAMENTALS_BASE_URL"] ?? "https://api.twelvedata.com";
    o.MicCode = builder.Configuration["FUNDAMENTALS_MIC"] ?? "XIDX";
    o.LookbackYears = int.TryParse(builder.Configuration["FUNDAMENTALS_LOOKBACK_YEARS"], out var years) ? years : 6;
    o.OutputSize = int.TryParse(builder.Configuration["FUNDAMENTALS_OUTPUT_SIZE"], out var size) ? size : 6;
    o.RequestDelayMs = int.TryParse(builder.Configuration["FUNDAMENTALS_REQUEST_DELAY_MS"], out var delay) ? delay : 500;
    o.IncludeCashFlow = !string.Equals(builder.Configuration["FUNDAMENTALS_INCLUDE_CASH_FLOW"], "false", StringComparison.OrdinalIgnoreCase);
    o.IncludeBalanceSheet = !string.Equals(builder.Configuration["FUNDAMENTALS_INCLUDE_BALANCE_SHEET"], "false", StringComparison.OrdinalIgnoreCase);
    o.IncludeDividends = !string.Equals(builder.Configuration["FUNDAMENTALS_INCLUDE_DIVIDENDS"], "false", StringComparison.OrdinalIgnoreCase);
});

builder.Services.AddSingleton(sp => new Database(sp.GetRequiredService<IOptions<DatabaseOptions>>().Value));
builder.Services.AddSingleton<StockRepository>();

builder.Services.AddSingleton<MarketDataRepository>();
builder.Services.AddSingleton<MarketDataCollector>();
builder.Services.AddHttpClient<TwelveDataMarketDataProvider>();
builder.Services.AddHttpClient<YahooFinanceMarketDataProvider>();

builder.Services.AddSingleton<IMarketDataProvider>(sp =>
{
    var options = sp.GetRequiredService<IOptions<MarketDataOptions>>().Value;

    if (options.Provider.Equals("yahoo", StringComparison.OrdinalIgnoreCase))
        return sp.GetRequiredService<YahooFinanceMarketDataProvider>();

    if (options.Provider.Equals("twelvedata", StringComparison.OrdinalIgnoreCase))
        return sp.GetRequiredService<TwelveDataMarketDataProvider>();

    return new NotConfiguredMarketDataProvider();
});

builder.Services.AddSingleton<FundamentalDataRepository>();
builder.Services.AddSingleton<FundamentalCollector>();
builder.Services.AddHttpClient<TwelveDataFundamentalDataProvider>();
builder.Services.AddHttpClient<YahooFinanceFundamentalDataProvider>();

builder.Services.AddSingleton<IFundamentalDataProvider>(sp =>
{
    var options = sp.GetRequiredService<IOptions<FundamentalDataOptions>>();

    if (options.Value.Provider.Equals("yahoo", StringComparison.OrdinalIgnoreCase))
        return sp.GetRequiredService<YahooFinanceFundamentalDataProvider>();

    if (options.Value.Provider.Equals("twelvedata", StringComparison.OrdinalIgnoreCase))
        return sp.GetRequiredService<TwelveDataFundamentalDataProvider>();

    if (options.Value.Provider.Equals("csv", StringComparison.OrdinalIgnoreCase))
        return new CsvFundamentalDataProvider(options);

    return new NotConfiguredFundamentalDataProvider();
});

builder.Services.AddSingleton<QuantAnalysisEngine>();
builder.Services.AddHttpClient<FrankfurterFxRateProvider>(client =>
    client.BaseAddress = new Uri("https://api.frankfurter.dev/"));
builder.Services.AddSingleton<IFxRateProvider>(sp =>
    sp.GetRequiredService<FrankfurterFxRateProvider>());
builder.Services.AddSingleton<QuantDataRepository>();
builder.Services.AddSingleton<QuantAnalysisRepository>();
builder.Services.AddSingleton<QuantAnalysisOrchestrator>();

builder.Services.AddHttpClient<AiAnalyst>(c => c.Timeout = TimeSpan.FromSeconds(60));
builder.Services.AddHttpClient<GroqAiAnalyst>(c => c.Timeout = TimeSpan.FromSeconds(60));
builder.Services.AddSingleton<IAiAnalyst>(sp =>
{
    var options = sp.GetRequiredService<AiOptions>();
    return options.Provider.Equals("groq", StringComparison.OrdinalIgnoreCase)
        ? sp.GetRequiredService<GroqAiAnalyst>()
        : sp.GetRequiredService<AiAnalyst>();
});
builder.Services.AddSingleton<AiAnalysisOrchestrator>();
builder.Services.AddSingleton<AiTriggerPolicy>();
builder.Services.AddSingleton<AiAnalysisRepository>();
builder.Services.AddSingleton<AiAnalysisCycleOrchestrator>();

builder.Services.AddHttpClient<TelegramNotifier>(client => client.Timeout = TimeSpan.FromSeconds(15));
builder.Services.AddSingleton<TelegramAlertService>();

builder.Services.AddHostedService<Worker>();

await builder.Build().RunAsync();

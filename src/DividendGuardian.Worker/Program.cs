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
builder.Services.Configure<FundamentalDataOptions>(o=>{
    o.Provider=builder.Configuration["FUNDAMENTALS_PROVIDER"]??"none";
    o.ApiKey=builder.Configuration["TWELVE_DATA_API_KEY"]??"";
    o.BaseUrl=builder.Configuration["FUNDAMENTALS_BASE_URL"]??"https://api.twelvedata.com";
    o.MicCode=builder.Configuration["FUNDAMENTALS_MIC"]??"XIDX";
    o.LookbackYears=int.TryParse(builder.Configuration["FUNDAMENTALS_LOOKBACK_YEARS"],out var years)?years:6;
    o.OutputSize=int.TryParse(builder.Configuration["FUNDAMENTALS_OUTPUT_SIZE"],out var size)?size:6;
    o.RequestDelayMs=int.TryParse(builder.Configuration["FUNDAMENTALS_REQUEST_DELAY_MS"],out var delay)?delay:500;
});
builder.Services.Configure<MarketDataOptions>(o=>{
    o.Provider=builder.Configuration["MARKET_DATA_PROVIDER"]??"none";
    o.ApiKey=builder.Configuration["TWELVE_DATA_API_KEY"]??"";
    o.BaseUrl=builder.Configuration["MARKET_DATA_BASE_URL"]??"https://api.twelvedata.com";
    o.MicCode=builder.Configuration["MARKET_DATA_MIC"]??"XIDX";
    o.LookbackDays=int.TryParse(builder.Configuration["MARKET_DATA_LOOKBACK_DAYS"],out var days)?days:14;
});
builder.Services.Configure<FundamentalDataOptions>(o=>{
    o.Provider=builder.Configuration["FUNDAMENTALS_PROVIDER"]??"none";
    o.ApiKey=builder.Configuration["TWELVE_DATA_API_KEY"]??"";
    o.BaseUrl=builder.Configuration["FUNDAMENTALS_BASE_URL"]??"https://api.twelvedata.com";
    o.MicCode=builder.Configuration["FUNDAMENTALS_MIC"]??"XIDX";
    o.LookbackYears=int.TryParse(builder.Configuration["FUNDAMENTALS_LOOKBACK_YEARS"],out var years)?years:6;
    o.OutputSize=int.TryParse(builder.Configuration["FUNDAMENTALS_OUTPUT_SIZE"],out var size)?size:6;
    o.RequestDelayMs=int.TryParse(builder.Configuration["FUNDAMENTALS_REQUEST_DELAY_MS"],out var delay)?delay:500;
    o.IncludeCashFlow=!string.Equals(builder.Configuration["FUNDAMENTALS_INCLUDE_CASH_FLOW"],"false",StringComparison.OrdinalIgnoreCase);
    o.IncludeBalanceSheet=!string.Equals(builder.Configuration["FUNDAMENTALS_INCLUDE_BALANCE_SHEET"],"false",StringComparison.OrdinalIgnoreCase);
    o.IncludeDividends=!string.Equals(builder.Configuration["FUNDAMENTALS_INCLUDE_DIVIDENDS"],"false",StringComparison.OrdinalIgnoreCase);
});

builder.Services.AddSingleton(sp=>new Database(sp.GetRequiredService<IOptions<DatabaseOptions>>().Value));
builder.Services.AddSingleton<StockRepository>();
builder.Services.AddSingleton<MarketDataRepository>();
builder.Services.AddSingleton<MarketDataCollector>();
builder.Services.AddHttpClient<TwelveDataMarketDataProvider>();
builder.Services.AddSingleton<IMarketDataProvider>(sp=>{
    var options=sp.GetRequiredService<IOptions<MarketDataOptions>>().Value;
    return options.Provider.Equals("twelvedata",StringComparison.OrdinalIgnoreCase)
        ? sp.GetRequiredService<TwelveDataMarketDataProvider>()
        : new NotConfiguredMarketDataProvider();
});
builder.Services.AddSingleton<FundamentalDataRepository>();
builder.Services.AddSingleton<FundamentalCollector>();
builder.Services.AddHttpClient<TwelveDataFundamentalDataProvider>();
builder.Services.AddSingleton<IFundamentalDataProvider>(sp=>{
    var options=sp.GetRequiredService<IOptions<FundamentalDataOptions>>().Value;
    return options.Provider.Equals("twelvedata",StringComparison.OrdinalIgnoreCase)
        ? sp.GetRequiredService<TwelveDataFundamentalDataProvider>()
        : new NotConfiguredFundamentalDataProvider();
});
builder.Services.AddSingleton<FundamentalDataRepository>();
builder.Services.AddSingleton<FundamentalCollector>();
builder.Services.AddHttpClient<TwelveDataFundamentalDataProvider>();
builder.Services.AddSingleton<IFundamentalDataProvider>(sp=>{
    var options=sp.GetRequiredService<IOptions<FundamentalDataOptions>>().Value;
    return options.Provider.Equals("twelvedata",StringComparison.OrdinalIgnoreCase)
        ? sp.GetRequiredService<TwelveDataFundamentalDataProvider>()
        : new NotConfiguredFundamentalDataProvider();
});
builder.Services.AddHttpClient<AiAnalyst>(c=>c.Timeout=TimeSpan.FromSeconds(60));
builder.Services.AddHttpClient<TelegramNotifier>();
builder.Services.AddHostedService<Worker>();

await builder.Build().RunAsync();
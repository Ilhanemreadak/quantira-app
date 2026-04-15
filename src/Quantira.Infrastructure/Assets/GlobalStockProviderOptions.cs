namespace Quantira.Infrastructure.Assets;

public sealed class GlobalStockProviderOptions
{
    public const string SectionName = "AssetProviders:GlobalStocks";

    public bool Enabled { get; set; }

    public string BaseUrl { get; set; } = "https://finnhub.io/";

    public string ApiKey { get; set; } = string.Empty;

    public string DefaultCurrency { get; set; } = "USD";

    public string DefaultExchangeLabel { get; set; } = "GLOBAL";

    public List<string> Exchanges { get; set; } = ["US"];
}
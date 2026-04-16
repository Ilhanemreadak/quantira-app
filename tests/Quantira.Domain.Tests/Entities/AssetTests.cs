using Quantira.Domain.Entities;
using Quantira.Domain.Enums;
using Quantira.Domain.Events;
using Quantira.Domain.Exceptions;

namespace Quantira.Domain.Tests.Entities;

public sealed class AssetTests
{
    public static IEnumerable<object?[]> ValidCreateCases()
    {
        yield return
        [
            "  btc  ", "  Bitcoin  ", AssetType.Crypto, "  usd  ", "  binance  ", null, "  BTCUSDT  ",
            "BTC", "Bitcoin", "USD", "BINANCE", null, "BTCUSDT"
        ];

        yield return
        [
            "  thyao  ", "  Turk Hava Yollari  ", AssetType.Stock, "  try  ", "  bist  ", "  Industrials  ", "  THYAO.IS  ",
            "THYAO", "Turk Hava Yollari", "TRY", "BIST", "Industrials", "THYAO.IS"
        ];

        yield return
        [
            "  xau/usd  ", "  Gold Spot  ", AssetType.Commodity, "  usd  ", null, null, null,
            "XAU/USD", "Gold Spot", "USD", null, null, null
        ];
    }

    public static IEnumerable<object?[]> InvalidTextCases()
    {
        yield return [null];
        yield return [""];
        yield return ["   "];
        yield return ["\t"];
    }

    public static IEnumerable<object?[]> InvalidCurrencyCases()
    {
        yield return [null];
        yield return [""];
        yield return ["   "];
        yield return ["US"];
        yield return ["USDT"];
        yield return ["1"];
        yield return ["TRYY"];
    }

    public static IEnumerable<object?[]> CatalogueUpdateCases()
    {
        yield return ["Bitcoin", "BTCUSDT", "Bitcoin Spot", "BTCUSDT", true, "Bitcoin Spot", "BTCUSDT"];
        yield return ["Bitcoin", "BTCUSDT", "Bitcoin", "BTCUSD", true, "Bitcoin", "BTCUSD"];
        yield return ["Old Name", "OLDKEY", "  New Name  ", "  NEWKEY  ", true, "New Name", "NEWKEY"];
        yield return ["Bitcoin", "BTCUSDT", "  Bitcoin  ", "  BTCUSDT  ", false, "Bitcoin", "BTCUSDT"];
        yield return ["Bitcoin", "BTCUSDT", "Bitcoin", null, true, "Bitcoin", null];
    }

    [Theory]
    [MemberData(nameof(ValidCreateCases))]
    public void Create_WithValidParameters_ShouldReturnNormalizedActiveAsset(
        string symbol,
        string name,
        AssetType assetType,
        string currency,
        string? exchange,
        string? sector,
        string? dataProviderKey,
        string expectedSymbol,
        string expectedName,
        string expectedCurrency,
        string? expectedExchange,
        string? expectedSector,
        string? expectedDataProviderKey)
    {
        // Arrange
        var beforeCreate = DateTime.UtcNow;

        // Act
        var asset = Asset.Create(symbol, name, assetType, currency, exchange, sector, dataProviderKey);
        var afterCreate = DateTime.UtcNow;

        // Assert
        Assert.NotEqual(Guid.Empty, asset.Id);
        Assert.Equal(expectedSymbol, asset.Symbol);
        Assert.Equal(expectedName, asset.Name);
        Assert.Equal(assetType, asset.AssetType);
        Assert.Equal(expectedCurrency, asset.Currency);
        Assert.Equal(expectedExchange, asset.Exchange);
        Assert.Equal(expectedSector, asset.Sector);
        Assert.Equal(expectedDataProviderKey, asset.DataProviderKey);
        Assert.True(asset.IsActive);
        Assert.InRange(asset.CreatedAt, beforeCreate, afterCreate);
        Assert.InRange(asset.UpdatedAt, beforeCreate, afterCreate);
        Assert.Null(asset.DeletedAt);
        Assert.False(asset.IsDeleted);
        Assert.Empty(asset.DomainEvents);
    }

    [Theory]
    [MemberData(nameof(InvalidTextCases))]
    public void Create_WithNullOrWhiteSpaceSymbol_ShouldThrowDomainException(string? symbol)
    {
        // Arrange
        const string validName = "Bitcoin";
        const string validCurrency = "USD";

        // Act
        var action = () => Asset.Create(symbol!, validName, AssetType.Crypto, validCurrency);

        // Assert
        var exception = Assert.Throws<DomainException>(action);
        Assert.Equal("Asset symbol cannot be empty.", exception.Message);
    }

    [Theory]
    [MemberData(nameof(InvalidTextCases))]
    public void Create_WithNullOrWhiteSpaceName_ShouldThrowDomainException(string? name)
    {
        // Arrange
        const string validSymbol = "BTC";
        const string validCurrency = "USD";

        // Act
        var action = () => Asset.Create(validSymbol, name!, AssetType.Crypto, validCurrency);

        // Assert
        var exception = Assert.Throws<DomainException>(action);
        Assert.Equal("Asset name cannot be empty.", exception.Message);
    }

    [Theory]
    [MemberData(nameof(InvalidCurrencyCases))]
    public void Create_WithInvalidCurrency_ShouldThrowDomainException(string? currency)
    {
        // Arrange
        const string validSymbol = "BTC";
        const string validName = "Bitcoin";

        // Act
        var action = () => Asset.Create(validSymbol, validName, AssetType.Crypto, currency!);

        // Assert
        var exception = Assert.Throws<DomainException>(action);
        Assert.Contains("not a valid ISO 4217 currency code", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(InvalidTextCases))]
    public void SetDataProviderKey_WithNullOrWhiteSpaceKey_ShouldThrowDomainException(string? key)
    {
        // Arrange
        var asset = Asset.Create("BTC", "Bitcoin", AssetType.Crypto, "USD", "BINANCE", null, "BTCUSDT");

        // Act
        var action = () => asset.SetDataProviderKey(key!);

        // Assert
        var exception = Assert.Throws<DomainException>(action);
        Assert.Equal("Data provider key cannot be empty.", exception.Message);
    }

    [Theory]
    [InlineData("  BTCUSDT  ", "BTCUSDT")]
    [InlineData(" THYAO.IS ", "THYAO.IS")]
    public void SetDataProviderKey_WithValidKey_ShouldTrimValueAndUpdateTimestamp(string key, string expectedKey)
    {
        // Arrange
        var asset = Asset.Create("BTC", "Bitcoin", AssetType.Crypto, "USD", "BINANCE", null, "OLD");
        var previousUpdatedAt = asset.UpdatedAt;

        // Act
        asset.SetDataProviderKey(key);

        // Assert
        Assert.Equal(expectedKey, asset.DataProviderKey);
        Assert.True(asset.UpdatedAt >= previousUpdatedAt);
    }

    [Theory]
    [MemberData(nameof(CatalogueUpdateCases))]
    public void UpdateCatalogueMetadata_WithDifferentInputCombinations_ShouldReturnExpectedChangeAndState(
        string initialName,
        string? initialProviderKey,
        string inputName,
        string? inputProviderKey,
        bool expectedHasChanged,
        string expectedName,
        string? expectedProviderKey)
    {
        // Arrange
        var asset = Asset.Create("BTC", initialName, AssetType.Crypto, "USD", "BINANCE", null, initialProviderKey);
        var previousUpdatedAt = asset.UpdatedAt;

        // Act
        var hasChanged = asset.UpdateCatalogueMetadata(inputName, inputProviderKey);

        // Assert
        Assert.Equal(expectedHasChanged, hasChanged);
        Assert.Equal(expectedName, asset.Name);
        Assert.Equal(expectedProviderKey, asset.DataProviderKey);

        if (expectedHasChanged)
            Assert.True(asset.UpdatedAt >= previousUpdatedAt);
        else
            Assert.Equal(previousUpdatedAt, asset.UpdatedAt);
    }

    [Theory]
    [MemberData(nameof(InvalidTextCases))]
    public void UpdateCatalogueMetadata_WithInvalidName_ShouldThrowDomainException(string? name)
    {
        // Arrange
        var asset = Asset.Create("BTC", "Bitcoin", AssetType.Crypto, "USD", "BINANCE", null, "BTCUSDT");

        // Act
        Action action = () => _ = asset.UpdateCatalogueMetadata(name!, "BTCUSDT");

        // Assert
        var exception = Assert.Throws<DomainException>(action);
        Assert.Equal("Asset name cannot be empty.", exception.Message);
    }

    [Fact]
    public void Deactivate_WhenAssetIsActive_ShouldMarkInactiveSoftDeleteAndAddDomainEvent()
    {
        // Arrange
        var asset = Asset.Create("THYAO", "Turk Hava Yollari", AssetType.Stock, "TRY", "BIST", "Industrials", "THYAO.IS");

        // Act
        asset.Deactivate();

        // Assert
        Assert.False(asset.IsActive);
        Assert.NotNull(asset.DeletedAt);
        Assert.True(asset.IsDeleted);
        Assert.Single(asset.DomainEvents);
        Assert.IsType<AssetPriceUpdatedEvent>(asset.DomainEvents.Single());
    }

    [Fact]
    public void Deactivate_WhenAssetIsAlreadyInactive_ShouldThrowDomainException()
    {
        // Arrange
        var asset = Asset.Create("THYAO", "Turk Hava Yollari", AssetType.Stock, "TRY", "BIST", "Industrials", "THYAO.IS");
        asset.Deactivate();

        // Act
        var action = asset.Deactivate;

        // Assert
        var exception = Assert.Throws<DomainException>(action);
        Assert.Equal("Asset 'THYAO' is already inactive.", exception.Message);
    }

    [Fact]
    public void Reactivate_AfterDeactivate_ShouldActivateAssetClearDeletedAtAndUpdateTimestamp()
    {
        // Arrange
        var asset = Asset.Create("BTC", "Bitcoin", AssetType.Crypto, "USD", "BINANCE", null, "BTCUSDT");
        asset.Deactivate();
        var previousUpdatedAt = asset.UpdatedAt;

        // Act
        asset.Reactivate();

        // Assert
        Assert.True(asset.IsActive);
        Assert.Null(asset.DeletedAt);
        Assert.False(asset.IsDeleted);
        Assert.True(asset.UpdatedAt >= previousUpdatedAt);
    }

    [Fact]
    public void Reactivate_WhenAlreadyActive_ShouldRemainActiveAndRefreshUpdatedAt()
    {
        // Arrange
        var asset = Asset.Create("BTC", "Bitcoin", AssetType.Crypto, "USD", "BINANCE", null, "BTCUSDT");
        var previousUpdatedAt = asset.UpdatedAt;

        // Act
        asset.Reactivate();

        // Assert
        Assert.True(asset.IsActive);
        Assert.Null(asset.DeletedAt);
        Assert.True(asset.UpdatedAt >= previousUpdatedAt);
    }

    [Fact]
    public void Deactivate_ShouldAddAssetPriceUpdatedEventWithExpectedPayload()
    {
        // Arrange
        var asset = Asset.Create("BTC", "Bitcoin", AssetType.Crypto, "USD", "BINANCE", null, "BTCUSDT");

        // Act
        asset.Deactivate();

        // Assert
        var domainEvent = Assert.Single(asset.DomainEvents);
        var priceUpdatedEvent = Assert.IsType<AssetPriceUpdatedEvent>(domainEvent);
        Assert.Equal(asset.Id, priceUpdatedEvent.AssetId);
        Assert.Equal("BTC", priceUpdatedEvent.Symbol);
        Assert.Equal(0m, priceUpdatedEvent.NewPrice);
        Assert.Equal(0m, priceUpdatedEvent.PreviousPrice);
        Assert.Equal("USD", priceUpdatedEvent.Currency);
        Assert.Equal(0m, priceUpdatedEvent.Change);
        Assert.Equal(0m, priceUpdatedEvent.ChangePercentage);
    }

    [Fact]
    public void Create_ShouldNotAddAnyDomainEvent()
    {
        // Arrange
        const string symbol = "  eth  ";

        // Act
        var asset = Asset.Create(symbol, "Ethereum", AssetType.Crypto, "usd", "binance", null, "ETHUSDT");

        // Assert
        Assert.Empty(asset.DomainEvents);
    }
}
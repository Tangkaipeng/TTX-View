using System.Text.Json.Serialization;

namespace TTXView.Wpf;

public sealed class AppConfig
{
    [JsonPropertyName("refresh_seconds")]
    public int RefreshSeconds { get; set; } = 10;

    [JsonPropertyName("appearance")]
    public AppearanceConfig Appearance { get; set; } = new();

    [JsonPropertyName("categories")]
    public List<string> Categories { get; set; } = new() { "默认", "贵金属", "A股", "基金" };

    [JsonPropertyName("symbols")]
    public List<SymbolItem> Symbols { get; set; } = new();
}

public sealed class AppearanceConfig
{
    [JsonPropertyName("opacity")]
    public double Opacity { get; set; } = 0.94;

    [JsonPropertyName("theme")]
    public string Theme { get; set; } = "dark";

    [JsonPropertyName("always_on_top")]
    public bool AlwaysOnTop { get; set; } = true;
}

public sealed class SymbolItem
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "a_stock";

    [JsonPropertyName("category")]
    public string Category { get; set; } = "默认";
}

public sealed class QuoteItem
{
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public string Category { get; init; } = "默认";
    public double? Price { get; init; }
    public double? Change { get; init; }
    public double? Percent { get; init; }
    public string TimeText { get; init; } = "";
    public bool Ok { get; init; } = true;
}

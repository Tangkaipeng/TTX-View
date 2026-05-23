using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TTXView.Wpf;

public sealed class ConfigStore
{
    private const string DefaultCategory = "默认";
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string ConfigPath { get; }

    public ConfigStore()
    {
        ConfigPath = Path.Combine(AppContext.BaseDirectory, "config.json");
    }

    public AppConfig Load()
    {
        if (!File.Exists(ConfigPath))
        {
            var fallback = FindWorkspaceConfig();
            if (fallback is not null)
            {
                File.Copy(fallback, ConfigPath, overwrite: true);
            }
        }

        if (!File.Exists(ConfigPath))
        {
            var created = CreateDefaultConfig();
            Save(created);
            return created;
        }

        try
        {
            var json = File.ReadAllText(ConfigPath);
            var config = JsonSerializer.Deserialize<AppConfig>(json, _jsonOptions) ?? CreateDefaultConfig();
            Normalize(config);
            return config;
        }
        catch
        {
            var config = CreateDefaultConfig();
            Save(config);
            return config;
        }
    }

    public void Save(AppConfig config)
    {
        Normalize(config);
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(config, _jsonOptions));
    }

    private static string? FindWorkspaceConfig()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 6 && directory is not null; i++)
        {
            var candidate = Path.Combine(directory.FullName, "config.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }
            directory = directory.Parent;
        }
        return null;
    }

    private static AppConfig CreateDefaultConfig() => new()
    {
        RefreshSeconds = 10,
        Appearance = new AppearanceConfig { Opacity = 0.94, Theme = "dark", AlwaysOnTop = true },
        Categories = new List<string> { DefaultCategory, "贵金属", "A股", "基金" },
        Symbols = new List<SymbolItem>
        {
            new() { Code = "hf_XAU", Name = "现货黄金", Type = "metal", Category = "贵金属" },
            new() { Code = "hf_XAG", Name = "现货白银", Type = "metal", Category = "贵金属" },
            new() { Code = "sh000001", Name = "上证指数", Type = "a_stock", Category = "A股" }
        }
    };

    private static void Normalize(AppConfig config)
    {
        config.RefreshSeconds = Math.Clamp(config.RefreshSeconds, 1, 60);
        config.Appearance ??= new AppearanceConfig();
        config.Appearance.Opacity = Math.Clamp(config.Appearance.Opacity, 0.35, 1.0);
        config.Appearance.Theme = config.Appearance.Theme == "light" ? "light" : "dark";
        config.Categories ??= new List<string>();
        config.Symbols ??= new List<SymbolItem>();

        var categories = new List<string>();
        foreach (var category in config.Categories.Where(c => !string.IsNullOrWhiteSpace(c)))
        {
            AddUnique(categories, category.Trim());
        }

        if (!categories.Contains(DefaultCategory))
        {
            categories.Insert(0, DefaultCategory);
        }

        foreach (var symbol in config.Symbols)
        {
            symbol.Code = symbol.Code.Trim();
            symbol.Name = string.IsNullOrWhiteSpace(symbol.Name) ? symbol.Code : symbol.Name.Trim();
            symbol.Type = string.IsNullOrWhiteSpace(symbol.Type) ? InferType(symbol.Code) : symbol.Type.Trim();
            symbol.Category = string.IsNullOrWhiteSpace(symbol.Category) ? InferCategory(symbol) : symbol.Category.Trim();
            AddUnique(categories, symbol.Category);
        }

        config.Categories = categories;
    }

    private static string InferType(string code)
    {
        if (code.StartsWith("hf_", StringComparison.OrdinalIgnoreCase))
        {
            return "metal";
        }
        if (code.StartsWith("f_", StringComparison.OrdinalIgnoreCase) || code.StartsWith("of", StringComparison.OrdinalIgnoreCase))
        {
            return "fund";
        }
        return "a_stock";
    }

    private static string InferCategory(SymbolItem symbol) => symbol.Type switch
    {
        "metal" => "贵金属",
        "fund" => "基金",
        _ => "A股"
    };

    private static void AddUnique(List<string> values, string value)
    {
        if (!values.Contains(value))
        {
            values.Add(value);
        }
    }
}

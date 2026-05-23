using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;

namespace TTXView.Wpf;

public sealed class MarketDataService
{
    private static readonly Regex PayloadRegex = new("var hq_str_([a-zA-Z0-9_]+)=\"(.*?)\";", RegexOptions.Compiled);
    private static readonly Regex SuggestRegex = new("var\\s+suggestdata=\"(.*?)\";", RegexOptions.Compiled);
    private static readonly Encoding SinaEncoding;
    private readonly HttpClient _client = new();

    static MarketDataService()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        SinaEncoding = Encoding.GetEncoding("GB18030");
    }

    public MarketDataService()
    {
        _client.DefaultRequestHeaders.Referrer = new Uri("https://finance.sina.com.cn");
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("TTXView/1.0");
    }

    public async Task<List<QuoteItem>> FetchAsync(IReadOnlyList<SymbolItem> symbols, CancellationToken cancellationToken = default)
    {
        if (symbols.Count == 0)
        {
            return new List<QuoteItem>();
        }

        try
        {
            var codes = string.Join(",", symbols.Select(symbol => symbol.Code));
            var bytes = await _client.GetByteArrayAsync($"https://hq.sinajs.cn/list={codes}", cancellationToken);
            var payload = SinaEncoding.GetString(bytes);
            var rawByCode = PayloadRegex.Matches(payload).ToDictionary(match => match.Groups[1].Value, match => match.Groups[2].Value);

            return symbols.Select(symbol =>
            {
                if (!rawByCode.TryGetValue(symbol.Code, out var raw) || string.IsNullOrWhiteSpace(raw))
                {
                    return Empty(symbol, "无数据");
                }
                return symbol.Type switch
                {
                    "metal" => ParseMetal(symbol, raw),
                    "fund" => ParseFund(symbol, raw),
                    _ => ParseAStock(symbol, raw)
                };
            }).ToList();
        }
        catch
        {
            return symbols.Select(symbol => Empty(symbol, "连接失败")).ToList();
        }
    }

    public async Task<SymbolItem?> ResolveSymbolAsync(string input, CancellationToken cancellationToken = default)
    {
        var direct = ResolveDirectSymbol(input);
        if (direct is not null)
        {
            return direct;
        }

        return await SearchSymbolAsync(input, cancellationToken);
    }

    public SymbolItem ResolveSymbol(string input) => ResolveDirectSymbol(input) ?? new SymbolItem
    {
        Code = input.Trim(),
        Name = input.Trim(),
        Type = "a_stock",
        Category = "默认"
    };

    private async Task<SymbolItem?> SearchSymbolAsync(string input, CancellationToken cancellationToken)
    {
        var text = input.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            var keyword = Uri.EscapeDataString(text);
            var bytes = await _client.GetByteArrayAsync($"https://suggest3.sinajs.cn/suggest/type=11,12&key={keyword}&name=suggestdata", cancellationToken);
            var payload = SinaEncoding.GetString(bytes);
            var match = SuggestRegex.Match(payload);
            if (!match.Success)
            {
                return null;
            }

            var entries = match.Groups[1].Value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var entry in entries)
            {
                var fields = entry.Split(',');
                if (fields.Length < 4)
                {
                    continue;
                }

                var code = fields[3].Trim().ToLowerInvariant();
                if (!Regex.IsMatch(code, @"^(sh|sz)\d{6}$"))
                {
                    continue;
                }

                var name = fields.Length > 4 && !string.IsNullOrWhiteSpace(fields[4]) ? fields[4].Trim() : fields[0].Trim();
                return new SymbolItem
                {
                    Code = code,
                    Name = string.IsNullOrWhiteSpace(name) ? code : name,
                    Type = "a_stock",
                    Category = "默认"
                };
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static SymbolItem? ResolveDirectSymbol(string input)
    {
        var text = input.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (text.Equals("XAU", StringComparison.OrdinalIgnoreCase) || text.Equals("HF_XAU", StringComparison.OrdinalIgnoreCase))
        {
            return new SymbolItem { Code = "hf_XAU", Name = "现货黄金", Type = "metal", Category = "默认" };
        }
        if (text.Equals("XAG", StringComparison.OrdinalIgnoreCase) || text.Equals("HF_XAG", StringComparison.OrdinalIgnoreCase))
        {
            return new SymbolItem { Code = "hf_XAG", Name = "现货白银", Type = "metal", Category = "默认" };
        }

        if (Regex.IsMatch(text, @"^f_\d{6}$", RegexOptions.IgnoreCase))
        {
            return new SymbolItem { Code = text.ToLowerInvariant(), Name = text.ToLowerInvariant(), Type = "fund", Category = "默认" };
        }

        if (Regex.IsMatch(text, @"^\d{6}$"))
        {
            if (text.StartsWith('5'))
            {
                return new SymbolItem { Code = $"f_{text}", Name = text, Type = "fund", Category = "默认" };
            }
            var prefix = text.StartsWith('6') || text.StartsWith('9') ? "sh" : "sz";
            return new SymbolItem { Code = $"{prefix}{text}", Name = text, Type = "a_stock", Category = "默认" };
        }

        if (Regex.IsMatch(text, @"^(sh|sz)\d{6}$", RegexOptions.IgnoreCase))
        {
            return new SymbolItem { Code = text.ToLowerInvariant(), Name = text.ToLowerInvariant(), Type = "a_stock", Category = "默认" };
        }

        return null;
    }

    private static QuoteItem ParseAStock(SymbolItem symbol, string raw)
    {
        var fields = raw.Split(',');
        var name = GetName(fields, 0, symbol.Name);
        var prevClose = GetDouble(fields, 2);
        var price = GetDouble(fields, 3);
        var time = fields.Length > 31 ? $"{fields[30]} {fields[31]}".Trim() : "";
        return BuildQuote(symbol, price, prevClose, time, name);
    }

    private static QuoteItem ParseMetal(SymbolItem symbol, string raw)
    {
        var fields = raw.Split(',');
        var price = GetDouble(fields, 0);
        var prevClose = GetDouble(fields, 7);
        var date = fields.Length > 12 ? fields[12] : "";
        var time = fields.Length > 6 ? fields[6] : "";
        return BuildQuote(symbol, price, prevClose, $"{date} {time}".Trim());
    }

    private static QuoteItem ParseFund(SymbolItem symbol, string raw)
    {
        var fields = raw.Split(',');
        var name = GetName(fields, 0, symbol.Name);
        var price = GetDouble(fields, 1);
        var percent = GetDouble(fields, 2);
        double? prevClose = null;
        if (price is not null && percent is not null && Math.Abs(percent.Value + 100) > 0.001)
        {
            prevClose = price.Value / (1 + percent.Value / 100);
        }
        var time = fields.Length > 4 ? fields[4] : "";
        return BuildQuote(symbol, price, prevClose, time, name);
    }

    private static QuoteItem BuildQuote(SymbolItem symbol, double? price, double? prevClose, string time, string? name = null)
    {
        var displayName = string.IsNullOrWhiteSpace(name) ? symbol.Name : name.Trim();
        if (price is null || prevClose is null || Math.Abs(prevClose.Value) < 0.000001)
        {
            return new QuoteItem
            {
                Code = symbol.Code,
                Name = displayName,
                Category = symbol.Category,
                Price = price,
                TimeText = time,
                Ok = false
            };
        }

        var change = price.Value - prevClose.Value;
        return new QuoteItem
        {
            Code = symbol.Code,
            Name = displayName,
            Category = symbol.Category,
            Price = price,
            Change = change,
            Percent = change / prevClose.Value * 100,
            TimeText = time
        };
    }

    private static QuoteItem Empty(SymbolItem symbol, string time) => new()
    {
        Code = symbol.Code,
        Name = symbol.Name,
        Category = symbol.Category,
        TimeText = time,
        Ok = false
    };

    private static double? GetDouble(string[] fields, int index)
    {
        if (index >= fields.Length)
        {
            return null;
        }
        return double.TryParse(fields[index], NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    private static string GetName(string[] fields, int index, string fallback)
    {
        if (index >= fields.Length)
        {
            return fallback;
        }

        var name = fields[index].Trim();
        return string.IsNullOrWhiteSpace(name) ? fallback : name;
    }
}

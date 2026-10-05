using DailyStockSummary.Core.Exceptions;
using DailyStockSummary.Core.Models;

namespace DailyStockSummary.Tests.Core;

public class SymbolTests
{
    [Theory]
    [InlineData("tsla", "TSLA")]
    [InlineData("  vod.l  ", "VOD.L")] // London listing; surrounding whitespace from a text box
    [InlineData("^gspc", "^GSPC")] // index
    [InlineData("eurusd=x", "EURUSD=X")] // currency pair
    [InlineData("btc-usd", "BTC-USD")] // crypto
    [InlineData("0700.hk", "0700.HK")] // numeric ticker (Tencent)
    [InlineData("abcdefghijklmno", "ABCDEFGHIJKLMNO")] // 15 chars: the maximum
    public void Parse_trims_and_upper_cases_valid_symbols(string raw, string expected)
    {
        Assert.Equal(expected, Symbol.Parse(raw).Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!!!")]
    [InlineData("TS LA")]
    [InlineData("../etc")] // path traversal attempt
    [InlineData("TSLA?interval=1d")] // query-string injection into the upstream URL
    [InlineData("abcdefghijklmnop")] // 16 chars: one over the maximum
    [InlineData("..")] // dot-only symbols collapse into path segments in the upstream URL
    [InlineData("ſpy")] // LATIN SMALL LETTER LONG S would upper-case to ASCII S
    public void Parse_rejects_invalid_symbols(string? raw)
    {
        Assert.Throws<InvalidSymbolException>(() => Symbol.Parse(raw));
    }
}

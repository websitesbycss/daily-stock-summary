using DailyStockSummary.Core.Exceptions;
using DailyStockSummary.Core.Models;

namespace DailyStockSummary.Tests.Core;

public class SymbolTests
{
    [Theory]
    [InlineData("tsla", "TSLA")]
    [InlineData("TSLA", "TSLA")]
    [InlineData("  brk.b  ", "BRK.B")]
    [InlineData("^gspc", "^GSPC")]
    [InlineData("eurusd=x", "EURUSD=X")]
    [InlineData("btc-usd", "BTC-USD")]
    [InlineData("abcdefghijklmno", "ABCDEFGHIJKLMNO")] // 15 chars: the maximum
    [InlineData("0700.hk", "0700.HK")]
    [InlineData("005930.ks", "005930.KS")]
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
    [InlineData("../etc")]
    [InlineData("TSLA/../x")]
    [InlineData("TSLA?interval=1d")]
    [InlineData("abcdefghijklmnop")] // 16 chars: one over the maximum
    [InlineData(".")] // dot-only symbols collapse into path segments in the upstream URL
    [InlineData("..")]
    [InlineData("^")]
    [InlineData("-")]
    [InlineData("=")]
    [InlineData("\u017Fpy")] // LATIN SMALL LETTER LONG S would upper-case to ASCII S
    [InlineData("\u0131bm")] // LATIN SMALL LETTER DOTLESS I would upper-case to ASCII I
    public void Parse_rejects_invalid_symbols(string? raw)
    {
        Assert.Throws<InvalidSymbolException>(() => Symbol.Parse(raw));
    }

    [Fact]
    public void Symbols_with_the_same_normalized_value_are_equal()
    {
        Assert.Equal(Symbol.Parse("tsla"), Symbol.Parse(" TSLA "));
    }
}

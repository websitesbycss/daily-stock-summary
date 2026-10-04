using System.Text.RegularExpressions;
using DailyStockSummary.Core.Exceptions;

namespace DailyStockSummary.Core.Models;

/// <summary>A validated, upper-cased ticker symbol (e.g. TSLA, BRK.B, ^GSPC, EURUSD=X).</summary>
public sealed partial record Symbol
{
    private Symbol(string value) => Value = value;

    public string Value { get; }

    public static Symbol Parse(string? raw)
    {
        var candidate = raw?.Trim();

        // Validate before upper-casing: some non-ASCII letters (e.g. U+017F) upper-case to ASCII ones.
        if (string.IsNullOrEmpty(candidate) || !ValidSymbol().IsMatch(candidate))
        {
            throw new InvalidSymbolException(
                "Symbol must be 1-15 characters: letters, digits, '.', '-', '^' or '=', with at least one letter or digit.");
        }

        return new Symbol(candidate.ToUpperInvariant());
    }

    public override string ToString() => Value;

    // The lookahead rejects dot-only symbols such as ".." that would alter the upstream URL path.
    [GeneratedRegex(@"^(?=.*[A-Za-z0-9])[A-Za-z0-9.\-^=]{1,15}$", RegexOptions.CultureInvariant)]
    private static partial Regex ValidSymbol();
}

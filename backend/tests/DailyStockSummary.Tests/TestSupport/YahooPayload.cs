namespace DailyStockSummary.Tests.TestSupport;

public static class YahooPayload
{
    /// <summary>Builds a Yahoo-shaped chart payload. Array arguments are raw JSON so tests can inject nulls.</summary>
    public static string Chart(
        string timestamps = "[1788442200,1788443100]",
        string low = "[10.5,10.25]",
        string high = "[11.5,11.25]",
        string volume = "[100,200]",
        string meta = """{"exchangeTimezoneName":"America/New_York","gmtoffset":-14400}""") =>
        $$$"""
        {"chart":{"result":[{"meta":{{{meta}}},"timestamp":{{{timestamps}}},
        "indicators":{"quote":[{"open":{{{low}}},"high":{{{high}}},"low":{{{low}}},"close":{{{low}}},"volume":{{{volume}}}}]}}],"error":null}}
        """;
}

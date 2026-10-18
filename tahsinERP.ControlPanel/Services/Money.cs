using System.Globalization;

namespace tahsinERP.ControlPanel.Services;

/// <summary>Pul: bazada tiyin (1 so'm = 100 tiyin), ekranda so'm.</summary>
public static class Money
{
    private static readonly NumberFormatInfo Display = new() { NumberGroupSeparator = " ", NumberDecimalSeparator = ",", NumberGroupSizes = [3] };

    public static string Format(long tiyin)
    {
        decimal som = tiyin / 100m;
        return som.ToString(tiyin % 100 == 0 ? "#,0" : "#,0.00", Display);
    }

    public static string FormatSom(long tiyin) => Format(tiyin) + " so'm";

    public static string? Format(long? tiyin) => tiyin is null ? null : Format(tiyin.Value);

    /// <summary>"1 500 000", "1500000,50", "1500000.5" → tiyin. Ko'pi bilan 2 kasr xonasi.</summary>
    public static bool TryParse(string? text, out long tiyin)
    {
        tiyin = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;
        string s = text.Replace(" ", "").Replace(" ", "").Replace("so'm", "", StringComparison.OrdinalIgnoreCase).Replace(',', '.');
        if (!decimal.TryParse(s, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out decimal som))
            return false;
        decimal t = som * 100m;
        if (t != decimal.Truncate(t) || t < 0 || t > long.MaxValue / 4)
            return false;
        tiyin = (long)t;
        return true;
    }

    /// <summary>Summa ichidagi QQS (narx QQS bilan): round(gross × r / (100 + r)).</summary>
    public static long VatIncluded(long grossTiyin, decimal ratePercent) =>
        ratePercent <= 0 ? 0 : (long)Math.Round(grossTiyin * ratePercent / (100m + ratePercent), MidpointRounding.AwayFromZero);

    public static long LineAmount(decimal quantity, long unitPriceTiyin) =>
        (long)Math.Round(quantity * unitPriceTiyin, MidpointRounding.AwayFromZero);
}

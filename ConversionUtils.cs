namespace QuickSellFlea;

public static class ConversionUtils
{
    public static float ConvertToUSD(float value)
    {
        return MathF.Round(value / 120f, 0, MidpointRounding.AwayFromZero);
    }

    public static float ConvertToEUR(float value)
    {
        return MathF.Round(value / 133f, 0, MidpointRounding.AwayFromZero);
    }
}

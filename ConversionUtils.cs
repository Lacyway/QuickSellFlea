namespace QuickSellFlea;

public static class ConversionUtils
{
    public const string RoubleTpl = "5449016a4bdc2d6f028b456f";
    public const string DollarTpl = "5696686a4bdc2da3298b456a";
    public const string EuroTpl = "569668774bdc2da2298b4568";

    public static float ConvertToUSD(float value)
    {
        return MathF.Round(value / 120f, 0, MidpointRounding.AwayFromZero);
    }

    public static float ConvertToEUR(float value)
    {
        return MathF.Round(value / 133f, 0, MidpointRounding.AwayFromZero);
    }
}

namespace RogueLikeNet.Client.Core.Rendering;

public static class RenderingHelpers
{
    public static string Truncate(string text, int maxLen)
        => text.Length <= maxLen ? text : text[..(maxLen - 2)] + "..";

    public static string FormatUnixMs(long unixMs)
    {
        if (unixMs <= 0) return "Never";
        var dt = DateTimeOffset.FromUnixTimeMilliseconds(unixMs);
        return dt.LocalDateTime.ToString("yyyy-MM-dd HH:mm");
    }
}

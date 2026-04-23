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

    public static IEnumerable<string> WrapText(string text, int width)
    {
        if (string.IsNullOrEmpty(text) || width <= 0) yield break;
        var words = text.Split(' ');
        var line = new System.Text.StringBuilder();
        foreach (var w in words)
        {
            if (line.Length == 0)
            {
                line.Append(w);
            }
            else if (line.Length + 1 + w.Length <= width)
            {
                line.Append(' ').Append(w);
            }
            else
            {
                yield return line.ToString();
                line.Clear();
                line.Append(w);
            }
        }
        if (line.Length > 0) yield return line.ToString();
    }
}

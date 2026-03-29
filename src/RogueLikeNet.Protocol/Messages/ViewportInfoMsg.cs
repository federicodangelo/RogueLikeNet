using MessagePack;

namespace RogueLikeNet.Protocol.Messages;

/// <summary>
/// Client → Server: informs the server how many chunks are visible in the client's viewport.
/// Sent whenever the viewport size changes (window resize, zoom level change).
/// </summary>
[MessagePackObject]
public class ViewportInfoMsg
{
    /// <summary>Number of distinct chunks visible in the client viewport. Capped at 100.</summary>
    [Key(0)] public int VisibleChunks { get; set; }
}

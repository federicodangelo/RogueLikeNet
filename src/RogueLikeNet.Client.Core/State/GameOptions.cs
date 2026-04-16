using Engine.Platform;

namespace RogueLikeNet.Client.Core.State;

/// <summary>
/// User-configurable game options that persist between sessions.
/// </summary>
public sealed class GameOptions
{
    private const string ShowStatsKey = "option.showStats";
    private const string DebugEnabledKey = "option.debugEnabled";
    private const string UsernameKey = "login.username";

    /// <summary>Whether the performance stats overlay (FPS, latency, bandwidth) is shown during gameplay.</summary>
    public bool ShowStats { get; set; } = true;

    public void Load(ISettings settings)
    {
        ShowStats = settings.Load(ShowStatsKey) == "true";
    }

    public void Save(ISettings settings)
    {
        settings.Save(ShowStatsKey, ShowStats ? "true" : "false");
    }

    public void LoadDebugEnabled(ISettings settings, DebugSettings debug)
    {
        debug.Enabled = settings.Load(DebugEnabledKey) == "true";
    }

    public void SaveDebugEnabled(ISettings settings, DebugSettings debug)
    {
        settings.Save(DebugEnabledKey, debug.Enabled ? "true" : "false");
    }

    public string LoadUsername(ISettings settings)
    {
        return settings.Load(UsernameKey) ?? "";
    }

    public void SaveUsername(ISettings settings, string username)
    {
        settings.Save(UsernameKey, username);
    }
}

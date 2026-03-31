using Engine.Core;

namespace RogueLikeNet.Client.Core.Rendering;

/// <summary>
/// Centralized color palette for all UI rendering.
/// </summary>
public static class RenderingTheme
{
    // General UI
    public static readonly Color4 Border = new(180, 180, 180, 255);
    public static readonly Color4 Title = new(255, 200, 50, 255);
    public static readonly Color4 Normal = new(180, 180, 180, 255);
    public static readonly Color4 Selected = new(255, 255, 255, 255);
    public static readonly Color4 Dim = new(100, 100, 100, 255);
    public static readonly Color4 Black = new(0, 0, 0, 255);
    public static readonly Color4 Overlay = new(0, 0, 0, 160);
    public static readonly Color4 OverlayBg = new(0, 0, 0, 180);

    // HP bar
    public static readonly Color4 HpBar = new(220, 50, 50, 255);
    public static readonly Color4 HpFill = new(0, 200, 0, 255);
    public static readonly Color4 HpText = new(255, 80, 80, 255);

    // Stats / Level
    public static readonly Color4 Stats = new(200, 200, 200, 255);
    public static readonly Color4 Level = new(255, 255, 100, 255);

    // Items / Inventory
    public static readonly Color4 Item = new(200, 180, 100, 255);
    public static readonly Color4 Inv = new(150, 200, 255, 255);
    public static readonly Color4 InvSel = new(255, 255, 80, 255);
    public static readonly Color4 Floor = new(150, 220, 130, 255);

    // Skills
    public static readonly Color4 SkillReady = new(100, 255, 100, 255);
    public static readonly Color4 SkillCd = new(128, 128, 128, 255);

    // Performance overlay
    public static readonly Color4 Fps = new(0, 255, 0, 255);
    public static readonly Color4 Latency = new(255, 200, 50, 255);

    // Chat
    public static readonly Color4 ChatBg = new(0, 0, 0, 160);
    public static readonly Color4 ChatText = new(200, 200, 200, 255);
    public static readonly Color4 ChatInput = new(255, 255, 100, 255);

    // Class select screen
    public static readonly Color4 ClassHighlight = new(100, 200, 255, 255);
    public static readonly Color4 ClassBorder = new(80, 80, 120, 255);
    public static readonly Color4 StatLabel = new(160, 160, 160, 255);
    public static readonly Color4 StatPositive = new(100, 255, 100, 255);
    public static readonly Color4 StatNegative = new(255, 100, 100, 255);
    public static readonly Color4 StatZero = new(120, 120, 120, 255);
    public static readonly Color4 SkillName = new(200, 180, 100, 255);
    public static readonly Color4 NameField = new(255, 220, 100, 255);

    // HUD panel background
    public static readonly Color4 HudBg = new(15, 15, 20, 255);

    // Save slot screen
    public static readonly Color4 SlotActive = new(100, 200, 255, 255);
    public static readonly Color4 SlotDate = new(140, 140, 140, 255);
    public static readonly Color4 SlotEmpty = new(80, 80, 80, 255);
    public static readonly Color4 Danger = new(255, 80, 80, 255);

    // Rarity colors
    public static readonly Color4 RarityCommon = new(180, 180, 180, 255);
    public static readonly Color4 RarityUncommon = new(30, 255, 30, 255);
    public static readonly Color4 RarityRare = new(80, 140, 255, 255);
    public static readonly Color4 RarityEpic = new(180, 80, 255, 255);
    public static readonly Color4 RarityLegendary = new(255, 165, 0, 255);
}

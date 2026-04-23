using Engine.Core;
using Engine.Platform;
using RogueLikeNet.Core.Generation;

namespace RogueLikeNet.Client.Core.Rendering.Menus;

public sealed class NewGameRenderer
{
    public void RenderNewGame(ISpriteRenderer r, int totalCols, int totalRows, int selectedIndex,
        string slotName, long worldSeed, int generatorIndex, bool seedEditing, string seedEditText,
        bool nameEditing, string nameEditText)
    {
        r.DrawRectScreen(0, 0, totalCols * AsciiDraw.TileWidth, totalRows * AsciiDraw.TileHeight, RenderingTheme.Black);

        int boxW = 46;
        int boxH = 16;
        int bx = (totalCols - boxW) / 2;
        int by = (totalRows - boxH) / 2;

        AsciiDraw.DrawBox(r, bx, by, boxW, boxH, RenderingTheme.Border, new Color4(10, 10, 15, 255));

        AsciiDraw.DrawCentered(r, totalCols, by + 1, "NEW GAME", RenderingTheme.Title);

        int sepY = by + 2;
        for (int i = bx + 2; i < bx + boxW - 2; i++)
            AsciiDraw.DrawChar(r, i, sepY, '\u2500', RenderingTheme.Dim);

        int itemStartY = sepY + 2;
        string nameDisplay = nameEditing ? nameEditText + "_" : (slotName.Length > 0 ? slotName : "");
        string seedDisplay = seedEditing ? seedEditText + "_" : worldSeed.ToString();
        string genName = GeneratorRegistry.GetName(generatorIndex);
        string[] labels =
        [
            "Name: " + nameDisplay,
            "Seed: " + seedDisplay,
            "Generator: \u25c4 " + genName + " \u25ba",
            "Randomize Seed",
            slotName.Length > 0 ? "Start" : "Start (name required)",
        ];

        int tx = bx + 6;
        for (int i = 0; i < labels.Length; i++)
        {
            bool sel = i == selectedIndex;
            string prefix = sel ? " \u25ba " : "   ";
            string label = prefix + labels[i];
            var color = sel ? RenderingTheme.Selected : RenderingTheme.Normal;
            if (i == 4 && slotName.Length == 0)
                color = sel ? RenderingTheme.Dim : RenderingTheme.Dim;
            AsciiDraw.DrawString(r, tx, itemStartY + i, label, color);
        }

        string footer;
        if (nameEditing)
            footer = "Type name   Enter Next   Esc Back";
        else if (seedEditing)
            footer = "Type seed   Enter Next   Esc Back";
        else if (selectedIndex == 2)
            footer = "\u2190\u2192 Change Generator   \u2191\u2193 Navigate";
        else
            footer = "\u2191\u2193 Navigate   Enter Select   Esc Back";
        AsciiDraw.DrawCentered(r, totalCols, by + boxH - 2, footer, RenderingTheme.Dim);
    }
}

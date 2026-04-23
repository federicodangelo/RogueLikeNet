using Engine.Platform;
using RogueLikeNet.Core.Definitions;

namespace RogueLikeNet.Client.Core.Rendering;

public sealed class ClassSelectRenderer
{
    public void RenderClassSelect(ISpriteRenderer r, int totalCols, int totalRows,
        int selectedClassIndex, string playerName, bool nameEditing, string nameEditText, bool canEditName)
    {
        r.DrawRectScreen(0, 0, totalCols * AsciiDraw.TileWidth, totalRows * AsciiDraw.TileHeight, RenderingTheme.Black);

        int classCount = ClassDefinitions.NumClasses;
        int cardW = 18;
        int cardH = 18;
        int gap = 2;
        int totalW = classCount * cardW + (classCount - 1) * gap;
        int startX = (totalCols - totalW) / 2;

        AsciiDraw.DrawCentered(r, totalCols, 1, "SELECT YOUR CLASS", RenderingTheme.Title);

        int nameY = 3;
        string nameDisplay = nameEditing ? nameEditText + "_" : playerName;
        string nameLabel = $"Name: {nameDisplay}";
        AsciiDraw.DrawCentered(r, totalCols, nameY, nameLabel, nameEditing ? RenderingTheme.Selected : RenderingTheme.NameField);
        if (!nameEditing && canEditName)
            AsciiDraw.DrawCentered(r, totalCols, nameY + 1, "(T to edit name)", RenderingTheme.Dim);

        int cardStartY = nameY + 3;

        for (int i = 0; i < classCount; i++)
        {
            int cx = startX + i * (cardW + gap);
            bool selected = i == selectedClassIndex;
            var borderColor = selected ? RenderingTheme.ClassHighlight : RenderingTheme.ClassBorder;

            AsciiDraw.DrawBox(r, cx, cardStartY, cardW, cardH, borderColor);

            var classDef = ClassDefinitions.All[i];
            var stats = ClassDefinitions.GetStartingStats(i);

            int nameX = cx + (cardW - classDef.Name.Length) / 2;
            AsciiDraw.DrawString(r, nameX, cardStartY + 1, classDef.Name, selected ? RenderingTheme.ClassHighlight : RenderingTheme.Title);

            var art = ClassDefinitions.GetAsciiArt(i);
            for (int line = 0; line < art.Length; line++)
            {
                int artX = cx + (cardW - art[line].Length) / 2;
                AsciiDraw.DrawString(r, artX, cardStartY + 3 + line, art[line], selected ? RenderingTheme.Selected : RenderingTheme.Normal);
            }

            int statsY = cardStartY + 3 + art.Length + 1;
            AsciiDraw.DrawStatLine(r, cx + 2, statsY, "ATK", stats.Attack, cardW - 4);
            AsciiDraw.DrawStatLine(r, cx + 2, statsY + 1, "DEF", stats.Defense, cardW - 4);
            AsciiDraw.DrawStatLine(r, cx + 2, statsY + 2, "HP", stats.Health, cardW - 4);
            AsciiDraw.DrawStatLine(r, cx + 2, statsY + 3, "SPD", stats.Speed, cardW - 4);

            if (selected)
            {
                int arrowY = cardStartY + cardH / 2;
                if (cx > 1)
                    AsciiDraw.DrawChar(r, cx - 1, arrowY, '\u25ba', RenderingTheme.ClassHighlight);
            }
        }

        int footerY = cardStartY + cardH + 1;
        string footer = nameEditing
            ? "Type name   Enter Confirm   Esc Cancel"
            : $"\u2190\u2192 Select Class {(canEditName ? " T Edit Name " : " ")} Enter Confirm   Esc Back";
        AsciiDraw.DrawCentered(r, totalCols, footerY, footer, RenderingTheme.Dim);
    }
}

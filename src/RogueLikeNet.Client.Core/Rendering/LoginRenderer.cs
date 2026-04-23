using Engine.Core;
using Engine.Platform;

namespace RogueLikeNet.Client.Core.Rendering;

public sealed class LoginRenderer
{
    public void RenderLogin(ISpriteRenderer r, int totalCols, int totalRows,
        string userName, string password, int selectedField, bool isEditing, string editText, string? errorMessage)
    {
        r.DrawRectScreen(0, 0, totalCols * AsciiDraw.TileWidth, totalRows * AsciiDraw.TileHeight, RenderingTheme.Black);

        int boxW = 70;
        int boxH = errorMessage != null ? 18 : 16;
        int bx = (totalCols - boxW) / 2;
        int by = (totalRows - boxH) / 2;

        AsciiDraw.DrawBox(r, bx, by, boxW, boxH, RenderingTheme.Border, new Color4(10, 10, 15, 255));

        AsciiDraw.DrawCentered(r, totalCols, by + 2, "L O G I N", RenderingTheme.Title);

        int sepY = by + 4;
        for (int i = bx + 2; i < bx + boxW - 2; i++)
            AsciiDraw.DrawChar(r, i, sepY, '\u2500', RenderingTheme.Dim);

        int fieldX = bx + 6;
        int nameY = sepY + 2;
        int passY = nameY + 3;

        // Username field
        bool nameSelected = selectedField == 0;
        bool nameActive = nameSelected && isEditing;
        string nameDisplay = nameActive ? editText + "_" : userName;
        string nameLabel = "Username: " + nameDisplay;
        AsciiDraw.DrawString(r, fieldX, nameY, nameLabel, nameActive ? RenderingTheme.Selected : (nameSelected ? RenderingTheme.ClassHighlight : RenderingTheme.Normal));

        // Password field
        bool passSelected = selectedField == 1;
        bool passActive = passSelected && isEditing;
        string maskedPass = passActive ? new string('*', editText.Length) + "_" : new string('*', password.Length);
        string passLabel = "Password: " + maskedPass + (password.Length == 0 && !passActive ? " (optional)" : "");
        AsciiDraw.DrawString(r, fieldX, passY, passLabel, passActive ? RenderingTheme.Selected : (passSelected ? RenderingTheme.ClassHighlight : RenderingTheme.Normal));

        // Error message
        if (errorMessage != null)
        {
            int errY = passY + 3;
            string err = errorMessage.Length > boxW - 4 ? errorMessage[..(boxW - 4)] : errorMessage;
            AsciiDraw.DrawCentered(r, totalCols, errY, err, RenderingTheme.HpText);
        }

        string footer = "\u2191\u2193 Select field   Enter Next/Confirm   Tab Confirm   Esc Back";
        AsciiDraw.DrawCentered(r, totalCols, by + boxH - 2, footer, RenderingTheme.Dim);
    }
}

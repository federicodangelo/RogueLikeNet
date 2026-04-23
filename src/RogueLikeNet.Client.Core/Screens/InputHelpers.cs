using Engine.Platform;

namespace RogueLikeNet.Client.Core.Screens;

/// <summary>
/// Shared input-handling helpers used by multiple screens.
/// Consolidates patterns such as list wrap-around navigation and 4-way direction selection.
/// </summary>
public static class InputHelpers
{
    /// <summary>
    /// Handles standard list navigation with MenuUp/MenuDown (repeat-aware, wrap-around).
    /// Returns <c>true</c> if the index was updated.
    /// </summary>
    public static bool HandleListNavigation(IInputManager input, ref int index, int count)
    {
        if (count <= 0) return false;
        if (input.IsActionPressedOrRepeated(InputAction.MenuUp))
        {
            index = (index - 1 + count) % count;
            return true;
        }
        if (input.IsActionPressedOrRepeated(InputAction.MenuDown))
        {
            index = (index + 1) % count;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Reads a single-frame 4-way direction press from the Move* actions (optionally also Menu* arrows).
    /// Returns <c>true</c> if any direction was pressed; writes (dx, dy) with one non-zero axis.
    /// </summary>
    public static bool TryReadDirection(IInputManager input, out int dx, out int dy, bool includeMenuArrows = true)
    {
        dx = 0;
        dy = 0;
        if (input.IsActionPressed(InputAction.MoveUp) || (includeMenuArrows && input.IsActionPressed(InputAction.MenuUp))) dy = -1;
        else if (input.IsActionPressed(InputAction.MoveDown) || (includeMenuArrows && input.IsActionPressed(InputAction.MenuDown))) dy = 1;
        else if (input.IsActionPressed(InputAction.MoveLeft) || (includeMenuArrows && input.IsActionPressed(InputAction.MenuLeft))) dx = -1;
        else if (input.IsActionPressed(InputAction.MoveRight) || (includeMenuArrows && input.IsActionPressed(InputAction.MenuRight))) dx = 1;
        return dx != 0 || dy != 0;
    }
}

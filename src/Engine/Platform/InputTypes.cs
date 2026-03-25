namespace Engine.Platform;

public enum InputAction
{
    DebugToggle,
    MenuConfirm,
    MenuUp,
    MenuDown,
    MenuLeft,
    MenuRight,
    MenuBack,
    MenuSecondaryAction,

    MoveUp,
    MoveDown,
    MoveLeft,
    MoveRight,
    FireWeapon,
    MapZoomOut,
    MapZoomIn,
    PreviousPanelView,
    NextPanelView,
    Interact,
    ToggleMap,
    Screenshot,
    DodgeRoll,
}

public enum InputActionAxis
{
    Movement,
    Heading,
}

public enum InputMethod
{
    MouseKeyboard,
    Gamepad,
}

public enum MouseButton
{
    Left = 1,
    Middle = 2,
    Right = 3,
}

public enum MovementInputMode
{
    HeadingRelative,
    Absolute,
}

public enum TextureScaleMode
{
    Nearest,
    Linear,
}

public enum KeyCode
{
    A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
    D0, D1, D2, D3, D4, D5, D6, D7, D8, D9,
    Space, Enter, Escape, Backspace, Tab,
    Up, Down, Left, Right,
    LeftShift, RightShift, LeftControl, RightControl,
    F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
}

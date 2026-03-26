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

    // Game-specific actions
    Wait,
    Attack,
    PickUp,
    OpenInventory,
    OpenChat,
    UseItem1,
    UseItem2,
    UseItem3,
    UseItem4,
    UseSkill1,
    UseSkill2,
    Drop,
    Equip,
    UnequipSlot1,
    UnequipSlot2,
    CycleSection,
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

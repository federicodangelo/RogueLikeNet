using System.Numerics;
using Engine.Platform;
using RogueLikeNet.Client.Core.State;

namespace RogueLikeNet.Client.Core.Tests;

public class DebugSettingsTests
{
    private sealed class FakeInputManager : IInputManager
    {
        public string TextInput { get; set; } = "";
        public float MouseX => 0;
        public float MouseY => 0;
        public float MouseWheelY => 0;
        public bool QuitRequested => false;
        public int TextInputBackspacesCount => 0;
        public int TextInputReturnsCount => 0;
        public InputMethod ActiveInputMethod => InputMethod.MouseKeyboard;
        public MovementInputMode MovementMode => MovementInputMode.Absolute;
        public void BeginFrame() { }
        public void EndFrame() { }
        public void Reset() { }
        public void ProcessEvents() { }
        public bool IsActionDown(InputAction action) => false;
        public bool IsActionPressed(InputAction action) => false;
        public bool IsActionReleased(InputAction action) => false;
        public Vector2 GetActionAxisDirection(InputActionAxis axis) => Vector2.Zero;
        public string GetActionHelpText(InputAction action, bool includeSecondary = false) => "";
        public string GetActionHelpTextFull(InputAction action) => "";
        public string GetMouseButtonHelpText(MouseButton button) => "";
        public bool IsMouseDown(MouseButton button) => false;
        public bool IsMousePressed(MouseButton button) => false;
        public bool IsMouseReleased(MouseButton button) => false;
    }

    [Fact]
    public void ToggleAll_AllOn_TurnsAllOff()
    {
        var debug = new DebugSettings { Enabled = true };
        // Defaults are all true
        Assert.True(debug.VisibilityOff);
        Assert.True(debug.CollisionsOff);
        Assert.True(debug.Invulnerable);
        Assert.True(debug.LightOff);
        Assert.True(debug.MaxSpeed);

        var input = new FakeInputManager { TextInput = "Z" };
        bool synced = false;
        debug.HandleDebugKeys(input, () => synced = true);

        Assert.False(debug.VisibilityOff);
        Assert.False(debug.CollisionsOff);
        Assert.False(debug.Invulnerable);
        Assert.False(debug.LightOff);
        Assert.False(debug.MaxSpeed);
        Assert.True(synced);
    }

    [Fact]
    public void ToggleAll_AllOff_TurnsAllOn()
    {
        var debug = new DebugSettings
        {
            Enabled = true,
            VisibilityOff = false,
            CollisionsOff = false,
            Invulnerable = false,
            LightOff = false,
            MaxSpeed = false,
        };

        var input = new FakeInputManager { TextInput = "z" };
        debug.HandleDebugKeys(input, () => { });

        Assert.True(debug.VisibilityOff);
        Assert.True(debug.CollisionsOff);
        Assert.True(debug.Invulnerable);
        Assert.True(debug.LightOff);
        Assert.True(debug.MaxSpeed);
    }

    [Fact]
    public void ToggleAll_MixedState_TurnsAllOff()
    {
        var debug = new DebugSettings
        {
            Enabled = true,
            VisibilityOff = true,
            CollisionsOff = false,
            Invulnerable = true,
            LightOff = false,
            MaxSpeed = false,
        };

        var input = new FakeInputManager { TextInput = "Z" };
        debug.HandleDebugKeys(input, () => { });

        Assert.False(debug.VisibilityOff);
        Assert.False(debug.CollisionsOff);
        Assert.False(debug.Invulnerable);
        Assert.False(debug.LightOff);
        Assert.False(debug.MaxSpeed);
    }

    [Fact]
    public void HandleDebugKeys_NotEnabled_DoesNothing()
    {
        var debug = new DebugSettings { Enabled = false };
        var input = new FakeInputManager { TextInput = "v" };
        bool synced = false;
        debug.HandleDebugKeys(input, () => synced = true);

        Assert.True(debug.VisibilityOff); // unchanged default
        Assert.False(synced);
    }

    [Fact]
    public void IndividualToggle_Visibility()
    {
        var debug = new DebugSettings { Enabled = true };
        Assert.True(debug.VisibilityOff);

        var input = new FakeInputManager { TextInput = "v" };
        debug.HandleDebugKeys(input, () => { });

        Assert.False(debug.VisibilityOff);
        // Others unchanged
        Assert.True(debug.CollisionsOff);
    }
}

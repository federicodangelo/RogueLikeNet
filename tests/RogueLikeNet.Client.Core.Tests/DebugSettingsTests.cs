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
        Assert.True(debug.FreeCrafting);

        var input = new FakeInputManager { TextInput = "Z" };
        bool synced = false;
        debug.HandleDebugKeys(input, () => synced = true);

        Assert.False(debug.VisibilityOff);
        Assert.False(debug.CollisionsOff);
        Assert.False(debug.Invulnerable);
        Assert.False(debug.LightOff);
        Assert.False(debug.MaxSpeed);
        Assert.False(debug.FreeCrafting);
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
            FreeCrafting = false,
        };

        var input = new FakeInputManager { TextInput = "z" };
        debug.HandleDebugKeys(input, () => { });

        Assert.True(debug.VisibilityOff);
        Assert.True(debug.CollisionsOff);
        Assert.True(debug.Invulnerable);
        Assert.True(debug.LightOff);
        Assert.True(debug.MaxSpeed);
        Assert.True(debug.FreeCrafting);
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
            FreeCrafting = true,
        };

        var input = new FakeInputManager { TextInput = "Z" };
        debug.HandleDebugKeys(input, () => { });

        Assert.False(debug.VisibilityOff);
        Assert.False(debug.CollisionsOff);
        Assert.False(debug.Invulnerable);
        Assert.False(debug.LightOff);
        Assert.False(debug.MaxSpeed);
        Assert.False(debug.FreeCrafting);
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

    [Fact]
    public void IndividualToggle_FreeCrafting()
    {
        var debug = new DebugSettings { Enabled = true };
        Assert.True(debug.FreeCrafting);

        var input = new FakeInputManager { TextInput = "f" };
        debug.HandleDebugKeys(input, () => { });

        Assert.False(debug.FreeCrafting);
        // Others unchanged
        Assert.True(debug.CollisionsOff);
    }

    [Fact]
    public void IndividualToggle_Collisions()
    {
        var debug = new DebugSettings { Enabled = true };
        Assert.True(debug.CollisionsOff);

        var input = new FakeInputManager { TextInput = "c" };
        debug.HandleDebugKeys(input, () => { });

        Assert.False(debug.CollisionsOff);
        Assert.True(debug.VisibilityOff); // unchanged
    }

    [Fact]
    public void IndividualToggle_Invulnerable()
    {
        var debug = new DebugSettings { Enabled = true };
        Assert.True(debug.Invulnerable);

        var input = new FakeInputManager { TextInput = "h" };
        debug.HandleDebugKeys(input, () => { });

        Assert.False(debug.Invulnerable);
    }

    [Fact]
    public void IndividualToggle_LightOff()
    {
        var debug = new DebugSettings { Enabled = true };
        Assert.True(debug.LightOff);

        var input = new FakeInputManager { TextInput = "l" };
        debug.HandleDebugKeys(input, () => { });

        Assert.False(debug.LightOff);
    }

    [Fact]
    public void IndividualToggle_MaxSpeed()
    {
        var debug = new DebugSettings { Enabled = true };
        Assert.True(debug.MaxSpeed);

        var input = new FakeInputManager { TextInput = "m" };
        debug.HandleDebugKeys(input, () => { });

        Assert.False(debug.MaxSpeed);
    }

    [Fact]
    public void ZoomIn_DecreasesZoomLevel()
    {
        var debug = new DebugSettings { Enabled = true };
        Assert.Equal(0, debug.ZoomLevel);

        var input = new FakeInputManager { TextInput = "+" };
        debug.HandleDebugKeys(input, () => { });

        Assert.Equal(-1, debug.ZoomLevel);
    }

    [Fact]
    public void ZoomOut_IncreasesZoomLevel()
    {
        var debug = new DebugSettings { Enabled = true };
        var input = new FakeInputManager { TextInput = "-" };
        debug.HandleDebugKeys(input, () => { });

        Assert.Equal(1, debug.ZoomLevel);
    }

    [Fact]
    public void ZoomReset_SetsZoomToZero()
    {
        var debug = new DebugSettings { Enabled = true, ZoomLevel = 3 };
        var input = new FakeInputManager { TextInput = "0" };
        debug.HandleDebugKeys(input, () => { });

        Assert.Equal(0, debug.ZoomLevel);
    }

    [Fact]
    public void ZoomIn_ClampsAtMinusFlive()
    {
        var debug = new DebugSettings { Enabled = true, ZoomLevel = -5 };
        var input = new FakeInputManager { TextInput = "+" };
        debug.HandleDebugKeys(input, () => { });

        Assert.Equal(-5, debug.ZoomLevel); // Already at min, stays clamped
    }

    [Fact]
    public void ZoomOut_ClampsAtFive()
    {
        var debug = new DebugSettings { Enabled = true, ZoomLevel = 5 };
        var input = new FakeInputManager { TextInput = "-" };
        debug.HandleDebugKeys(input, () => { });

        Assert.Equal(5, debug.ZoomLevel); // Already at max, stays clamped
    }

    [Fact]
    public void ZoomIn_AlternateKey_Equals()
    {
        var debug = new DebugSettings { Enabled = true };
        var input = new FakeInputManager { TextInput = "=" };
        debug.HandleDebugKeys(input, () => { });

        Assert.Equal(-1, debug.ZoomLevel);
    }

    [Fact]
    public void ZoomOut_AlternateKey_Underscore()
    {
        var debug = new DebugSettings { Enabled = true };
        var input = new FakeInputManager { TextInput = "_" };
        debug.HandleDebugKeys(input, () => { });

        Assert.Equal(1, debug.ZoomLevel);
    }

    [Fact]
    public void MultipleKeys_ProcessedInSequence()
    {
        var debug = new DebugSettings { Enabled = true };
        var input = new FakeInputManager { TextInput = "vc" };
        int syncCount = 0;
        debug.HandleDebugKeys(input, () => syncCount++);

        Assert.False(debug.VisibilityOff);
        Assert.False(debug.CollisionsOff);
        Assert.Equal(1, syncCount); // sync called once even for multiple keys
    }

    [Fact]
    public void NoRecognizedKeys_DoesNotSync()
    {
        var debug = new DebugSettings { Enabled = true };
        var input = new FakeInputManager { TextInput = "qwj" };
        bool synced = false;
        debug.HandleDebugKeys(input, () => synced = true);

        Assert.False(synced);
    }

    [Fact]
    public void Reset_RestoresAllDefaults()
    {
        var debug = new DebugSettings
        {
            Enabled = true,
            VisibilityOff = false,
            CollisionsOff = false,
            Invulnerable = false,
            LightOff = false,
            MaxSpeed = false,
            FreeCrafting = false,
            ZoomLevel = 3,
        };

        debug.Reset();

        Assert.True(debug.VisibilityOff);
        Assert.True(debug.CollisionsOff);
        Assert.True(debug.Invulnerable);
        Assert.True(debug.LightOff);
        Assert.True(debug.MaxSpeed);
        Assert.True(debug.FreeCrafting);
        Assert.Equal(0, debug.ZoomLevel);
    }

    [Fact]
    public void UppercaseKeys_AlsoWork()
    {
        var debug = new DebugSettings { Enabled = true };
        var input = new FakeInputManager { TextInput = "V" };
        debug.HandleDebugKeys(input, () => { });
        Assert.False(debug.VisibilityOff);

        debug = new DebugSettings { Enabled = true };
        input = new FakeInputManager { TextInput = "C" };
        debug.HandleDebugKeys(input, () => { });
        Assert.False(debug.CollisionsOff);

        debug = new DebugSettings { Enabled = true };
        input = new FakeInputManager { TextInput = "H" };
        debug.HandleDebugKeys(input, () => { });
        Assert.False(debug.Invulnerable);

        debug = new DebugSettings { Enabled = true };
        input = new FakeInputManager { TextInput = "L" };
        debug.HandleDebugKeys(input, () => { });
        Assert.False(debug.LightOff);

        debug = new DebugSettings { Enabled = true };
        input = new FakeInputManager { TextInput = "M" };
        debug.HandleDebugKeys(input, () => { });
        Assert.False(debug.MaxSpeed);

        debug = new DebugSettings { Enabled = true };
        input = new FakeInputManager { TextInput = "F" };
        debug.HandleDebugKeys(input, () => { });
        Assert.False(debug.FreeCrafting);
    }
}

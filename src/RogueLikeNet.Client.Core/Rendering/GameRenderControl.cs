using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using RogueLikeNet.Client.Core.Networking;
using RogueLikeNet.Client.Core.State;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Protocol.Messages;
using SkiaSharp;

namespace RogueLikeNet.Client.Core.Rendering;

/// <summary>
/// Avalonia custom control that renders the game world using SkiaSharp.
/// This is the main game viewport.
/// </summary>
public class GameRenderControl : Control
{
    private readonly TileRenderer _tileRenderer = new();
    private readonly ClientGameState _gameState = new();
    private IGameServerConnection? _connection;
    private DispatcherTimer? _renderTimer;
    private bool _initialized;

    public ClientGameState GameState => _gameState;

    public void SetConnection(IGameServerConnection connection)
    {
        _connection = connection;
        _connection.OnWorldSnapshot += OnWorldSnapshot;
        _connection.OnWorldDelta += OnWorldDelta;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (!_initialized)
        {
            _tileRenderer.Initialize();
            _initialized = true;
        }

        Focusable = true;
        Focus();

        // Render at ~30 fps
        _renderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _renderTimer.Tick += (_, _) => InvalidateVisual();
        _renderTimer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _renderTimer?.Stop();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        int width = _tileRenderer.PixelWidth;
        int height = _tileRenderer.PixelHeight;

        // Create an SKBitmap, render to it, then draw to Avalonia
        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);

        _tileRenderer.Render(canvas, _gameState);

        // Convert to Avalonia bitmap
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = new MemoryStream(data.ToArray());
        var avaloniaBitmap = new Avalonia.Media.Imaging.Bitmap(stream);

        var destRect = new Rect(0, 0, bounds.Width, bounds.Height);
        var srcRect = new Rect(0, 0, width, height);

        context.DrawImage(avaloniaBitmap, srcRect, destRect);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        var input = e.Key switch
        {
            Key.Up or Key.W => new ClientInputMsg { ActionType = ActionTypes.Move, TargetX = 0, TargetY = -1 },
            Key.Down or Key.S => new ClientInputMsg { ActionType = ActionTypes.Move, TargetX = 0, TargetY = 1 },
            Key.Left or Key.A => new ClientInputMsg { ActionType = ActionTypes.Move, TargetX = -1, TargetY = 0 },
            Key.Right or Key.D => new ClientInputMsg { ActionType = ActionTypes.Move, TargetX = 1, TargetY = 0 },
            Key.Space => new ClientInputMsg { ActionType = ActionTypes.Wait },
            _ => null
        };

        if (input != null && _connection != null)
        {
            input.Tick = _gameState.WorldTick;
            _ = _connection.SendInputAsync(input);
            e.Handled = true;
        }
    }

    private void OnWorldSnapshot(WorldSnapshotMsg snapshot)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _gameState.ApplySnapshot(snapshot);
            InvalidateVisual();
        });
    }

    private void OnWorldDelta(WorldDeltaMsg delta)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _gameState.ApplyDelta(delta);
        });
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        return new Size(_tileRenderer.PixelWidth, _tileRenderer.PixelHeight);
    }
}

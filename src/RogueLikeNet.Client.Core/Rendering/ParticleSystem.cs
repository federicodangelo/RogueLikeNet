using Engine.Core;
using Engine.Platform;

namespace RogueLikeNet.Client.Core.Rendering;

/// <summary>
/// Client-side particle effects for combat, spells, and ambient visuals.
/// All positions are in world-space tile coordinates; rendering converts to screen-space.
/// </summary>
public sealed class ParticleSystem
{
    private readonly List<Particle> _particles = new();
    private readonly Random _rng = new();

    /// <summary>Spawn floating damage number at a world position.</summary>
    public void SpawnDamageNumber(int worldX, int worldY, int damage, bool killed)
    {
        _particles.Add(new Particle
        {
            WorldX = worldX + (_rng.NextSingle() - 0.5f) * 0.3f,
            WorldY = worldY - 0.2f,
            VelocityX = (_rng.NextSingle() - 0.5f) * 0.3f,
            VelocityY = -1.5f, // float upward
            Text = damage.ToString(),
            Color = killed ? new Color4(255, 50, 50, 255) : new Color4(255, 200, 80, 255),
            Life = 1.0f,
            Decay = killed ? 0.8f : 1.2f,
        });
    }

    /// <summary>Spawn hit sparks between attacker and target.</summary>
    public void SpawnHitSparks(int attackerX, int attackerY, int targetX, int targetY, bool killed)
    {
        int count = killed ? 6 : 3;
        for (int i = 0; i < count; i++)
        {
            float dx = (targetX - attackerX) * 0.5f;
            float dy = (targetY - attackerY) * 0.5f;
            _particles.Add(new Particle
            {
                WorldX = targetX + (_rng.NextSingle() - 0.5f) * 0.4f,
                WorldY = targetY + (_rng.NextSingle() - 0.5f) * 0.4f,
                VelocityX = dx + (_rng.NextSingle() - 0.5f) * 2f,
                VelocityY = dy + (_rng.NextSingle() - 0.5f) * 2f,
                Text = killed ? "*" : "·",
                Color = killed ? new Color4(255, 80, 30, 255) : new Color4(255, 255, 100, 255),
                Life = 1.0f,
                Decay = killed ? 1.5f : 2.5f,
            });
        }
    }

    /// <summary>Update all particles. Call once per frame with deltaTime in seconds.</summary>
    public void Update(float dt)
    {
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.WorldX += p.VelocityX * dt;
            p.WorldY += p.VelocityY * dt;
            p.Life -= p.Decay * dt;
            if (p.Life <= 0)
                _particles.RemoveAt(i);
            else
                _particles[i] = p;
        }
    }

    /// <summary>Render all active particles.</summary>
    public void Render(ISpriteRenderer r, int cameraCenterX, int cameraCenterY,
        int halfW, int halfH, float shakeX, float shakeY)
    {
        foreach (var p in _particles)
        {
            float sx = p.WorldX - (cameraCenterX - halfW);
            float sy = p.WorldY - (cameraCenterY - halfH);

            float px = sx * TileRenderer.TileWidth + shakeX;
            float py = sy * TileRenderer.TileHeight + shakeY;

            byte alpha = (byte)(Math.Clamp(p.Life, 0f, 1f) * 255);
            var color = new Color4(p.Color.R, p.Color.G, p.Color.B, alpha);
            r.DrawTextScreen(px, py, p.Text, color, 1f);
        }
    }

    public int ActiveCount => _particles.Count;

    private struct Particle
    {
        public float WorldX, WorldY;
        public float VelocityX, VelocityY;
        public string Text;
        public Color4 Color;
        public float Life;   // 0..1, dies at 0
        public float Decay;  // life units per second
    }
}

using Engine.Core;
using Engine.Platform;
using Engine.Rendering.Base;

namespace RogueLikeNet.Client.Core.Rendering.Game;

/// <summary>
/// Client-side particle effects for combat, spells, and ambient visuals.
/// All positions are in world-space tile coordinates; rendering converts to screen-space.
/// </summary>
public sealed class ParticleSystem
{
    private readonly List<Particle> _particles = new();
    private readonly Random _rng = new();

    private float RandomWorldOffsetX() => _rng.NextSingle() - 0.5f; // random offset in range [-0.5, 0.5]
    private float RandomVelocityX() => _rng.NextSingle() - 0.5f; // random velocity in range [-0.5, 0.5]

    private const float VelocityY = -1.5f; // float upwards
    private const float OffsetY = -0.2f; // slightly above the target tile


    /// <summary>Spawn floating damage number at a world position.</summary>
    public void SpawnDamageNumber(int worldX, int worldY, int damage, bool killed)
    {
        _particles.Add(new Particle
        {
            WorldX = worldX + RandomWorldOffsetX(),
            WorldY = worldY + OffsetY,
            VelocityX = RandomVelocityX(),
            VelocityY = VelocityY,
            Text = damage.ToString(),
            Color = killed ? new Color4(255, 50, 50, 255) : new Color4(255, 200, 80, 255),
            Life = 1.0f,
            Decay = killed ? 0.8f : 1.2f,
        });
    }

    /// <summary>Spawn floating "BLOCK" text when a shield blocks an attack.</summary>
    public void SpawnBlockText(int worldX, int worldY)
    {
        _particles.Add(new Particle
        {
            WorldX = worldX + RandomWorldOffsetX(),
            WorldY = worldY + OffsetY,
            VelocityX = RandomVelocityX(),
            VelocityY = VelocityY,
            Text = "BLOCK",
            Color = new Color4(100, 180, 255, 255),
            Life = 1.0f,
            Decay = 1.0f,
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

    /// <summary>Spawn a projectile trail from attacker to target (for ranged attacks).</summary>
    public void SpawnProjectileTrail(int attackerX, int attackerY, int targetX, int targetY)
    {
        float dx = targetX - attackerX;
        float dy = targetY - attackerY;
        float dist = MathF.Sqrt(dx * dx + dy * dy);
        if (dist < 1f) return;

        // Place trail particles along the line from attacker to target
        int steps = Math.Max(1, (int)dist);
        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps;
            _particles.Add(new Particle
            {
                WorldX = attackerX + dx * t,
                WorldY = attackerY + dy * t,
                VelocityX = 0f,
                VelocityY = 0f,
                Text = "·",
                Color = new Color4(255, 220, 100, 255),
                Life = 0.15f + (1f - t) * 0.3f, // tail fades first
                Decay = 2.0f,
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
        int halfW, int halfH, float shakeX, float shakeY, int tileW, int tileH)
    {
        foreach (var p in _particles)
        {
            float sx = p.WorldX - (cameraCenterX - halfW);
            float sy = p.WorldY - (cameraCenterY - halfH);

            float px = (sx + 0.5f) * tileW + shakeX; // +half-tile offset to center text
            float py = sy * tileH + shakeY;

            byte alpha = (byte)(Math.Clamp(p.Life, 0f, 1f) * 255);
            var color = new Color4(p.Color.R, p.Color.G, p.Color.B, alpha);

            var textWidth = r.MeasureText(p.Text, 1.0f);
            px -= textWidth / 2f; // center text horizontally
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

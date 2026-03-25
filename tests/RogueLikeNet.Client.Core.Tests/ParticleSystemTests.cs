using RogueLikeNet.Client.Core.Rendering;

namespace RogueLikeNet.Client.Core.Tests;

public class ParticleSystemTests
{
    [Fact]
    public void SpawnDamageNumber_AddsParticle()
    {
        var ps = new ParticleSystem();
        ps.SpawnDamageNumber(5, 5, 10, killed: false);
        Assert.Equal(1, ps.ActiveCount);
    }

    [Fact]
    public void SpawnHitSparks_AddsMultipleParticles()
    {
        var ps = new ParticleSystem();
        ps.SpawnHitSparks(5, 5, 6, 5, killed: false);
        Assert.Equal(3, ps.ActiveCount); // 3 sparks for non-kill
    }

    [Fact]
    public void SpawnHitSparks_KilledAddsMoreParticles()
    {
        var ps = new ParticleSystem();
        ps.SpawnHitSparks(5, 5, 6, 5, killed: true);
        Assert.Equal(6, ps.ActiveCount); // 6 sparks for kill
    }

    [Fact]
    public void Update_DecaysParticles()
    {
        var ps = new ParticleSystem();
        ps.SpawnDamageNumber(5, 5, 10, killed: false);
        Assert.Equal(1, ps.ActiveCount);

        // Simulate enough time for the particle to die (decay=1.2, life=1.0 → dies at ~0.83s)
        ps.Update(0.5f);
        Assert.Equal(1, ps.ActiveCount); // still alive

        ps.Update(0.5f);
        Assert.Equal(0, ps.ActiveCount); // should die after ~0.83s total
    }

    [Fact]
    public void Update_RemovesExpiredParticles()
    {
        var ps = new ParticleSystem();
        ps.SpawnDamageNumber(5, 5, 10, killed: false);

        // Fast-forward well past the life span
        ps.Update(2.0f);
        Assert.Equal(0, ps.ActiveCount);
    }

    [Fact]
    public void Update_PreservesLivingParticles()
    {
        var ps = new ParticleSystem();
        ps.SpawnDamageNumber(5, 5, 10, killed: false);
        ps.SpawnHitSparks(4, 5, 5, 5, killed: true);
        int total = ps.ActiveCount; // 1 + 6 = 7

        ps.Update(0.01f); // tiny time step
        Assert.Equal(total, ps.ActiveCount); // all should survive
    }

    [Fact]
    public void EmptySystem_UpdateDoesNotThrow()
    {
        var ps = new ParticleSystem();
        ps.Update(1.0f);
        Assert.Equal(0, ps.ActiveCount);
    }
}

using System.Numerics;
using Terrain.Core.World;
using Terrain.Core.Rendering;
using Xunit;

namespace Terrain.Core.Tests;

public sealed class WorldTests
{
    [Fact]
    public void EveryWorldContainsAllFiveBiomesAndLowlandsStayLow()
    {
        foreach (int seed in new[] { 0, 42, 123456 })
        {
            var world = new WorldTerrain(seed);
            for (int i = 0; i < 5; i++)
            {
                var center = world.BiomeExamples[i];
                var sample = world.Sample(center.X, center.Y);
                Assert.Equal((WorldBiome)i, sample.Biome);
                Assert.True(float.IsFinite(sample.Height));
                if (i is 1 or 3)
                    Assert.InRange(sample.Height, -.2f, .4f);
            }
        }
    }

    [Fact]
    public void NeighbourChunksSharePositionsNormalsAndColorsExactly()
    {
        var world = new WorldTerrain(42);
        var a = world.BuildChunk(new(-1, 0), TestContext.Current.CancellationToken);
        var b = world.BuildChunk(new(0, 0), TestContext.Current.CancellationToken);
        for (int z = 0; z <= WorldTerrain.Cells; z++)
            Assert.Equal(a.Vertices[z * 33 + 32], b.Vertices[z * 33]);
        var repeat = new WorldTerrain(42).BuildChunk(new(-1, 0), TestContext.Current.CancellationToken);
        Assert.Equal(a.Vertices, repeat.Vertices);
        Assert.Equal(a.Indices, repeat.Indices);
        Assert.All(a.Indices, index => Assert.InRange(index, 0, a.Vertices.Length - 1));
    }

    [Fact]
    public void GroundInterpolationMatchesRenderedTriangleAndCameraStaysInside()
    {
        var world = new WorldTerrain(73);
        var chunk = world.BuildChunk(new(-1, -1), TestContext.Current.CancellationToken);
        float x = -2 + WorldTerrain.Step * .2f, z = -2 + WorldTerrain.Step * .3f;
        float expected = chunk.Vertices[0].Position.Y * .5f + chunk.Vertices[1].Position.Y * .2f + chunk.Vertices[33].Position.Y * .3f - .5f;
        Assert.Equal(expected, world.Ground(x, z), 5);
        var camera = world.Anchor(new(new(100, -20, -100)));
        Assert.InRange(camera.Position.X, -32, 32);
        Assert.InRange(camera.Position.Z, -32, 32);
        Assert.True(camera.Position.Y > world.Ground(camera.Position.X, camera.Position.Z));
    }

    [Fact]
    public void StreamingHasForwardReserveAndBoundedCacheAndSupportsCancellation()
    {
        var world = new WorldTerrain(42);
        var stream = new WorldStream(world);
        var camera = new CameraPose(Vector3.Zero);
        var selected = WorldStream.Select(camera);
        Assert.True(selected.Count(k => k.Z < 0) > selected.Count(k => k.Z >= 0));
        Assert.True(selected.Length < WorldStream.CacheLimit);
        Assert.Contains(new ChunkKey(0, 0), selected);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => stream.Build(selected, cancellation.Token));
        stream.Build([new(0, 0), new(1, 0)], TestContext.Current.CancellationToken);
        Assert.False(stream.HasGround(Vector3.Zero));
        stream.Publish([new(0, 0), new(1, 0)]);
        Assert.True(stream.HasGround(Vector3.Zero));
        Assert.False(stream.HasGround(new(20, 0, 20)));
        Assert.Equal(2, stream.CachedCount);
    }

    [Fact]
    public void FastTurnShortensFogUntilTheNewForwardAreaIsPublished()
    {
        var stream = new WorldStream(new WorldTerrain(42));
        var camera = new CameraPose(Vector3.Zero);
        stream.Publish(WorldStream.Select(camera));
        float before = stream.ViewDistance(camera);
        var turned = camera.Turn(180);
        float whileLoading = stream.ViewDistance(turned);
        Assert.True(whileLoading < before);
        stream.Publish(WorldStream.Select(turned));
        Assert.True(stream.ViewDistance(turned) > whileLoading);
    }

    [Fact]
    public void DayLastsFiveMinutesNightThreeAndCycleRepeats()
    {
        Assert.True(DayNight.Sun(299).Y > 0);
        Assert.True(DayNight.Sun(301).Y < 0);
        Assert.True(DayNight.Sun(479).Y < 0);
        Assert.True(DayNight.Sun(481).Y > 0);
        foreach (float boundary in new[] { 300f, 480f })
            Assert.InRange(Vector3.Distance(DayNight.Sun(boundary - .001f), DayNight.Sun(boundary + .001f)), 0, .0001f);
        Assert.True(DayNight.Sun(150).Y > .999f);
        Assert.True(DayNight.Sun(390).Y < -.999f);
        Assert.True(Vector3.Distance(DayNight.Sun(0), DayNight.Sun(480)) < .00001f);
        Assert.True(DayNight.Exposure(390) < DayNight.Exposure(150));
        var moon = DayNight.Sky(Vector3.UnitY, 390, .001f);
        var night = DayNight.Sky(Vector3.Normalize(new Vector3(.4f, 1, 0)), 390, .001f);
        Assert.True(moon.Length() > night.Length() * 3);
        Assert.Equal(1, DayNight.Fog(6));
    }
}

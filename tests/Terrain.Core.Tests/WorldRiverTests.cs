using Terrain.Core.World;
using Xunit;

namespace Terrain.Core.Tests;

public sealed class WorldRiverTests
{
    [Fact]
    public void RiverMeshHasWaterAndWalkingStaysAboveItsSurface()
    {
        var world = new WorldTerrain(42);
        var source = world.Rivers.Sources[1];
        float z = source.Z + 3;
        float x = Enumerable.Range(-80, 161).Select(i => source.X + i * .05f)
            .OrderByDescending(x => world.Rivers.Sample(x, z).Coverage).First();
        var sample = world.Sample(x, z);
        Assert.True(sample.Water);
        var camera = world.Anchor(new(new(x, 0, z)), WorldLod.NearCells);
        Assert.True(camera.Position.Y >= sample.WaterLevel + .119f);
        var key = new ChunkKey((int)MathF.Floor(x / 2), (int)MathF.Floor(z / 2));
        foreach (int cells in new[] { WorldTerrain.Cells, WorldLod.MediumCells, WorldLod.NearCells })
        {
            var mesh = world.BuildChunk(key, TestContext.Current.CancellationToken, cells);
            Assert.Contains(mesh.Vertices, v => v.Color == new System.Numerics.Vector3(.12f, .34f, .40f));
            Assert.All(mesh.Indices, i => Assert.InRange(i, 0, mesh.Vertices.Length - 1));
        }
    }

    [Theory]
    [InlineData(42)]
    [InlineData(20260909)]
    public void MountainRiversHaveWaterAndDescendAcrossTheWorld(int seed)
    {
        var world = new WorldTerrain(seed);
        var repeated = new WorldTerrain(seed);
        Assert.Equal(3, world.Rivers.Sources.Count);
        foreach (var source in world.Rivers.Sources)
        {
            Assert.Equal(WorldBiome.Winter, world.Sample(source.X, source.Z).Biome);
            float previous = float.PositiveInfinity;
            for (float z = source.Z + 1; z <= 31; z += .5f)
            {
                // Search the full corridor independently of the river's centreline implementation.
                var wet = Enumerable.Range(-80, 161).Select(i => source.X + i * .05f)
                    .Where(x => world.Rivers.Sample(x, z).Coverage > 0).Select(x => world.Sample(x, z))
                    .Where(s => s.Water && s.WaterCoverage > 0).OrderBy(s => s.WaterLevel).ToArray();
                Assert.NotEmpty(wet);
                var sample = wet[0];
                Assert.True(sample.WaterLevel <= previous + 1e-5f);
                Assert.True(sample.Height < sample.WaterLevel);
                previous = sample.WaterLevel;
            }
            Assert.Equal(world.Sample(source.X, source.Z + 1), repeated.Sample(source.X, source.Z + 1));
        }
    }

    [Fact]
    public void WinterHasBroadUplandAndSeparatedPeaks()
    {
        var world = new WorldTerrain(20260909);
        int upland = 0, high = 0;
        for (int z = -31; z < -20; z++)
            for (int x = -30; x <= 30; x++)
            {
                float height = world.Sample(x, z).Height;
                if (height is > .7f and < 1.4f)
                    upland++;
                if (height > 2)
                    high++;
            }
        Assert.True(upland > 350);
        Assert.InRange(high, 10, 130);
    }
}

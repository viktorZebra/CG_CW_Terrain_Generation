using Terrain.Core.World;
using Xunit;

namespace Terrain.Core.Tests;

public sealed class WorldClimateTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(42)]
    [InlineData(123456)]
    [InlineData(20260909)]
    public void LatitudeOrdersBiomesAndWetlandsStayInTheForestBelt(int seed)
    {
        var world = new WorldTerrain(seed);
        int forest = 0, swamp = 0, steppe = 0;
        for (int x = -30; x <= 30; x++)
        {
            Assert.Equal(WorldBiome.Winter, world.Sample(x, -31).Biome);
            Assert.Equal(WorldBiome.Desert, world.Sample(x, 31).Biome);
            for (int z = -28; z <= 28; z++)
            {
                var sample = world.Sample(x, z);
                if (sample.Biome == WorldBiome.Swamp)
                {
                    swamp++;
                    Assert.InRange(z, -15, 12);
                    Assert.True(sample.Height < .4f);
                }
                if (sample.Biome == WorldBiome.Forest)
                    forest++;
                if (sample.Biome == WorldBiome.Steppe)
                {
                    steppe++;
                    Assert.InRange(z, 4, 29);
                }
                if (sample.Biome == WorldBiome.Desert)
                    Assert.True(sample.Height < .4f);
            }
        }
        Assert.True(forest > 150 && swamp > 30 && steppe > 150);
        // Along each meridian there is a steppe zone separating the forest from the desert.
        for (int x = -28; x <= 28; x += 4)
            Assert.Contains(Enumerable.Range(5, 23), z => world.Sample(x, z).Biome == WorldBiome.Steppe);
    }

    [Fact]
    public void ClimateWeightsAreContinuousNormalizedAndSeeded()
    {
        var climate = new WorldClimate(42);
        var repeated = new WorldClimate(42);
        var other = new WorldClimate(73);
        int changed = 0;
        for (int z = -30; z <= 30; z += 2)
            for (int x = -30; x <= 30; x += 2)
            {
                var sample = climate.Sample(x, z);
                Assert.Equal(sample, repeated.Sample(x, z));
                if (sample != other.Sample(x, z))
                    changed++;
                float sum = 0;
                for (int i = 0; i < 5; i++)
                {
                    Assert.InRange(sample[i], 0, 1);
                    Assert.True(Math.Abs(sample[i] - climate.Sample(x + .001f, z + .001f)[i]) < .003f);
                    sum += sample[i];
                }
                Assert.Equal(1, sum, 5);
            }
        Assert.True(changed > 200);
    }

    [Fact]
    public void MarshesFormMultiplePatchesRatherThanOneRadialSector()
    {
        var world = new WorldTerrain(20260909);
        var wetlands = new HashSet<(int X, int Z)>();
        for (int z = -10; z <= 5; z++)
            for (int x = -28; x <= 28; x++)
                if (world.Sample(x, z).Biome == WorldBiome.Swamp)
                    wetlands.Add((x, z));
        int significantPatches = 0;
        while (wetlands.Count > 0)
        {
            var queue = new Queue<(int X, int Z)>();
            var start = wetlands.First();
            wetlands.Remove(start);
            queue.Enqueue(start);
            int count = 0;
            while (queue.TryDequeue(out var p))
            {
                count++;
                foreach (var next in new[] { (p.X + 1, p.Z), (p.X - 1, p.Z), (p.X, p.Z + 1), (p.X, p.Z - 1) })
                    if (wetlands.Remove(next))
                        queue.Enqueue(next);
            }
            if (count >= 4)
                significantPatches++;
        }
        Assert.True(significantPatches >= 3);
    }
}

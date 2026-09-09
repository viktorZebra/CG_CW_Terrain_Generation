using Terrain.Core.Hydrology;
using Terrain.Core.Models;
using Xunit;

namespace Terrain.Core.Tests;

public sealed class HydrologyTests
{
    [Fact]
    public void BowlFillsToSpillAndSourceHeightsStayIntact()
    {
        var map = new HeightMap(7, 7);
        Array.Fill(map.Values, .8f);
        for (int y = 1; y < 6; y++)
            for (int x = 1; x < 6; x++)
                map[x, y] = .2f;
        map[3, 0] = .5f;
        var original = (float[])map.Values.Clone();
        var water = HydrologyBuilder.Build(map, new(0));
        Assert.Equal(original, map.Values);
        Assert.Equal(.5f, water.Levels[3 * 7 + 3]);
        Assert.True(water.StandingWater[3 * 7 + 3]);
        Assert.Equal(.8f, water.Levels[0]);
        Assert.False(water.StandingWater[0]);
        var mesh = Mesh.Build(map, 0, new(0));
        Assert.Equal(.5f, mesh.Vertices[3 * 7 + 3].Position.Y);
        Assert.Equal(.2f, map[3, 3]);
        ValidateDrainage(water);
    }

    [Fact]
    public void FlatRectangularMapHasNoCyclesAndConservesRainfall()
    {
        var map = new HeightMap(19, 11);
        Array.Fill(map.Values, .4f);
        var water = HydrologyBuilder.Build(map, new(0, 1));
        Assert.DoesNotContain(true, water.StandingWater);
        Assert.All(water.Coverage, value => Assert.Equal(0, value));
        ValidateDrainage(water);
        int outflow = Enumerable.Range(0, map.Values.Length)
            .Where(i => water.Downstream[i] < 0).Sum(i => water.Accumulation[i]);
        Assert.Equal(map.Values.Length, outflow);
    }

    [Fact]
    public void DrainageUsesSlopePerDistanceRatherThanLowestNeighbor()
    {
        var map = new HeightMap(5, 5);
        Array.Fill(map.Values, .9f);
        for (int i = 0; i < 5; i++)
            map[0, i] = map[4, i] = map[i, 0] = map[i, 4] = .1f;
        map[2, 2] = .8f;
        map[3, 2] = .6f;
        map[3, 3] = .55f;
        var water = HydrologyBuilder.Build(map, new(0));
        Assert.Equal(2 * 5 + 3, water.Downstream[2 * 5 + 2]);
        ValidateDrainage(water);
    }

    [Fact]
    public void ValleyCollectsTributariesAndFormsContinuousRiver()
    {
        var map = new HeightMap(33, 33);
        for (int y = 0; y < 33; y++)
            for (int x = 0; x < 33; x++)
                map[x, y] = .1f + (32 - y) * .012f + Math.Abs(x - 16) * .015f;
        var water = HydrologyBuilder.Build(map, new(.02f, .01f));
        int upstream = 10 * 33 + 16, downstream = 28 * 33 + 16;
        Assert.True(water.Accumulation[downstream] > water.Accumulation[upstream]);
        Assert.True(water.Accumulation[downstream] > 28);
        for (int y = 15; y < 33; y++)
            Assert.Equal(1, water.Coverage[y * 33 + 16]);
        Assert.True(water.Moisture[downstream] > water.Moisture[28 * 33 + 1]);
        Assert.Contains(water.Bank, value => value > 0);
        ValidateDrainage(water);
        var repeat = HydrologyBuilder.Build(map, new(.02f, .01f));
        Assert.Equal(water.Downstream, repeat.Downstream);
        Assert.Equal(water.Coverage, repeat.Coverage);
    }

    [Fact]
    public void SeaLevelCreatesFlatWaterAndOptionValidationRejectsNonFiniteValues()
    {
        var map = new HeightMap(3, 4);
        Array.Fill(map.Values, .1f);
        var water = HydrologyBuilder.Build(map, new(.25f));
        Assert.All(water.Levels, value => Assert.Equal(.25f, value));
        Assert.All(water.StandingWater, value => Assert.True(value));
        Assert.All(water.Coverage, value => Assert.Equal(1, value));
        Assert.Throws<ArgumentOutOfRangeException>(() => HydrologyBuilder.Build(map, new(float.NaN)));
        Assert.Throws<ArgumentOutOfRangeException>(() => HydrologyBuilder.Build(map, new(0, 0)));
    }

    private static void ValidateDrainage(WaterMap water)
    {
        for (int start = 0; start < water.Levels.Length; start++)
        {
            int current = start, steps = 0;
            while (water.Downstream[current] is var next && next >= 0)
            {
                Assert.True(water.Levels[next] <= water.Levels[current]);
                Assert.True(water.Accumulation[next] > water.Accumulation[current]);
                Assert.True(++steps < water.Levels.Length);
                current = next;
            }
            int x = current % water.Width, y = current / water.Width;
            Assert.True(x == 0 || y == 0 || x == water.Width - 1 || y == water.Height - 1);
        }
    }
}

using System.Numerics;
using Terrain.Core.Climate;
using Terrain.Core.Generation;
using Terrain.Core.Hydrology;
using Terrain.Core.Models;
using Terrain.Core.Surface;
using Xunit;

namespace Terrain.Core.Tests;

public sealed class ClimateTests
{
    [Fact]
    public void ClimateIsDeterministicBoundedAndKeepsSourceAndWaterIntact()
    {
        var map = Generators.Generate(new(GeneratorKind.Perlin, 33, Seed: 12));
        var heights = (float[])map.Values.Clone();
        var water = HydrologyBuilder.Build(map, new());
        var downstream = (int[])water.Downstream.Clone();
        var levels = (float[])water.Levels.Clone();
        var first = ClimateBuilder.Build(map, new(Seed: 77), water);
        var again = ClimateBuilder.Build(map, new(Seed: 77), water);
        Assert.Equal(first.Samples, again.Samples);
        Assert.NotEqual(first.Samples, ClimateBuilder.Build(map, new(Seed: 78), water).Samples);
        foreach (var sample in first.Samples)
        {
            Assert.InRange(sample.Temperature, 0, 1);
            Assert.InRange(sample.Moisture, 0, 1);
            var b = sample.Biomes;
            float[] weights = [b.Meadow, b.WetForest, b.Steppe, b.Alpine, b.Snow];
            Assert.All(weights, w => Assert.InRange(w, 0, 1));
            Assert.InRange(weights.Sum(), .999999f, 1.000001f);
        }
        ClimateBuilder.Build(map, new(0, 1), water);
        Assert.Equal(heights, map.Values);
        Assert.Equal(downstream, water.Downstream);
        Assert.Equal(levels, water.Levels);
    }

    [Fact]
    public void ColdAndWetControlsMoveExpectedBiomesEvenWithoutWater()
    {
        var map = new HeightMap(17, 9);
        Array.Fill(map.Values, .2f);
        var dry = ClimateBuilder.Build(map, new(.9f, 0));
        var wet = ClimateBuilder.Build(map, new(.9f, 1));
        var cold = ClimateBuilder.Build(map, new(.1f, 1));
        for (int i = 0; i < map.Values.Length; i++)
        {
            Assert.True(wet.Samples[i].Biomes.WetForest > dry.Samples[i].Biomes.WetForest);
            Assert.True(dry.Samples[i].Biomes.Steppe > wet.Samples[i].Biomes.Steppe);
            Assert.True(cold.Samples[i].Biomes.Snow > wet.Samples[i].Biomes.Snow);
        }
    }

    [Fact]
    public void ElevationCoolsClimateAndWaterOnlyAddsMoisture()
    {
        var low = new HeightMap(9, 5);
        var high = new HeightMap(9, 5);
        Array.Fill(high.Values, 1);
        var lowClimate = ClimateBuilder.Build(low, new());
        var highClimate = ClimateBuilder.Build(high, new());
        var water = HydrologyBuilder.Build(low, new());
        var wet = ClimateBuilder.Build(low, new(), water);
        for (int i = 0; i < low.Values.Length; i++)
        {
            Assert.True(highClimate.Samples[i].Temperature < lowClimate.Samples[i].Temperature);
            Assert.Equal(lowClimate.Samples[i].Temperature, wet.Samples[i].Temperature);
            Assert.True(wet.Samples[i].Moisture >= lowClimate.Samples[i].Moisture);
        }
        Assert.True(wet.Samples.Average(s => s.Moisture) > lowClimate.Samples.Average(s => s.Moisture));
    }

    [Fact]
    public void SpatialNoiseIsSmoothAndResolutionIndependentOnRectangularMaps()
    {
        var coarse = new HeightMap(17, 9);
        var fine = new HeightMap(33, 17);
        Array.Fill(coarse.Values, .4f);
        Array.Fill(fine.Values, .4f);
        var a = ClimateBuilder.Build(coarse, new());
        var b = ClimateBuilder.Build(fine, new());
        for (int y = 0; y < 9; y++)
            for (int x = 0; x < 17; x++)
                Assert.Equal(a.Samples[y * 17 + x], b.Samples[y * 2 * 33 + x * 2]);
        for (int y = 0; y < 17; y++)
            for (int x = 1; x < 33; x++)
                Assert.InRange(Math.Abs(b.Samples[y * 33 + x].Moisture - b.Samples[y * 33 + x - 1].Moisture), 0, .15f);
    }

    [Fact]
    public void ClimateChangesMaterialsOnlyAndOverlaysStillExcludeSnowOnCliffsAndWater()
    {
        var map = Generators.Generate(new(GeneratorKind.Hills, 33));
        var warm = Mesh.Build(map, waterOptions: new(), vegetationOptions: new(), climateOptions: new(.9f, 0));
        var cold = Mesh.Build(map, waterOptions: new(), vegetationOptions: new(), climateOptions: new(.1f, 1));
        Assert.NotNull(warm.Climate);
        Assert.Equal(warm.Indices, cold.Indices);
        Assert.Equal(warm.Vertices.Select(v => v.Position), cold.Vertices.Select(v => v.Position));
        Assert.Equal(warm.Vertices.Select(v => v.Normal), cold.Vertices.Select(v => v.Normal));
        Assert.False(warm.Vertices.Select(v => v.Color).SequenceEqual(cold.Vertices.Select(v => v.Color)));
        var surface = new TerrainSurface();
        var snowy = new ClimateSample(0, .5f, new(0, 0, 0, 0, 1));
        Assert.Equal(1, surface.Sample(Vector3.Zero, Vector3.UnitY, climate: snowy).Snow);
        Assert.Equal(0, surface.Sample(Vector3.Zero, Vector3.UnitX, climate: snowy).Snow);
        Assert.Equal(0, surface.Sample(Vector3.Zero, Vector3.UnitY, water: 1, climate: snowy).Snow);
    }
}

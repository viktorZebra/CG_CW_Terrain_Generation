using System.Numerics;
using Terrain.Core.Generation;
using Terrain.Core.Models;
using Terrain.Core.Surface;
using Terrain.Core.Vegetation;
using Xunit;

namespace Terrain.Core.Tests;

public sealed class DetailTests
{
    [Fact]
    public void TalusIsSeededAndLeavesFlatGroundEmpty()
    {
        var flat = new HeightMap(33, 33);
        Array.Fill(flat.Values, .3f);
        Assert.Empty(RockScatter.Place(flat, Mesh.Build(flat), null, [], new(1)));
        var map = Generators.Generate(new(GeneratorKind.Perlin, 65, Seed: 43));
        var terrain = Mesh.Build(map);
        var a = RockScatter.Place(map, terrain, null, [], new(1, Seed: 12));
        Assert.NotEmpty(a);
        Assert.Equal(a, RockScatter.Place(map, terrain, null, [], new(1, Seed: 12)));
        Assert.Empty(RockScatter.Place(map, terrain, null, [], new(0)));
    }
    [Fact]
    public void SnowPatchesAreStableAndOnlyReduceSnowWithoutChangingGeometry()
    {
        var surface = new TerrainSurface(42);
        var climate = new Terrain.Core.Climate.ClimateSample(0, .7f, new(0, 0, 0, 0, 1));
        var values = Enumerable.Range(0, 40).Select(i => surface.Sample(new(i * .03f, 1, .25f), Vector3.UnitY, climate: climate, snowPatches: true).Snow).ToArray();
        Assert.All(values, x => Assert.InRange(x, .5f, 1));
        Assert.True(values.Max() - values.Min() > .05f);
        Assert.Equal(values, Enumerable.Range(0, 40).Select(i => surface.Sample(new(i * .03f, 1, .25f), Vector3.UnitY, climate: climate, snowPatches: true).Snow));
    }
}

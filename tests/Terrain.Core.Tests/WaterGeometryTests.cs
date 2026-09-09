using Terrain.Core.Hydrology;
using Terrain.Core.Models;
using Xunit;
namespace Terrain.Core.Tests;

public sealed class WaterGeometryTests
{
    [Fact]
    public void LakeKeepsItsBottomAndAddsFlatSurfaceInsideMap()
    {
        var map = new HeightMap(5, 3);
        Array.Fill(map.Values, .4f);
        map[2, 1] = .1f;
        var original = (float[])map.Values.Clone();
        var mesh = Mesh.Build(map, waterOptions: new(SeparateSurface: true));
        Assert.Equal(original, map.Values);
        Assert.Equal(.1f, mesh.Vertices[7].Position.Y);
        Assert.True(mesh.Vertices.Length > map.Values.Length);
        Assert.All(mesh.Vertices.Skip(map.Values.Length), v => { Assert.InRange(v.Position.Y, .4f, .401f); Assert.InRange(v.Position.X, -1, 1); Assert.InRange(v.Position.Z, -.5f, .5f); });
    }
    [Fact]
    public void FlatSeaAndDiagonalConfluenceHaveContinuousFiniteWater()
    {
        var flat = new HeightMap(5, 3);
        Array.Fill(flat.Values, .2f);
        var sea = Mesh.Build(flat, waterOptions: new(.4f, SeparateSurface: true));
        Assert.All(sea.Vertices.Skip(flat.Values.Length), v => Assert.InRange(v.Position.Y, .4f, .401f));
        var map = new HeightMap(17, 17);
        Array.Fill(map.Values, .3f);
        var water = HydrologyBuilder.Build(map, new(0, .01f));
        Array.Fill(water.Downstream, -1);
        Array.Fill(water.Accumulation, 1);
        Array.Clear(water.Coverage);
        int a = 6 * 17 + 6, b = 6 * 17 + 8, c = 7 * 17 + 7, d = 8 * 17 + 7;
        water.Downstream[a] = c;
        water.Downstream[b] = c;
        water.Downstream[c] = d;
        water.Accumulation[a] = 10;
        water.Accumulation[b] = 10;
        water.Accumulation[c] = 21;
        water.Accumulation[d] = 22;
        var prepared = WaterGeometry.Prepare(map, water, new(0, .01f, true, .02f));
        Assert.True((prepared.Water.Coverage[6 * 17 + 7] + prepared.Water.Coverage[7 * 17 + 6]) * .5f >= .5f);
        Assert.True(prepared.Water.Coverage[c] >= .99f);
        foreach (int i in new[] { a, b, c })
            Assert.True(prepared.Water.Levels[i] >= prepared.Water.Levels[water.Downstream[i]]);
        Assert.All(prepared.Ground.Values, h => Assert.InRange(h, 0, .3f));
    }

    [Theory]
    [InlineData(17, 17)]
    [InlineData(25, 17)]
    public void ChannelsPreserveDrainageAndOnlyLowerWorkingGround(int width, int height)
    {
        var map = new HeightMap(width, height);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                map[x, y] = .2f + .5f * (height - 1 - y) / (height - 1) + .08f * Math.Abs(x - width / 2f) / width;
        var original = (float[])map.Values.Clone();
        var options = new HydrologyOptions(0, .01f, true, .025f);
        var drainage = HydrologyBuilder.Build(map, options);
        var mesh = Mesh.Build(map, waterOptions: options);
        Assert.Equal(original, map.Values);
        Assert.Equal(drainage.Downstream, mesh.Water!.Downstream);
        Assert.Contains(mesh.WorkingMap!.Values.Zip(original), p => p.First < p.Second);
        Assert.All(mesh.WorkingMap.Values.Zip(original), p => Assert.True(float.IsFinite(p.First) && p.First >= 0 && p.First <= p.Second));
        for (int i = 0; i < drainage.Downstream.Length; i++)
            if (drainage.Downstream[i] >= 0)
                Assert.True(drainage.Levels[i] >= drainage.Levels[drainage.Downstream[i]]);
        Assert.All(mesh.Indices, i => Assert.InRange(i, 0, mesh.Vertices.Length - 1));
    }
}

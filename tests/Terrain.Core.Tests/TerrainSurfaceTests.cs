using System.Numerics;
using Terrain.Core.Models;
using Terrain.Core.Surface;
using Xunit;

namespace Terrain.Core.Tests;

public sealed class TerrainSurfaceTests
{
    [Fact]
    public void SteepLowSlopeIsRockAndFlatLowGroundIsGrass()
    {
        var surface = new TerrainSurface(42);
        var position = new Vector3(.2f, .35f, -.1f);
        var flat = surface.Sample(position, Vector3.UnitY);
        var cliff = surface.Sample(position, Vector3.Normalize(new Vector3(1, .2f, 0)));
        Assert.True(flat.Rock < .01f);
        Assert.True(cliff.Rock > .99f);
        Assert.True(flat.Color.Y > flat.Color.X);
        Assert.True(Vector3.Distance(flat.Color, cliff.Color) > .1f);
    }

    [Fact]
    public void SnowRemainsOnHighPlateauButNotOnCliff()
    {
        var surface = new TerrainSurface();
        var position = new Vector3(0, 1, 0);
        Assert.True(surface.Sample(position, Vector3.UnitY).Snow > .99f);
        Assert.True(surface.Sample(position, Vector3.UnitX).Snow < .01f);
        Assert.True(surface.Sample(position with
        {
            Y = .5f
        }, Vector3.UnitY).Snow < .01f);
    }

    [Fact]
    public void HeightTransitionsAreContinuousAndColorsStayInRange()
    {
        var surface = new TerrainSurface(123);
        foreach (var normal in new[] { Vector3.UnitY, Vector3.Normalize(new Vector3(1, 1, 0)), Vector3.UnitX })
        {
            var previous = surface.Sample(new(.13f, 0, .27f), normal).Color;
            for (int i = 1; i <= 10000; i++)
            {
                var color = surface.Sample(new(.13f, i / 10000f, .27f), normal).Color;
                Assert.True(float.IsFinite(color.X) && float.IsFinite(color.Y) && float.IsFinite(color.Z));
                Assert.True(color.X >= 0 && color.Y >= 0 && color.Z >= 0 && color.X <= 1 && color.Y <= 1 && color.Z <= 1);
                Assert.True(Vector3.Distance(previous, color) < .01f);
                previous = color;
            }
        }
    }

    [Fact]
    public void SurfaceSeedChangesOnlyColorsAndReproducesThemExactly()
    {
        var map = new HeightMap(17, 9);
        Array.Fill(map.Values, .4f);
        var original = (float[])map.Values.Clone();
        var first = Mesh.Build(map, 42);
        var repeat = Mesh.Build(map, 42);
        var other = Mesh.Build(map, 43);
        Assert.Equal(first.Vertices, repeat.Vertices);
        Assert.Equal(first.Indices, other.Indices);
        Assert.Equal(original, map.Values);
        Assert.True(first.Vertices.Zip(other.Vertices).All(pair => pair.First.Position == pair.Second.Position && pair.First.Normal == pair.Second.Normal));
        Assert.Contains(first.Vertices.Zip(other.Vertices), pair => pair.First.Color != pair.Second.Color);
        Assert.True(first.Vertices.Select(vertex => vertex.Color).Distinct().Count() > 10);
    }
}

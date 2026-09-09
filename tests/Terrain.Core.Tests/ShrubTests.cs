using System.Numerics;
using Terrain.Core.Models;
using Terrain.Core.Hydrology;
using Terrain.Core.Vegetation;
using Xunit;

namespace Terrain.Core.Tests;

public sealed class ShrubTests
{
    private static HeightMap Map()
    {
        var map = new HeightMap(33, 25);
        for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
                map[x, y] = .2f + .1f * x / (map.Width - 1);
        return map;
    }

    [Fact]
    public void ShrubsAreSeededSpacedGroundedAndIndependentOfTrees()
    {
        var map = Map();
        var terrain = Mesh.Build(map, climateOptions: new(.9f, .9f));
        var trees = ObjectScatter.Place(map, terrain, null, 42, new(BiomeAware: true));
        var options = new ShrubOptions(1, Seed: 67);
        var shrubs = ShrubScatter.Place(map, terrain, null, trees, 42, options);
        Assert.NotEmpty(shrubs);
        Assert.Equal(shrubs, ShrubScatter.Place(map, terrain, null, trees, 42, options));
        Assert.NotEqual(shrubs, ShrubScatter.Place(map, terrain, null, trees, 42, options with
        {
            Seed = 68
        }));
        Assert.Contains(shrubs, o => o.Kind == ObjectKind.RoundShrub);
        Assert.Contains(shrubs, o => o.Kind == ObjectKind.WideShrub);
        foreach (var shrub in shrubs)
        {
            Assert.InRange(Math.Abs(shrub.Position.Y - (.25f + .05f * shrub.Position.X)), 0, 1e-6f);
            Assert.All(trees, o => Assert.True(Vector2.Distance(new(o.Position.X, o.Position.Z), new(shrub.Position.X, shrub.Position.Z)) >= o.Size * .4f + shrub.Size * .6f + .01f));
        }
        for (int i = 0; i < shrubs.Length; i++)
            for (int j = i + 1; j < shrubs.Length; j++)
                Assert.True(Vector3.Distance(shrubs[i].Position, shrubs[j].Position) >= .03499f);
        var plain = Mesh.Build(map, 42, vegetationOptions: new(BiomeAware: true), climateOptions: new(.9f, .9f));
        var decorated = Mesh.Build(map, 42, vegetationOptions: new(BiomeAware: true), climateOptions: new(.9f, .9f), shrubOptions: options);
        Assert.Equal(plain.Vertices, decorated.Vertices.Take(plain.Vertices.Length).ToArray());
        Assert.Equal(plain.Indices, decorated.Indices.Take(plain.Indices.Length).ToArray());
        Assert.Equal(plain.TreeCount, decorated.TreeCount);
        Assert.Equal(plain.RockCount, decorated.RockCount);
        Assert.True(decorated.ShrubCount > 0);
    }

    [Fact]
    public void WaterBanksSnowCliffsAndLimitsAreRespected()
    {
        var map = Map();
        var terrain = Mesh.Build(map, climateOptions: new(.9f, .9f));
        ObjectInstance[] Place(Mesh mesh, WaterMap? water = null, ShrubOptions? options = null) =>
            ShrubScatter.Place(map, mesh, water, [], 42, options ?? new(1));
        Assert.Empty(Place(terrain, options: new(0)));
        Assert.InRange(Place(terrain, options: new(1, 5)).Length, 1, 5);
        Assert.Empty(Place(Mesh.Build(map, climateOptions: new(0, 1))));
        Assert.Empty(Place(terrain with
        {
            Vertices = terrain.Vertices.Select(v => v with { Normal = Vector3.UnitX }).ToArray()
        }));
        var water = HydrologyBuilder.Build(map, new(.5f));
        Assert.Empty(Place(terrain, water));
        var banks = water with
        {
            Coverage = new float[map.Values.Length],
            Bank = Enumerable.Repeat(1f, map.Values.Length).ToArray()
        };
        Assert.Empty(Place(terrain, banks));
        var dry = Mesh.Build(map, climateOptions: new(.9f, 0));
        Assert.True(Place(terrain).Length > Place(dry).Length);
    }
}

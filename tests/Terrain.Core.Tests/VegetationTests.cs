using System.Numerics;
using Terrain.Core.Models;
using Terrain.Core.Climate;
using Terrain.Core.Hydrology;
using Terrain.Core.Vegetation;
using Terrain.Core.Rendering;
using Terrain.Core.Rendering.Shadows;
using Xunit;

namespace Terrain.Core.Tests;

public sealed class VegetationTests
{
    private static HeightMap Flat(float height = .3f)
    {
        var map = new HeightMap(33, 25);
        Array.Fill(map.Values, height);
        return map;
    }

    [Fact]
    public void PlacementIsReproducibleSpacedAndGrounded()
    {
        var map = Flat();
        var terrain = Mesh.Build(map);
        var options = new VegetationOptions();
        var objects = ObjectScatter.Place(map, terrain, null, 42, options);
        Assert.Equal(objects, ObjectScatter.Place(map, terrain, null, 42, options));
        Assert.Contains(objects, item => item.Kind == ObjectKind.Pine);
        Assert.Contains(objects, item => item.Kind == ObjectKind.Broadleaf);
        Assert.Contains(objects, item => item.Kind == ObjectKind.Rock);
        foreach (var item in objects)
            Assert.InRange(item.Position.Y, .29999f, .30001f);
        for (int i = 0; i < objects.Length; i++)
            for (int j = i + 1; j < objects.Length; j++)
                Assert.True(Vector3.Distance(objects[i].Position, objects[j].Position) >= .04499f);
    }

    [Fact]
    public void BiomesChangeDensitySpeciesAndTreeLineDeterministically()
    {
        var map = Flat();
        ObjectInstance[] Forest(float temperature, float moisture, float density = .7f)
        {
            var terrain = Mesh.Build(map, climateOptions: new(temperature, moisture, 42));
            return ObjectScatter.Place(map, terrain, null, 42, new(density, MaxRocks: 0, BiomeAware: true));
        }
        var warm = Forest(.95f, 1);
        var cool = Forest(.60f, 1);
        var dry = Forest(.95f, 0);
        Assert.Equal(warm, Forest(.95f, 1));
        Assert.True(warm.Length > dry.Length * 2);
        Assert.NotEmpty(cool);
        double Pines(ObjectInstance[] objects) => objects.Count(o => o.Kind == ObjectKind.Pine) / (double)objects.Length;
        Assert.True(Pines(cool) > Pines(warm) + .2);
        Assert.Empty(Forest(0, 1));
        var alpine = Mesh.Build(map) with
        {
            Climate = new(map.Width, map.Height, Enumerable.Repeat(
                new ClimateSample(.21f, .8f, new(0, 0, 0, 1, 0)), map.Values.Length).ToArray())
        };
        Assert.Empty(ObjectScatter.Place(map, alpine, null, 42, new(MaxRocks: 0, BiomeAware: true)));
        Assert.Empty(Forest(.95f, 1, 0));
        Assert.True(Forest(.95f, 1, 1).Length > Forest(.95f, 1, .2f).Length);
        foreach (var item in warm)
            Assert.InRange(item.Position.Y, .29999f, .30001f);
        for (int i = 0; i < warm.Length; i++)
            for (int j = i + 1; j < warm.Length; j++)
                Assert.True(Vector3.Distance(warm[i].Position, warm[j].Position) >= .04499f);
    }

    [Fact]
    public void BiomeAwareObjectsRespectWaterCliffsAndSnowAndLegacyPlacementIsPreserved()
    {
        var map = Flat();
        var options = new VegetationOptions(BiomeAware: true);
        var snowy = Mesh.Build(map, climateOptions: new(0, 1));
        Assert.Empty(ObjectScatter.Place(map, snowy, null, 42, options));
        var water = HydrologyBuilder.Build(map, new(.5f));
        var wet = Mesh.Build(map, waterOptions: new(.5f), climateOptions: new(.95f, 1));
        Assert.Empty(ObjectScatter.Place(map, wet, water, 42, options));
        var terrain = Mesh.Build(map, climateOptions: new(.95f, 1));
        var steep = terrain with
        {
            Vertices = terrain.Vertices.Select(v => v with { Normal = Vector3.UnitX }).ToArray()
        };
        Assert.Empty(ObjectScatter.Place(map, steep, null, 42, options));
        var legacy = ObjectScatter.Place(map, Mesh.Build(map), null, 42, new());
        Assert.Equal(legacy, ObjectScatter.Place(map, snowy, null, 42, new()));
        Assert.Equal(legacy, ObjectScatter.Place(map, Mesh.Build(map), null, 42, options));
        var limited = Mesh.Build(map, vegetationOptions: new(1, 12, 3, true), climateOptions: new(.95f, 1));
        Assert.InRange(limited.TreeCount, 1, 12);
        Assert.InRange(limited.RockCount, 1, 3);
    }

    [Fact]
    public void RootsFollowSlopingSurfaceExactly()
    {
        var map = Flat();
        for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
                map[x, y] = .3f + .12f * (x / 16f - 1);
        var objects = ObjectScatter.Place(map, Mesh.Build(map), null, 42, new());
        Assert.NotEmpty(objects);
        foreach (var item in objects)
            Assert.InRange(Math.Abs(item.Position.Y - (.3f + .12f * item.Position.X)), 0, 1e-6f);
    }

    [Fact]
    public void WaterSnowAndSteepSlopesExcludeTrees()
    {
        var map = Flat();
        var flooded = HydrologyBuilder.Build(map, new(.5f));
        Assert.Empty(ObjectScatter.Place(map, Mesh.Build(map, 0, new(.5f)), flooded, 42, new()));
        var snow = Flat(1);
        Assert.Empty(ObjectScatter.Place(snow, Mesh.Build(snow), null, 42, new()));
        var terrain = Mesh.Build(map);
        var steep = terrain with
        {
            Vertices = terrain.Vertices.Select(v => v with { Normal = Vector3.UnitX }).ToArray()
        };
        Assert.Empty(ObjectScatter.Place(map, steep, null, 42, new()));
    }

    [Fact]
    public void DensityAndLimitsApplyWithoutChangingTerrain()
    {
        var map = Flat();
        var plain = Mesh.Build(map, 42);
        var decorated = Mesh.Build(map, 42, vegetationOptions: new(1, 20, 7));
        Assert.InRange(decorated.TreeCount, 1, 20);
        Assert.InRange(decorated.RockCount, 1, 7);
        Assert.Equal(plain.Vertices, decorated.Vertices.Take(plain.Vertices.Length).ToArray());
        Assert.Equal(plain.Indices, decorated.Indices.Take(plain.Indices.Length).ToArray());
        Assert.All(decorated.Indices, index => Assert.InRange(index, 0, decorated.Vertices.Length - 1));
        Assert.All(decorated.Vertices, vertex => Assert.InRange(vertex.Normal.Length(), .9999f, 1.0001f));
        Assert.DoesNotContain(ObjectScatter.Place(map, plain, null, 42, new(0)), item => item.Kind != ObjectKind.Rock);
    }

    [Theory]
    [InlineData(ObjectKind.Pine)]
    [InlineData(ObjectKind.Broadleaf)]
    [InlineData(ObjectKind.Rock)]
    [InlineData(ObjectKind.RoundShrub)]
    [InlineData(ObjectKind.WideShrub)]
    public void ReliefMovesAnchorsButPreservesObjectShapeAndNormals(ObjectKind kind)
    {
        var terrain = Mesh.Build(Flat());
        var root = new Vector3(.2f, .3f, -.1f);
        var decorated = ObjectGeometry.Append(terrain, [new(kind, root, .12f, .7f, 1)]);
        var reference = new Scene(23, -41, 17, HeightScale: 1);
        foreach (float heightScale in new[] { .05f, .65f, 2f })
        {
            var scene = reference with
            {
                HeightScale = heightScale
            };
            foreach (var vertex in decorated.Vertices.Skip(terrain.Vertices.Length))
            {
                var relative = scene.WorldVertexPosition(vertex) - scene.WorldPosition(root);
                var expected = reference.WorldVertexPosition(vertex) - reference.WorldPosition(root);
                Assert.True(Vector3.Distance(relative, expected) < 1e-6f);
                Assert.True(Vector3.Distance(scene.WorldNormal(vertex), reference.WorldNormal(vertex)) < 1e-6f);
            }
            var ground = terrain.Vertices[0];
            Assert.Equal(scene.WorldPosition(ground.Position), scene.WorldVertexPosition(ground));
        }
    }

    [Fact]
    public void TreesCastShadowsOntoTheTerrain()
    {
        var terrain = Mesh.Build(Flat());
        var root = new Vector3(0, .3f, 0);
        var decorated = ObjectGeometry.Append(terrain, [new(ObjectKind.Pine, root, .15f, 0, 1)]);
        var scene = new Scene(0, 0, 0, HeightScale: 1, Light: Vector3.UnitY, ShadowResolution: 512);
        var shadow = ShadowMap.Build(decorated, scene);
        float visibility = shadow.Visibility(shadow.Projection.Project(scene.WorldPosition(root)), 1, false);
        Assert.True(visibility < .1f);
    }
}

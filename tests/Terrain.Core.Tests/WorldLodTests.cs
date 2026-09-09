using System.Numerics;
using Terrain.Core.World;
using Terrain.Core.Rendering;
using Xunit;

namespace Terrain.Core.Tests;

public sealed class WorldLodTests
{
    [Theory]
    [InlineData(64)]
    [InlineData(128)]
    public void DetailedChunkHasClosedTopologyAndExactlyTheSameBoundaryAsCoarse(int cells)
    {
        var terrain = new WorldTerrain(42);
        var chunk = terrain.BuildChunk(new(0, 0), TestContext.Current.CancellationToken, cells);
        var coarse = terrain.BuildChunk(new(-1, 0), TestContext.Current.CancellationToken);
        int count = (cells + 1) * (cells + 1);
        var edges = new Dictionary<(int, int), int>();
        double area = 0;
        for (int i = 0; i < chunk.Indices.Length; i += 3)
        {
            int a = chunk.Indices[i], b = chunk.Indices[i + 1], c = chunk.Indices[i + 2];
            if (a >= count || b >= count || c >= count)
                continue;
            var pa = chunk.Vertices[a].Position;
            var pb = chunk.Vertices[b].Position;
            var pc = chunk.Vertices[c].Position;
            float cross = Vector3.Cross(pb - pa, pc - pa).Y;
            Assert.True(cross > 0);
            area += cross / 2;
            foreach (var (u, v) in new[] { (a, b), (b, c), (c, a) })
            {
                var key = (Math.Min(u, v), Math.Max(u, v));
                edges[key] = edges.GetValueOrDefault(key) + 1;
            }
        }
        Assert.Equal(4, area, 5);
        var boundary = edges.Where(p => p.Value == 1).ToArray();
        Assert.Equal(128, boundary.Length); // 32 edges on each side, at every LOD.
        Assert.All(edges.Values, value => Assert.InRange(value, 1, 2));
        foreach (var edge in boundary)
        {
            var a = chunk.Vertices[edge.Key.Item1].Position;
            var b = chunk.Vertices[edge.Key.Item2].Position;
            Assert.True(a.X == b.X && (a.X == 0 || a.X == 2) || a.Z == b.Z && (a.Z == 0 || a.Z == 2));
        }
        for (int z = 0; z <= 32; z++)
            Assert.Equal(coarse.Vertices[z * 33 + 32], chunk.Vertices[z * (cells / 32) * (cells + 1)]);
    }

    [Theory]
    [InlineData(64)]
    [InlineData(128)]
    public void CameraFollowsActualBoundaryFansAndObjectsKeepTheirIdentity(int cells)
    {
        var terrain = new WorldTerrain(19);
        var mesh = terrain.BuildChunk(new(0, 0), TestContext.Current.CancellationToken, cells);
        int count = (cells + 1) * (cells + 1);
        int checkedTriangles = 0;
        for (int i = 0; i < mesh.Indices.Length; i += 3)
        {
            int ia = mesh.Indices[i], ib = mesh.Indices[i + 1], ic = mesh.Indices[i + 2];
            if (ia >= count || ib >= count || ic >= count)
                continue;
            var a = mesh.Vertices[ia].Position;
            var b = mesh.Vertices[ib].Position;
            var c = mesh.Vertices[ic].Position;
            if (Math.Min(a.X, Math.Min(b.X, c.X)) > .02f && Math.Min(a.Z, Math.Min(b.Z, c.Z)) > .02f)
                continue;
            var p = (a + b + c) / 3;
            // Absolute error avoids false failures at a decimal rounding midpoint.
            Assert.InRange(Math.Abs(p.Y - .5f - terrain.Ground(p.X, p.Z, cells)), 0, 1e-5f);
            checkedTriangles++;
        }
        Assert.True(checkedTriangles > 20);
        var low = terrain.BuildChunk(new(0, 0), TestContext.Current.CancellationToken);
        var lowObjects = low.Vertices.Where(v => v.AnchorHeight.HasValue).ToArray();
        var highObjects = mesh.Vertices.Where(v => v.AnchorHeight.HasValue).ToArray();
        Assert.Equal(lowObjects.Length, highObjects.Length);
        for (int i = 0; i < lowObjects.Length; i++)
        {
            Assert.Equal(lowObjects[i].Position.X, highObjects[i].Position.X);
            Assert.Equal(lowObjects[i].Position.Z, highObjects[i].Position.Z);
            Assert.Equal(lowObjects[i].Color, highObjects[i].Color);
            Assert.Equal(lowObjects[i].Position.Y - lowObjects[i].AnchorHeight!.Value, highObjects[i].Position.Y - highObjects[i].AnchorHeight!.Value, 5);
        }
    }

    [Fact]
    public void LodSelectionUsesHysteresisAndPublishedResolutionForWalking()
    {
        var camera = new CameraPose(Vector3.Zero);
        var initial = WorldLod.Select(camera);
        Assert.Equal(128, initial.Single(c => c.Key == new ChunkKey(0, 0)).Cells);
        Assert.Contains(initial, c => c.Cells == 64);
        Assert.Contains(initial, c => c.Cells == 32);
        var near = new CameraPose(new(3.2f, 0, 1));
        var a = WorldLod.Select(near);
        var b = WorldLod.Select(near with
        {
            Position = new(3.4f, 0, 1)
        }, a);
        Assert.Equal(128, b.Single(c => c.Key == new ChunkKey(0, 0)).Cells);
        var stream = new WorldStream(new WorldTerrain(42));
        stream.PublishDetailed(initial);
        Assert.Equal(128, stream.CellsAt(Vector3.Zero));
        Assert.Equal(144, WorldStream.CacheLimit);
    }

    [Fact]
    public void MoonIlluminationDoublesWithoutChangingDaylight()
    {
        Assert.Equal(.28f, DayNight.Exposure(390), 5);
        Assert.Equal(1, DayNight.Exposure(150), 5);
    }

    [Fact]
    public void AtlasPlacesCameraAtCorrectCardinalCoordinatesAndIsCancelable()
    {
        Assert.Equal(new Vector2(.5f, .5f), WorldAtlas.ToMap(Vector3.Zero));
        Assert.Equal(Vector2.Zero, WorldAtlas.ToMap(new(-32, 100, -32)));
        Assert.Equal(Vector2.One, WorldAtlas.ToMap(new(32, -100, 32)));
        using var token = new CancellationTokenSource();
        token.Cancel();
        Assert.Throws<OperationCanceledException>(() => WorldAtlas.Build(new WorldTerrain(42), token.Token));
    }
}

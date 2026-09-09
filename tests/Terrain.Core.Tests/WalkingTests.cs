using System.Numerics;
using Terrain.Core.Hydrology;
using Terrain.Core.Generation;
using Terrain.Core.Models;
using Terrain.Core.Rendering;
using Xunit;

namespace Terrain.Core.Tests;

public sealed class WalkingTests
{
    private static HeightMap Flat()
    {
        var map = new HeightMap(33, 25);
        Array.Fill(map.Values, .3f);
        return map;
    }
    [Fact]
    public void WalkFollowsSlopingTrianglesAndStopsAtBoundary()
    {
        var map = Flat();
        for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
                map[x, y] = .3f + .1f * (x / 16f - 1);
        var surface = new WalkingSurface(map, null);
        var camera = surface.Spawn(.65f)! with
        {
            Yaw = 90,
            Pitch = 80
        };
        var moved = surface.Move(camera, .2f, .65f);
        Assert.True(moved.Position.X > camera.Position.X);
        Assert.InRange(Math.Abs(moved.Position.Y - ((.3f + .1f * moved.Position.X - .5f) * .65f + WalkingSurface.EyeHeight)), 0, 1e-5f);
        var edge = surface.Move(moved, 5, .65f);
        Assert.Equal(1, edge.Position.X);
        Assert.True(surface.TryGround(edge.Position.X, edge.Position.Z, .65f, out _));
    }
    [Fact]
    public void WaterAndCliffsAllowMovementAboveVisibleSurface()
    {
        var map = Flat();
        var water = HydrologyBuilder.Build(map, new(.5f));
        var surface = new WalkingSurface(map, water);
        var start = surface.Spawn(1)! with
        {
            Yaw = 90
        };
        var moved = surface.Move(start, .8f, 1);
        Assert.InRange(moved.Position.X, .79999f, .80001f);
        Assert.InRange(moved.Position.Y, WalkingSurface.EyeHeight - 1e-6f, WalkingSurface.EyeHeight + 1e-6f);
        map[16, 12] = 1;
        surface = new WalkingSurface(map, null);
        start = surface.Anchor(new CameraPose(new Vector3(-.1f, 0, 0), 90), 1)!;
        moved = surface.Move(start, .1f, 1);
        Assert.InRange(moved.Position.Y, .5f + WalkingSurface.EyeHeight - 1e-5f, .5f + WalkingSurface.EyeHeight + 1e-5f);
    }

    [Fact]
    public void SmallFloodedMapAllowsSpawnAndClampsAllFourEdges()
    {
        var map = new HeightMap(2, 3);
        var water = HydrologyBuilder.Build(map, new(.7f));
        var surface = new WalkingSurface(map, water);
        var camera = surface.Spawn(.65f)!;
        Assert.NotNull(camera);
        foreach (float yaw in new[] { 0f, 90f, 180f, -90f })
        {
            var moved = surface.Move(camera with
            {
                Yaw = yaw
            }, 100, .65f);
            Assert.InRange(moved.Position.X, -.5f, .5f);
            Assert.InRange(moved.Position.Z, -1, 1);
            Assert.True(surface.TryGround(moved.Position.X, moved.Position.Z, .65f, out float height));
            Assert.InRange(Math.Abs(moved.Position.Y - height - WalkingSurface.EyeHeight), 0, 1e-6f);
        }
    }

    [Theory]
    [InlineData(GeneratorKind.Hills)]
    [InlineData(GeneratorKind.Perlin)]
    [InlineData(GeneratorKind.Simplex)]
    [InlineData(GeneratorKind.DiamondSquare)]
    public void DefaultGeneratedMapsOfferForwardMovementAtSpawn(GeneratorKind kind)
    {
        var map = Generators.Generate(new(kind, 129));
        var water = HydrologyBuilder.Build(map, new(.02f, .04f * MathF.Pow(.72f, 5)));
        var surface = new WalkingSurface(map, water);
        var camera = surface.Spawn(.65f);
        Assert.NotNull(camera);
        Assert.True(Vector3.Distance(camera.Position, surface.Move(camera, .1f, .65f).Position) > .01f);
    }

    [Fact]
    public void FreeLookIsInvertibleClampedAndDoesNotTiltWalkingDirection()
    {
        var camera = CameraPose.Default.Look(720 + 35, 120);
        Assert.Equal(89, camera.Pitch);
        Assert.InRange(camera.Yaw, -180, 180);
        var vector = Vector3.Normalize(new Vector3(.3f, -.5f, .7f));
        Assert.True(Vector3.Distance(vector, camera.WorldDirection(camera.ViewDirection(vector))) < 1e-5);
        Assert.Equal(0, camera.Forward.Y);
        Assert.Equal(-89, camera.Look(0, -300).Pitch);
    }
    [Fact]
    public void WalkingSunIsVisibleOnlyWhenLookingTowardsItsWorldDirection()
    {
        var camera = new CameraPose(Vector3.Zero, -90); // sunrise is -X
        Vector3 Color(CameraPose pose) => SunPath.WalkingSkyColor(.5f, .5f, 1, -Vector3.UnitX,
            pose.WorldDirection(Vector3.UnitX), pose.WorldDirection(Vector3.UnitY), pose.WorldDirection(-Vector3.UnitZ), CameraPose.FocalLength, .001f);
        Assert.True(Color(camera).X > .9f);
        Assert.True(Color(camera.Turn(180)).X < .8f);
    }
}

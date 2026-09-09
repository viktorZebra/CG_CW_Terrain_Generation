using System.Numerics;
using Terrain.Core.Models;
using Terrain.Core.Rendering;
using Terrain.Core.Rendering.Shadows;
using Xunit;

namespace Terrain.Core.Tests;

public sealed class CameraTests
{
    [Fact]
    public void StrafingFollowsYawKeepsLookAndDoesNotAccelerateDiagonally()
    {
        var camera = new CameraPose(new Vector3(2, 3, 4), 90, 75);
        var right = camera.Move(0, 1);
        Assert.InRange(Vector3.Distance(right.Position, new Vector3(2, 3, 5)), 0, 1e-5f);
        Assert.Equal(camera.Yaw, right.Yaw);
        Assert.Equal(camera.Pitch, right.Pitch);
        Assert.Equal(camera.Position.Y, right.Position.Y);
        Assert.InRange(Math.Abs(Vector3.Distance(camera.Position, camera.Move(1, 1).Position) - 1), 0, 1e-5f);
        Assert.InRange(Vector3.Distance(camera.Move(0, -1).Position - camera.Position, camera.Position - right.Position), 0, 1e-5f);
        Assert.Equal(camera, camera.Move(0, 0));
    }

    [Fact]
    public void TurningIsInPlaceAndMovementFollowsHeading()
    {
        var camera = CameraPose.Default.Turn(90);
        Assert.Equal(CameraPose.Default.Position, camera.Position);
        Assert.True(Vector3.Distance(camera.Move(2).Position, camera.Position + Vector3.UnitX * 2) < 1e-5);
        Assert.True(Vector3.Distance(camera.Move(2).Move(-2).Position, camera.Position) < 1e-5);
        Assert.True(Vector3.Distance(camera.ViewDirection(camera.Forward), -Vector3.UnitZ) < 1e-5);
        Assert.Equal(0, CameraPose.Default.Turn(360).Yaw);
    }

    [Fact]
    public void ForwardMovementEnlargesTerrainWithoutChangingModelOrShadows()
    {
        var mesh = Mesh.Build(new HeightMap(3, 3));
        var original = new Scene();
        var moved = original with
        {
            Camera = original.ViewCamera.Move(1)
        };
        var before = original.Project(mesh, 1).Vertices;
        var after = moved.Project(mesh, 1).Vertices;
        Assert.True(after.Max(v => v.Position.X) - after.Min(v => v.Position.X)
            > before.Max(v => v.Position.X) - before.Min(v => v.Position.X));
        Assert.Equal(ShadowKey.From(mesh, original), ShadowKey.From(mesh, moved));
        Assert.Equal(original.WorldPosition(mesh.Vertices[0].Position), moved.WorldPosition(mesh.Vertices[0].Position));
        Assert.Equal(original.LightDirection, moved.LightDirection);
    }

    [Fact]
    public void NearPlaneClipsCrossingTriangleAndRejectsGeometryBehindCamera()
    {
        Vertex V(float x, float y, float z) => new(new(x, y, z), Vector3.UnitY, Vector3.One);
        var scene = new Scene(0, 0, 0, HeightScale: 1, Camera: new CameraPose(Vector3.Zero));
        var crossing = new Mesh([V(-.1f, .5f, -.5f), V(.1f, .5f, -.5f), V(0, .6f, .1f)], [0, 1, 2]);
        var projected = scene.Project(crossing, 1);
        Assert.Equal(6, projected.Indices.Length);
        foreach (int index in projected.Indices)
        {
            var vertex = projected.Vertices[index];
            Assert.True(float.IsFinite(vertex.Position.X));
            Assert.InRange(vertex.Position.Z, -1.00001f, 1.00001f);
            Assert.InRange(vertex.ReciprocalW, 1 / CameraPose.Far, 1 / CameraPose.Near + .001f);
        }
        var behind = new Mesh([V(-1, .5f, 1), V(1, .5f, 1), V(0, 1, 1)], [0, 1, 2]);
        Assert.Empty(scene.Project(behind, 1).Indices);
        Assert.Empty((scene with
        {
            Camera = new CameraPose(new Vector3(0, 0, 200))
        }).Project(crossing, 1).Indices);
    }
}

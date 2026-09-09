using System.Numerics;
using Terrain.Core.Models;
using Terrain.Core.Rendering;
using Xunit;
using static Terrain.Core.Tests.TestSupport;

namespace Terrain.Core.Tests;

public sealed class GeometryTests
{
    [Fact]
    public void RectangularMeshHasCompleteAreaValidIndicesAndUpwardNormals()
    {
        var mesh = Mesh.Build(new HeightMap(7, 4));
        Check(mesh.Vertices.Length == 28 && mesh.Indices.Length == 6 * 6 * 3);
        Check(mesh.Indices.All(i => i >= 0 && i < 28));
        float area = 0;
        for (int i = 0; i < mesh.Indices.Length; i += 3)
        {
            var a = mesh.Vertices[mesh.Indices[i]].Position;
            var b = mesh.Vertices[mesh.Indices[i + 1]].Position;
            var c = mesh.Vertices[mesh.Indices[i + 2]].Position;
            var cross = Vector3.Cross(b - a, c - a);
            Check(cross.Y > 0);
            area += cross.Length() / 2;
        }
        Near(area, 2);
        Check(mesh.Vertices.All(v => Vector3.Distance(v.Normal, Vector3.UnitY) < 1e-5));
    }

    [Fact]
    public void YRotationFixesCentrePreservesDistancesFullTurnIsIdentity()
    {
        var scene = new Scene(0, 90, 0);
        var point = new Vector3(2, 3, 4);
        Check(scene.Rotate(Vector3.Zero) == Vector3.Zero);
        Near(scene.Rotate(point).Length(), point.Length());
        Check(Vector3.Distance(scene.Rotate(point), new(4, 3, -2)) < 1e-5);
        Check(Vector3.Distance(new Scene(0, 360, 0).Rotate(point), point) < 1e-5);
    }
}

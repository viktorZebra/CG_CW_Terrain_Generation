using System.Numerics;
using Terrain.Core.Models;
using Terrain.Core.Rendering;
using Xunit;
using static Terrain.Core.Tests.TestSupport;

namespace Terrain.Core.Tests;

public sealed class SoftwareRendererTests
{
    [Fact]
    public void ZBufferResultIsIndependentOfTriangleSubmissionOrder()
    {
        Vertex[] vertices = [V(-.8f,-.8f,.5f,Vector3.UnitX), V(.8f,-.8f,.5f,Vector3.UnitX), V(0,.8f,.5f,Vector3.UnitX),
            V(-.8f,-.8f,-.5f,Vector3.UnitY), V(.8f,-.8f,-.5f,Vector3.UnitY), V(0,.8f,-.5f,Vector3.UnitY)];
        var a = SoftwareRenderer.Render(vertices, [0, 1, 2, 3, 4, 5], Vector3.UnitZ, 64, 64);
        var b = SoftwareRenderer.Render(vertices, [3, 4, 5, 0, 1, 2], Vector3.UnitZ, 64, 64);
        Check(a.Pixels.SequenceEqual(b.Pixels));
        Check(a.Pixels[(32 * 64 + 32) * 4 + 1] == 255);
    }

    [Fact]
    public void SharedEdgeHasNoHolesReversedWindingGivesIdenticalCoverage()
    {
        Vertex[] vertices = [V(-1, -1, 0, Vector3.One), V(1, -1, 0, Vector3.One), V(1, 1, 0, Vector3.One), V(-1, 1, 0, Vector3.One)];
        var a = SoftwareRenderer.Render(vertices, [0, 1, 2, 0, 2, 3], Vector3.UnitZ, 32, 32);
        var b = SoftwareRenderer.Render(vertices, [2, 1, 0, 3, 2, 0], Vector3.UnitZ, 32, 32);
        Check(a.Pixels.All(v => v == 255));
        Check(a.Pixels.SequenceEqual(b.Pixels));
    }

    [Fact]
    public void ViewportDepthClippingAndDegenerateTrianglesAreSafe()
    {
        Vertex[] vertices = [V(-100, -100, 2, Vector3.One), V(100, -100, 2, Vector3.One), V(0, 100, 2, Vector3.One)];
        var empty = SoftwareRenderer.Render(vertices, [], Vector3.UnitZ, 32, 32);
        var outside = SoftwareRenderer.Render(vertices, [0, 1, 2, 0, 0, 0], Vector3.UnitZ, 32, 32);
        Check(empty.Pixels.SequenceEqual(outside.Pixels));
    }
}

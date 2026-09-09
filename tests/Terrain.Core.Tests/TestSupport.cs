using System.Numerics;
using Terrain.Core.Models;

namespace Terrain.Core.Tests;

internal static class TestSupport
{
    public static void Check(bool condition, string message = "Assertion failed") => Xunit.Assert.True(condition, message);
    public static void Near(float actual, float expected, float tolerance = 1e-5f) =>
        Xunit.Assert.True(Math.Abs(actual - expected) < tolerance, $"{actual} != {expected}");
    public static Vertex V(float x, float y, float z, Vector3 color) => new(new(x, y, z), Vector3.UnitZ, color);
    public static Mesh OccluderScene()
    {
        Vertex P(float x, float y, float z) => new(new(x, y, z), Vector3.UnitY, Vector3.One);
        // A small elevated square above a receiving plane: an analytic shadow fixture.
        return new([P(-1,0,-1), P(1,0,-1), P(-1,0,1), P(1,0,1),
            P(-.3f,.5f,-.3f), P(.3f,.5f,-.3f), P(-.3f,.5f,.3f), P(.3f,.5f,.3f)],
            [0, 2, 1, 1, 2, 3, 4, 6, 5, 5, 6, 7]);
    }
}

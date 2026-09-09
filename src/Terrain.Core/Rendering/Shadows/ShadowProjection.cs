using System.Numerics;
using Terrain.Core.Models;
using Terrain.Core.Rendering;

namespace Terrain.Core.Rendering.Shadows;

/// <summary>Orthographic light camera fitted to the whole terrain, independent of the viewing camera.</summary>
public sealed record ShadowProjection(Vector3 Right, Vector3 Up, Vector3 ToLight, Vector3 Min, Vector3 Extent)
{
    public static ShadowProjection Create(Mesh mesh, Scene scene)
    {
        var light = scene.LightDirection;
        var reference = Math.Abs(light.Y) > .95f ? Vector3.UnitZ : Vector3.UnitY;
        var right = Vector3.Normalize(Vector3.Cross(reference, light));
        var up = Vector3.Cross(light, right);
        var min = new Vector3(float.PositiveInfinity);
        var max = new Vector3(float.NegativeInfinity);
        foreach (var vertex in mesh.Vertices)
        {
            var p = scene.WorldVertexPosition(vertex);
            var q = new Vector3(Vector3.Dot(p, right), Vector3.Dot(p, up), -Vector3.Dot(p, light));
            min = Vector3.Min(min, q);
            max = Vector3.Max(max, q);
        }
        // Padding includes PCF neighbours and keeps flat/edge-on maps non-degenerate.
        var padding = Vector3.Max((max - min) * .02f, new Vector3(.02f));
        return new(right, up, light, min - padding, max - min + 2 * padding);
    }

    // UV and depth are all in [0, 1]; UV origin is lower left, as in OpenGL.
    public Vector3 Project(Vector3 world) => (new Vector3(Vector3.Dot(world, Right), Vector3.Dot(world, Up),
        -Vector3.Dot(world, ToLight)) - Min) / Extent;
}

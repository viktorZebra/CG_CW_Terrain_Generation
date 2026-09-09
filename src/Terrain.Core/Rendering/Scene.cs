using System.Numerics;
using Terrain.Core.Models;
using Terrain.Core.Rendering.Shadows;

namespace Terrain.Core.Rendering;

public sealed record Scene(float RotationX = 35, float RotationY = -30, float RotationZ = 0,
    float Zoom = 1, float PanX = 0, float PanY = 0, float HeightScale = .65f,
    Vector3 Light = default, bool Shadows = true, bool SoftShadows = true, int ShadowResolution = 1024, bool ShowSky = false)
{
    // Camera is fixed; model rotation does not rotate the world-space light.
    public Vector3 LightDirection => Light.LengthSquared() > 1e-12f ? Vector3.Normalize(Light) : Vector3.Normalize(new Vector3(-1, 1, 1));
    public Vector3 WorldPosition(Vector3 position) => Rotate(new(position.X, (position.Y - .5f) * HeightScale, position.Z));

    /// <summary>Explicit Euler rotation around the model centre. Angles are degrees.</summary>
    public Vector3 Rotate(Vector3 p)
    {
        float ax = RotationX * MathF.PI / 180, ay = RotationY * MathF.PI / 180, az = RotationZ * MathF.PI / 180;
        p = new(p.X, p.Y * MathF.Cos(ax) - p.Z * MathF.Sin(ax), p.Y * MathF.Sin(ax) + p.Z * MathF.Cos(ax));
        p = new(p.X * MathF.Cos(ay) + p.Z * MathF.Sin(ay), p.Y, -p.X * MathF.Sin(ay) + p.Z * MathF.Cos(ay));
        return new(p.X * MathF.Cos(az) - p.Y * MathF.Sin(az), p.X * MathF.Sin(az) + p.Y * MathF.Cos(az), p.Z);
    }

    public Vertex[] Project(Mesh mesh, float aspect, ShadowProjection? shadowProjection = null)
    {
        var output = new Vertex[mesh.Vertices.Length];
        for (int i = 0; i < output.Length; i++)
        {
            var vertex = mesh.Vertices[i];
            var world = WorldPosition(vertex.Position);
            var p = world;
            // Orthographic camera looks down -Z. Clip depth stays in [-1, 1].
            var clip = new Vector3((p.X * Zoom / 1.65f + PanX) / aspect, p.Y * Zoom / 1.65f + PanY, -p.Z / 8);
            // Inverse transpose for non-uniform vertical scale, followed by rotation.
            var normal = Rotate(new(vertex.Normal.X, vertex.Normal.Y / HeightScale, vertex.Normal.Z));
            output[i] = new(clip, Vector3.Normalize(normal), vertex.Color, shadowProjection?.Project(world) ?? default);
        }
        return output;
    }

    public static Vector3 Shade(Vector3 normal, Vector3 color, Vector3 light, float visibility = 1)
    {
        float length = normal.LengthSquared();
        normal = length > 1e-12f ? normal / MathF.Sqrt(length) : Vector3.UnitY;
        return color * (.3f + .7f * Math.Max(0, Vector3.Dot(normal, light)) * visibility);
    }
}

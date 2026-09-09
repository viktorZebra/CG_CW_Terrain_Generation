using System.Numerics;
using Terrain.Core.Models;
using Terrain.Core.Rendering.Shadows;

namespace Terrain.Core.Rendering;

public sealed record Scene(float RotationX = 35, float RotationY = -30, float RotationZ = 0,
    float Zoom = 1, float PanX = 0, float PanY = 0, float HeightScale = .65f,
    Vector3 Light = default, bool Shadows = true, bool SoftShadows = true, int ShadowResolution = 1024, bool ShowSky = false, CameraPose? Camera = null, bool Walking = false, float FogDensity = 0, float? WorldTime = null, float WorldViewDistance = 5.5f, int? CloudSeed = null, float CloudTime = 0)
{
    public float NearDistance => Walking ? .005f : CameraPose.Near;
    public CameraPose ViewCamera => Camera ?? CameraPose.Default;
    // Model and camera transforms do not rotate the world-space light.
    public Vector3 LightDirection => Light.LengthSquared() > 1e-12f ? Vector3.Normalize(Light) : Vector3.Normalize(new Vector3(-1, 1, 1));
    // The sky disk is a stylized marker at infinity; only camera yaw moves it.
    public Vector3 SkySun
    {
        get
        {
            var direction = ViewCamera.ViewDirection(new(LightDirection.X, LightDirection.Y, -1.5f));
            float distance = Math.Max(.001f, -direction.Z);
            return new(direction.X * 1.5f / distance, direction.Y * 1.5f / distance, direction.Z);
        }
    }
    public Vector3 WorldPosition(Vector3 position) => Rotate(new(position.X, (position.Y - .5f) * HeightScale, position.Z));

    // Terrain scales around its centre; rigid objects follow only their ground anchor.
    public Vector3 WorldVertexPosition(Vertex vertex)
    {
        var p = vertex.Position;
        float y = vertex.AnchorHeight is { } anchor
            ? (anchor - .5f) * HeightScale + (p.Y - anchor)
            : (p.Y - .5f) * HeightScale;
        return Rotate(new(p.X, y, p.Z));
    }

    public Vector3 WorldNormal(Vertex vertex)
    {
        var normal = vertex.Normal;
        if (vertex.AnchorHeight is null)
            normal.Y /= HeightScale;
        return Vector3.Normalize(Rotate(normal));
    }

    /// <summary>Explicit Euler rotation around the model centre. Angles are degrees.</summary>
    public Vector3 Rotate(Vector3 p)
    {
        float ax = RotationX * MathF.PI / 180, ay = RotationY * MathF.PI / 180, az = RotationZ * MathF.PI / 180;
        p = new(p.X, p.Y * MathF.Cos(ax) - p.Z * MathF.Sin(ax), p.Y * MathF.Sin(ax) + p.Z * MathF.Cos(ax));
        p = new(p.X * MathF.Cos(ay) + p.Z * MathF.Sin(ay), p.Y, -p.X * MathF.Sin(ay) + p.Z * MathF.Cos(ay));
        return new(p.X * MathF.Cos(az) - p.Y * MathF.Sin(az), p.X * MathF.Sin(az) + p.Y * MathF.Cos(az), p.Z);
    }

    public Mesh Project(Mesh mesh, float aspect, ShadowProjection? shadowProjection = null)
        => CameraProjection.Project(this, mesh, aspect, shadowProjection);

    public static Vector3 Shade(Vector3 normal, Vector3 color, Vector3 light, float visibility = 1)
    {
        float length = normal.LengthSquared();
        normal = length > 1e-12f ? normal / MathF.Sqrt(length) : Vector3.UnitY;
        return color * (.3f + .7f * Math.Max(0, Vector3.Dot(normal, light)) * visibility);
    }
}

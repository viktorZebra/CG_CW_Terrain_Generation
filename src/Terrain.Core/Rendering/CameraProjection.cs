using System.Numerics;
using Terrain.Core.Models;
using Terrain.Core.Rendering.Shadows;

namespace Terrain.Core.Rendering;

/// <summary>Perspective projection with shared near/far clipping for CPU and OpenGL.</summary>
internal static class CameraProjection
{
    public static Mesh Project(Scene scene, Mesh mesh, float aspect, ShadowProjection? shadow)
    {
        var camera = scene.ViewCamera;
        var view = new Vertex[mesh.Vertices.Length];
        var output = new List<Vertex>(view.Length);
        for (int i = 0; i < view.Length; i++)
        {
            var vertex = mesh.Vertices[i];
            var world = scene.WorldVertexPosition(vertex);
            var normal = scene.WorldNormal(vertex);
            view[i] = new(camera.ViewPosition(world), normal, vertex.Color, shadow?.Project(world) ?? default);
            output.Add(ProjectVertex(view[i], scene, aspect));
        }
        var indices = new List<int>(mesh.Indices.Length);
        Span<Vertex> first = stackalloc Vertex[6];
        Span<Vertex> second = stackalloc Vertex[6];
        for (int i = 0; i < mesh.Indices.Length; i += 3)
        {
            int a = mesh.Indices[i], b = mesh.Indices[i + 1], c = mesh.Indices[i + 2];
            if (Inside(view[a], scene.NearDistance) && Inside(view[b], scene.NearDistance) && Inside(view[c], scene.NearDistance))
            {
                indices.Add(a);
                indices.Add(b);
                indices.Add(c);
                continue;
            }
            first[0] = view[a];
            first[1] = view[b];
            first[2] = view[c];
            int count = Clip(first[..3], second, -scene.NearDistance, true);
            count = Clip(second[..count], first, -CameraPose.Far, false);
            if (count < 3)
                continue;
            int start = output.Count;
            for (int j = 0; j < count; j++)
                output.Add(ProjectVertex(first[j], scene, aspect));
            for (int j = 1; j < count - 1; j++)
            {
                indices.Add(start);
                indices.Add(start + j);
                indices.Add(start + j + 1);
            }
        }
        return new(output.ToArray(), indices.ToArray());
    }

    private static bool Inside(Vertex vertex, float near) => vertex.Position.Z <= -near && vertex.Position.Z >= -CameraPose.Far;

    private static Vertex ProjectVertex(Vertex vertex, Scene scene, float aspect)
    {
        float distance = Math.Max(scene.NearDistance, -vertex.Position.Z);
        float inverse = 1 / distance;
        float z = (CameraPose.Far + scene.NearDistance) / (CameraPose.Far - scene.NearDistance)
            - 2 * CameraPose.Far * scene.NearDistance / (CameraPose.Far - scene.NearDistance) * inverse;
        return vertex with
        {
            Position = new((vertex.Position.X * CameraPose.FocalLength * scene.Zoom * inverse + scene.PanX) / aspect,
                vertex.Position.Y * CameraPose.FocalLength * scene.Zoom * inverse + scene.PanY, z),
            ReciprocalW = inverse
        };
    }

    private static int Clip(ReadOnlySpan<Vertex> input, Span<Vertex> output, float plane, bool less)
    {
        if (input.IsEmpty)
            return 0;
        int count = 0;
        var previous = input[^1];
        bool previousInside = less ? previous.Position.Z <= plane : previous.Position.Z >= plane;
        foreach (var current in input)
        {
            bool currentInside = less ? current.Position.Z <= plane : current.Position.Z >= plane;
            if (currentInside != previousInside)
            {
                float t = (plane - previous.Position.Z) / (current.Position.Z - previous.Position.Z);
                output[count++] = new(Vector3.Lerp(previous.Position, current.Position, t) with
                {
                    Z = plane
                },
                    Vector3.Lerp(previous.Normal, current.Normal, t), Vector3.Lerp(previous.Color, current.Color, t),
                    Vector3.Lerp(previous.ShadowPosition, current.ShadowPosition, t));
            }
            if (currentInside)
                output[count++] = current;
            previous = current;
            previousInside = currentInside;
        }
        return count;
    }
}

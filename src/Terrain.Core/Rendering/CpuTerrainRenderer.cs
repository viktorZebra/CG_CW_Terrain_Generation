using Terrain.Core.Models;
using Terrain.Core.Rendering.Shadows;

namespace Terrain.Core.Rendering;

/// <summary>Cache the CPU light pass until mesh, model rotation/height, light or resolution changes.</summary>
public sealed class CpuTerrainRenderer
{
    private readonly object gate = new();
    private ShadowKey? key;
    private ShadowMap? shadowMap;
    private (Mesh Mesh, Scene Scene, float Aspect, ShadowProjection? Shadow)? projectionKey;
    private Mesh? projectedMesh;
    public int ShadowBuildCount
    {
        get; private set;
    }

    public ShadowMap GetShadowMap(Mesh mesh, Scene scene)
    {
        lock (gate)
        {
            var nextKey = ShadowKey.From(mesh, scene);
            if (key != nextKey)
            {
                shadowMap = ShadowMap.Build(mesh, scene);
                key = nextKey;
                ShadowBuildCount++;
            }
            return shadowMap!;
        }
    }

    public Frame Render(Mesh mesh, Scene scene, int width, int height)
    {
        var shadows = scene.Shadows ? GetShadowMap(mesh, scene) : null;
        Mesh projected;
        lock (gate)
        {
            var projectionScene = scene with
            {
                WorldTime = null,
                CloudSeed = null,
                CloudTime = 0,
                WorldViewDistance = 5.5f,
                Light = default,
                ShowSky = false,
                FogDensity = 0
            };
            var next = (mesh, projectionScene, (float)width / height, shadows?.Projection);
            if (projectionKey != next)
            {
                projectedMesh = scene.Project(mesh, (float)width / height, shadows?.Projection);
                projectionKey = next;
            }
            projected = projectedMesh!;
        }
        return SoftwareRenderer.Render(projected.Vertices, projected.Indices,
            scene.LightDirection, width, height, shadows, scene.SoftShadows, scene.ShowSky, scene.SkySun, scene);
    }
}

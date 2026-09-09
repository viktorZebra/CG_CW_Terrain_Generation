using Terrain.Core.Models;
using Terrain.Core.Rendering.Shadows;

namespace Terrain.Core.Rendering;

/// <summary>Cache the CPU light pass until mesh, model rotation/height, light or resolution changes.</summary>
public sealed class CpuTerrainRenderer
{
    private readonly object gate = new();
    private ShadowKey? key;
    private ShadowMap? shadowMap;
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
        return SoftwareRenderer.Render(scene.Project(mesh, (float)width / height, shadows?.Projection), mesh.Indices,
            scene.LightDirection, width, height, shadows, scene.SoftShadows, scene.ShowSky);
    }
}

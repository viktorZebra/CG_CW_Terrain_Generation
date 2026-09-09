using System.Numerics;
using Terrain.Core.Models;
using Terrain.Core.Rendering;

namespace Terrain.Core.Rendering.Shadows;

public readonly record struct ShadowKey(Mesh Mesh, float HeightScale, Vector3 Rotation, Vector3 Light, int Resolution)
{
    public static ShadowKey From(Mesh mesh, Scene scene) => new(mesh, scene.HeightScale,
        new(scene.RotationX, scene.RotationY, scene.RotationZ), scene.LightDirection, scene.ShadowResolution);
}

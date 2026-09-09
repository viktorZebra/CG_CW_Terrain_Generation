using System.Numerics;
namespace Terrain.Core.Rendering;

public static class Atmosphere
{
    public static Vector3 Color(Vector3 light) => Vector3.Lerp(new(.62f, .48f, .40f), new(.49f, .67f, .76f), Math.Clamp(light.Y, 0, 1));
    public static float Amount(float distance, float density) => density <= 0 ? 0 : 1 - MathF.Exp(-density * Math.Max(0, distance - .3f));
    public static float Distance(float x, float y, float aspect, float inverseW, Scene scene)
    {
        float z = 1 / inverseW;
        float focal = CameraPose.FocalLength * scene.Zoom;
        return new Vector3(((2 * x - 1) * aspect - scene.PanX) * z / focal, ((1 - 2 * y) - scene.PanY) * z / focal, z).Length();
    }
}

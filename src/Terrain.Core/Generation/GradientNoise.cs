
namespace Terrain.Core.Generation;

/// <summary>Explicit 2D gradient noise: quintic Perlin and triangular-lattice Simplex.</summary>
public sealed class GradientNoise
{
    private readonly int[] permutation = new int[512];
    private static readonly (float X, float Y)[] Gradients =
        [(1, 0), (-1, 0), (0, 1), (0, -1), (.70710678f, .70710678f),
         (-.70710678f, .70710678f), (.70710678f, -.70710678f), (-.70710678f, -.70710678f)];
    public GradientNoise(int seed)
    {
        var random = new Random(seed);
        int[] values = Enumerable.Range(0, 256).ToArray();
        for (int i = 255; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }
        for (int i = 0; i < 512; i++)
            permutation[i] = values[i & 255];
    }
    private float Dot(int x, int y, float dx, float dy)
    {
        var g = Gradients[permutation[(x & 255) + permutation[y & 255]] & 7];
        return g.X * dx + g.Y * dy;
    }
    private static float Fade(float t) => t * t * t * (t * (t * 6 - 15) + 10);
    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    public float Perlin(float x, float y)
    {
        int ix = (int)MathF.Floor(x), iy = (int)MathF.Floor(y);
        float dx = x - ix, dy = y - iy;
        return Lerp(Lerp(Dot(ix, iy, dx, dy), Dot(ix + 1, iy, dx - 1, dy), Fade(dx)),
            Lerp(Dot(ix, iy + 1, dx, dy - 1), Dot(ix + 1, iy + 1, dx - 1, dy - 1), Fade(dx)), Fade(dy));
    }
    public float Simplex(float x, float y)
    {
        const float f = .3660254038f, g = .2113248654f;
        float skew = (x + y) * f;
        int i = (int)MathF.Floor(x + skew), j = (int)MathF.Floor(y + skew);
        float unskew = (i + j) * g;
        float x0 = x - (i - unskew), y0 = y - (j - unskew);
        int i1 = x0 > y0 ? 1 : 0, j1 = 1 - i1;
        float Corner(int cx, int cy, float dx, float dy)
        {
            float t = .5f - dx * dx - dy * dy;
            return t <= 0 ? 0 : t * t * t * t * Dot(cx, cy, dx, dy);
        }
        return 70 * (Corner(i, j, x0, y0) + Corner(i + i1, j + j1, x0 - i1 + g, y0 - j1 + g) +
            Corner(i + 1, j + 1, x0 - 1 + 2 * g, y0 - 1 + 2 * g));
    }
}

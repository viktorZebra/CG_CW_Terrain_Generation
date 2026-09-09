
namespace Terrain.Core.Models;

/// <summary>Row-major heights in [0, 1]. No UI or graphics-library dependencies.</summary>
public sealed class HeightMap
{
    public int Width
    {
        get;
    }
    public int Height
    {
        get;
    }
    public float[] Values
    {
        get;
    }
    public float this[int x, int y] { get => Values[y * Width + x]; set => Values[y * Width + x] = value; }

    public HeightMap(int width, int height)
    {
        if (width < 2 || height < 2 || width > 2049 || height > 2049)
            throw new ArgumentOutOfRangeException(nameof(width), "Размер карты: от 2 до 2049 по каждой стороне.");
        Width = width;
        Height = height;
        Values = new float[width * height];
    }

    public void Normalize()
    {
        float min = Values.Min(), max = Values.Max();
        if (!float.IsFinite(min) || !float.IsFinite(max))
            throw new InvalidOperationException("Некорректные высоты.");
        float range = max - min;
        for (int i = 0; i < Values.Length; i++)
            Values[i] = range > 0 ? (Values[i] - min) / range : 0;
    }

    public HeightMap Smooth()
    {
        // Read only from the source. At an edge, average only existing neighbours.
        var result = new HeightMap(Width, Height);
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                float sum = 0;
                int count = 0;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        if (x + dx >= 0 && x + dx < Width && y + dy >= 0 && y + dy < Height)
                        {
                            sum += this[x + dx, y + dy];
                            count++;
                        }
                result[x, y] = sum / count;
            }
        return result;
    }

    public void Valley()
    {
        for (int i = 0; i < Values.Length; i++)
            Values[i] = MathF.Sqrt(Math.Clamp(Values[i], 0, 1));
    }
}

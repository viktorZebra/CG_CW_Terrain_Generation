using Terrain.Core.Models;

namespace Terrain.Core.Hydrology;

/// <summary>Priority-Flood, D8 drainage, accumulated rainfall and river/shore masks.</summary>
public static class HydrologyBuilder
{
    private static readonly (int X, int Y)[] Neighbors = [(-1, -1), (0, -1), (1, -1), (-1, 0), (1, 0), (-1, 1), (0, 1), (1, 1)];

    public static WaterMap Build(HeightMap map, HydrologyOptions options)
    {
        if (!float.IsFinite(options.SeaLevel) || options.SeaLevel < 0 || options.SeaLevel > 1 ||
            !float.IsFinite(options.RiverFraction) || options.RiverFraction <= 0 || options.RiverFraction > 1 || !float.IsFinite(options.ChannelDepth) || options.ChannelDepth is < 0 or > .08f || (!options.SeparateSurface && options.ChannelDepth != 0))
            throw new ArgumentOutOfRangeException(nameof(options));
        if (map.Values.Any(h => !float.IsFinite(h) || h < 0 || h > 1))
            throw new ArgumentException("Высоты должны быть конечными числами от 0 до 1.", nameof(map));
        int width = map.Width, height = map.Height, count = map.Values.Length;
        var levels = new float[count];
        var visited = new bool[count];
        var downstream = new int[count];
        Array.Fill(downstream, -1);
        var order = new int[count];
        var rank = new int[count];
        // Index breaks ties reproducibly, including on large flat surfaces.
        var queue = new PriorityQueue<int, (float Height, int Index)>();
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                if (x == 0 || y == 0 || x == width - 1 || y == height - 1)
                {
                    int i = y * width + x;
                    visited[i] = true;
                    levels[i] = Math.Max(map.Values[i], options.SeaLevel);
                    queue.Enqueue(i, (levels[i], i));
                }
        int next = 0;
        while (queue.TryDequeue(out int current, out _))
        {
            order[next] = current;
            rank[current] = next++;
            int x = current % width, y = current / width;
            foreach (var (dx, dy) in Neighbors)
            {
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                    continue;
                int neighbor = ny * width + nx;
                if (visited[neighbor])
                    continue;
                visited[neighbor] = true;
                levels[neighbor] = Math.Max(map.Values[neighbor], levels[current]);
                downstream[neighbor] = current;
                queue.Enqueue(neighbor, (levels[neighbor], neighbor));
            }
        }
        // On slopes use the steepest descent per unit distance. On flats keep the
        // flood parent, which is earlier in rank and guarantees a path to an outlet.
        for (int y = 1; y < height - 1; y++)
            for (int x = 1; x < width - 1; x++)
            {
                int i = y * width + x;
                float best = 0;
                foreach (var (dx, dy) in Neighbors)
                {
                    int neighbor = (y + dy) * width + x + dx;
                    float slope = (levels[i] - levels[neighbor]) / (dx != 0 && dy != 0 ? MathF.Sqrt(2) : 1);
                    if (slope > best && rank[neighbor] < rank[i])
                    {
                        best = slope;
                        downstream[i] = neighbor;
                    }
                }
            }
        var accumulation = new int[count];
        Array.Fill(accumulation, 1);
        for (int i = count - 1; i >= 0; i--)
        {
            int cell = order[i], destination = downstream[cell];
            if (destination >= 0)
                accumulation[destination] += accumulation[cell];
        }
        var standing = new bool[count];
        var coverage = new float[count];
        for (int i = 0; i < count; i++)
        {
            standing[i] = levels[i] > map.Values[i] + 1e-6f || map.Values[i] <= options.SeaLevel;
            if (standing[i])
                coverage[i] = 1;
        }
        int threshold = Math.Max(8, (int)MathF.Ceiling(count * options.RiverFraction));
        float mapScale = Math.Max(width, height) / 129f;
        for (int i = 0; i < count; i++)
        {
            if (accumulation[i] < threshold || standing[i])
                continue;
            int x = i % width, y = i / width;
            float radius = Math.Max(.75f, mapScale * (.65f + .35f * MathF.Log2((float)accumulation[i] / threshold)));
            radius = Math.Min(radius, Math.Max(1, mapScale * 3));
            int reach = (int)MathF.Ceiling(radius);
            for (int dy = -reach; dy <= reach; dy++)
                for (int dx = -reach; dx <= reach; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                        continue;
                    int neighbor = ny * width + nx;
                    // Widen along the valley floor; never smear a channel over a ridge.
                    if (map.Values[neighbor] > levels[i] + .015f)
                        continue;
                    float mask = Math.Clamp(radius + .5f - MathF.Sqrt(dx * dx + dy * dy), 0, 1);
                    coverage[neighbor] = Math.Max(coverage[neighbor], mask);
                }
            coverage[i] = 1;
        }
        // Grid distance is sufficient for an artistic moisture/shore falloff.
        var distance = new int[count];
        Array.Fill(distance, int.MaxValue);
        var wetQueue = new Queue<int>();
        for (int i = 0; i < count; i++)
            if (coverage[i] >= .5f)
            {
                distance[i] = 0;
                wetQueue.Enqueue(i);
            }
        while (wetQueue.TryDequeue(out int current))
        {
            int x = current % width, y = current / width;
            foreach (var (dx, dy) in Neighbors)
            {
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                    continue;
                int neighbor = ny * width + nx;
                if (distance[neighbor] <= distance[current] + 1)
                    continue;
                distance[neighbor] = distance[current] + 1;
                wetQueue.Enqueue(neighbor);
            }
        }
        var moisture = new float[count];
        var bank = new float[count];
        for (int i = 0; i < count; i++)
        {
            moisture[i] = MathF.Exp(-distance[i] / Math.Max(1, mapScale * 5));
            bank[i] = Math.Clamp(1 - distance[i] / Math.Max(1.5f, mapScale * 2), 0, 1) * (1 - coverage[i]);
        }
        return new(width, height, levels, downstream, accumulation, standing, coverage, moisture, bank);
    }
}

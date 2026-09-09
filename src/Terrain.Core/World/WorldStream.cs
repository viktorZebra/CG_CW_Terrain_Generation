using System.Numerics;
using Terrain.Core.Models;
using Terrain.Core.Rendering;

namespace Terrain.Core.World;

/// <summary>Single-worker bounded chunk cache. The UI owns scheduling and cancellation.</summary>
public sealed class WorldStream(WorldTerrain terrain)
{
    public const int CacheLimit = 144;
    private readonly Dictionary<ChunkDetail, Mesh> cache = new();
    private readonly HashSet<ChunkKey> published = new();
    private readonly Dictionary<ChunkKey, int> publishedCells = new();
    public int CellsAt(Vector3 position) => publishedCells.GetValueOrDefault(Key(position), WorldTerrain.Cells);
    public CameraPose Anchor(CameraPose camera) => terrain.Anchor(camera, CellsAt(camera.Position));
    public int CachedCount => cache.Count;
    public bool HasGround(Vector3 position) => published.Contains(Key(position));
    public static ChunkKey Key(Vector3 p) => new((int)MathF.Floor(p.X / WorldTerrain.ChunkSize), (int)MathF.Floor(p.Z / WorldTerrain.ChunkSize));

    /// <summary>Conservative forward visibility in the currently published geometry, including after a turn.</summary>
    public float ViewDistance(CameraPose camera)
    {
        float safe = 9;
        for (int angle = -80; angle <= 80; angle += 10)
        {
            var ray = camera.Turn(angle).Forward;
            for (float distance = .25f; distance <= safe + 1; distance += .25f)
                if (!HasGround(camera.Position + ray * distance))
                {
                    safe = Math.Max(1, distance - 1);
                    break;
                }
        }
        return safe;
    }

    public static ChunkKey[] Select(CameraPose camera)
    {
        var center = Key(camera.Position);
        var result = new List<ChunkKey>();
        for (int z = center.Z - 6; z <= center.Z + 6; z++)
            for (int x = center.X - 6; x <= center.X + 6; x++)
            {
                // Render a guard band outside walkable bounds so no terrain edge is exposed.
                if (x is < -22 or >= 22 || z is < -22 or >= 22)
                    continue;
                var delta = new Vector3((x + .5f) * 2 - camera.Position.X, 0, (z + .5f) * 2 - camera.Position.Z);
                float radius = Vector3.Dot(delta, camera.Forward) > 0 ? 10 : 6;
                if (delta.Length() <= radius + 1.42f)
                    result.Add(new(x, z));
            }
        return result.OrderBy(k => Vector2.DistanceSquared(new(k.X * 2 + 1, k.Z * 2 + 1), new(camera.Position.X, camera.Position.Z))).ToArray();
    }

    public Mesh Build(ChunkKey[] wanted, CancellationToken token, IProgress<int>? progress = null)
        => BuildDetailed(wanted.Select(k => new ChunkDetail(k, WorldTerrain.Cells)).ToArray(), token, progress);

    public Mesh BuildDetailed(ChunkDetail[] wanted, CancellationToken token, IProgress<int>? progress = null)
    {
        if (wanted.Length > CacheLimit || wanted.Length == 0 || wanted.Select(c => c.Key).Distinct().Count() != wanted.Length)
            throw new ArgumentException("Expected a bounded set of unique chunks.", nameof(wanted));
        int done = 0;
        foreach (var key in wanted)
        {
            token.ThrowIfCancellationRequested();
            if (!cache.ContainsKey(key))
                cache[key] = terrain.BuildChunk(key.Key, token, key.Cells);
            progress?.Report(++done * 100 / wanted.Length);
        }
        // Only preserve a bounded neighbourhood. Eviction cannot change future regenerated chunks.
        foreach (var key in cache.Keys.Where(k => !wanted.Contains(k)).ToArray())
            if (cache.Count > CacheLimit || key.Cells > WorldTerrain.Cells)
                cache.Remove(key);
        token.ThrowIfCancellationRequested();
        int count = wanted.Sum(k => cache[k].Vertices.Length), triangles = wanted.Sum(k => cache[k].Indices.Length);
        var vertices = new Vertex[count];
        var indices = new int[triangles];
        int vertexOffset = 0, indexOffset = 0, trees = 0, rocks = 0, shrubs = 0;
        foreach (var key in wanted)
        {
            var mesh = cache[key];
            mesh.Vertices.CopyTo(vertices, vertexOffset);
            foreach (int index in mesh.Indices)
                indices[indexOffset++] = index + vertexOffset;
            vertexOffset += mesh.Vertices.Length;
            trees += mesh.TreeCount;
            rocks += mesh.RockCount;
            shrubs += mesh.ShrubCount;
        }
        return new(vertices, indices, trees, rocks, ShrubCount: shrubs);
    }

    public void PublishDetailed(IEnumerable<ChunkDetail> chunks)
    {
        published.Clear();
        publishedCells.Clear();
        foreach (var chunk in chunks)
        {
            published.Add(chunk.Key);
            publishedCells[chunk.Key] = chunk.Cells;
        }
    }

    // Called on the UI thread only after publishing the finished mesh.
    public void Publish(IEnumerable<ChunkKey> keys)
    {
        publishedCells.Clear();
        published.Clear();
        published.UnionWith(keys);
        foreach (var key in published)
            publishedCells[key] = WorldTerrain.Cells;
    }
}

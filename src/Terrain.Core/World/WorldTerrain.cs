using System.Numerics;
using Terrain.Core.Generation;
using Terrain.Core.Models;
using Terrain.Core.Rendering;
using Terrain.Core.Vegetation;

namespace Terrain.Core.World;

public enum WorldBiome
{
    Forest, Desert, Winter, Swamp, Steppe
}
public readonly record struct WorldSample(float Height, Vector3 Color, WorldBiome Biome, bool Water, float WaterLevel = .045f, float WaterCoverage = 1);
public readonly record struct ChunkKey(int X, int Z);

/// <summary>Continuous seeded fields in world coordinates; chunk order never changes the terrain.</summary>
public sealed class WorldTerrain
{
    public const float HalfExtent = 32;
    public const float ChunkSize = 2;
    public const int Cells = 32;
    public const float Step = ChunkSize / Cells;
    private readonly GradientNoise noise;
    private readonly WorldClimate climate;
    private readonly Vector3[] peaks;
    public WorldRivers Rivers
    {
        get;
    }
    private readonly Lazy<IReadOnlyList<Vector2>> biomeExamples;
    public int Seed
    {
        get;
    }
    // Diagnostic locations only: these points do not participate in generation.
    public IReadOnlyList<Vector2> BiomeExamples => biomeExamples.Value;

    public WorldTerrain(int seed)
    {
        Seed = seed;
        noise = new(seed);
        climate = new(seed);
        var random = new Random(seed);
        peaks = Enumerable.Range(0, 3).Select(i => new Vector3(-20 + i * 20 + (float)random.NextDouble() * 4 - 2,
            1.5f + (float)random.NextDouble(), -27 + (float)random.NextDouble() * 3)).ToArray();
        Rivers = new WorldRivers(peaks, noise, (x, z) => BaseSample(x, z).Height);
        biomeExamples = new(FindBiomeExamples);
    }

    private IReadOnlyList<Vector2> FindBiomeExamples()
    {
        var points = new Vector2[5];
        var scores = Enumerable.Repeat(float.NegativeInfinity, 5).ToArray();
        float[] targetZ = [0, 29, -29, 0, 16];
        for (int z = -30; z <= 30; z++)
            for (int x = -30; x <= 30; x++)
            {
                var sample = Sample(x, z);
                int i = (int)sample.Biome;
                float score = climate.Sample(x, z)[i] - .0002f * (x * x + (z - targetZ[i]) * (z - targetZ[i]));
                if (i == (int)WorldBiome.Swamp && sample.Water)
                    score += .2f;
                if (score > scores[i])
                {
                    scores[i] = score;
                    points[i] = new(x, z);
                }
            }
        return Array.AsReadOnly(points);
    }

    public WorldSample Sample(float x, float z)
    {
        var sample = BaseSample(x, z);
        var river = Rivers.Sample(x, z);
        if (river.Bank <= 0)
            return sample;
        float bottom = Math.Min(sample.Height, river.Level - .085f);
        float height = sample.Height + (bottom - sample.Height) * river.Bank;
        float level = river.Level;
        float coverage = river.Coverage;
        return sample with
        {
            Height = height,
            Water = coverage > 0 && height < level,
            WaterLevel = level,
            WaterCoverage = coverage
        };
    }

    private WorldSample BaseSample(float x, float z)
    {
        var weights = climate.Sample(x, z);
        int biome = 0;
        for (int i = 1; i < 5; i++)
            if (weights[i] > weights[biome])
                biome = i;
        float broad = .5f + .5f * noise.Perlin(x * .18f + 12, z * .18f - 7);
        float fine = noise.Perlin(x * 1.2f, z * 1.2f);
        float ridge = MathF.Pow(1 - Math.Abs(noise.Perlin(x * .35f, z * .35f)), 3);
        float highlands = Math.Clamp((1 - weights[1] - weights[3] - .55f) / .35f, 0, 1);
        highlands = highlands * highlands * (3 - 2 * highlands);
        float forest = .16f + broad * .2f + highlands * (broad * .5f + ridge * .55f) + fine * .06f;
        float desert = .12f + .075f * MathF.Sin(x * 2 + noise.Perlin(x * .2f, z * .2f) * 5) + fine * .018f;
        float mountains = 0;
        foreach (var peak in peaks)
        {
            float radius = Vector2.Distance(new(x, z), new(peak.X, peak.Z)) / 5.5f;
            float dome = Math.Max(0, 1 - radius * radius);
            mountains += peak.Y * dome * dome * dome;
        }
        float winter = .25f + highlands * (.6f + broad * .25f + mountains) + fine * .025f;
        float swamp = .035f + noise.Perlin(x * .7f, z * .7f) * .055f;
        float steppe = .12f + broad * .28f + fine * .025f;
        float height = weights[0] * forest + weights[1] * desert + weights[2] * winter + weights[3] * swamp + weights[4] * steppe;
        Vector3 color = weights[0] * new Vector3(.20f, .38f, .16f) + weights[1] * new Vector3(.77f, .59f, .30f)
            + weights[2] * new Vector3(.84f, .89f, .91f) + weights[3] * new Vector3(.22f, .30f, .12f)
            + weights[4] * new Vector3(.55f, .52f, .25f);
        color *= 1 + fine * .15f;
        return new(height, color, (WorldBiome)biome, weights[3] > .65f && height < .045f, .045f, weights[3] > .65f ? 1 : -1);
    }

    // Same triangle interpolation as the chunk mesh, including at negative coordinates.
    public float Ground(float x, float z, int cells = Cells)
    {
        float step = ChunkSize / cells;
        float localX = x - MathF.Floor(x / ChunkSize) * ChunkSize;
        float localZ = z - MathF.Floor(z / ChunkSize) * ChunkSize;
        if (cells > Cells && (localX < step || localZ < step || localX > ChunkSize - step || localZ > ChunkSize - step))
            return StitchedGround(x, z, cells);
        float gx = MathF.Floor(x / step), gz = MathF.Floor(z / step);
        float u = x / step - gx, v = z / step - gz;
        float a = Sample(gx * step, gz * step).Height;
        float b = Sample((gx + 1) * step, gz * step).Height;
        float c = Sample(gx * step, (gz + 1) * step).Height;
        float d = Sample((gx + 1) * step, (gz + 1) * step).Height;
        return u + v <= 1 ? a + (b - a) * u + (c - a) * v : d + (c - d) * (1 - u) + (b - d) * (1 - v);
    }

    private float StitchedGround(float x, float z, int cells)
    {
        float originX = MathF.Floor(x / ChunkSize) * ChunkSize, originZ = MathF.Floor(z / ChunkSize) * ChunkSize;
        float step = ChunkSize / cells;
        var point = new Vector2((x - originX) / step, (z - originZ) / step);
        int px = (int)point.X, pz = (int)point.Y, reach = cells / Cells;
        Vector2 GridPoint(int index) => new(index % (cells + 1), index / (cells + 1));
        float? Triangle(int a, int b, int c)
        {
            var pa = GridPoint(a);
            var pb = GridPoint(b);
            var pc = GridPoint(c);
            if (!WorldLod.Weights(point, pa, pb, pc, out var weights))
                return null;
            return weights.X * Sample(originX + pa.X * step, originZ + pa.Y * step).Height
                + weights.Y * Sample(originX + pb.X * step, originZ + pb.Y * step).Height
                + weights.Z * Sample(originX + pc.X * step, originZ + pc.Y * step).Height;
        }
        for (int iz = Math.Max(0, pz - reach); iz <= Math.Min(cells - 1, pz + reach); iz++)
            for (int ix = Math.Max(0, px - reach); ix <= Math.Min(cells - 1, px + reach); ix++)
            {
                int a = WorldLod.Index(ix, iz, cells), b = WorldLod.Index(ix + 1, iz, cells);
                int c = WorldLod.Index(ix, iz + 1, cells), d = WorldLod.Index(ix + 1, iz + 1, cells);
                var height = Triangle(a, c, b) ?? Triangle(b, c, d);
                if (height.HasValue)
                    return height.Value;
            }
        throw new InvalidOperationException("Point outside stitched terrain topology.");
    }

    public CameraPose Anchor(CameraPose camera, int cells = Cells)
    {
        float x = Math.Clamp(camera.Position.X, -HalfExtent + .02f, HalfExtent - .02f);
        float z = Math.Clamp(camera.Position.Z, -HalfExtent + .02f, HalfExtent - .02f);
        float ground = Ground(x, z, cells);
        if (Sample(x, z).Water)
            ground = Math.Max(ground, Sample(x, z).WaterLevel);
        return camera with
        {
            Position = new(x, ground + .12f, z)
        };
    }

    public Mesh BuildChunk(ChunkKey key, CancellationToken cancellation = default, int cells = Cells)
    {
        if (cells is not (Cells or WorldLod.MediumCells or WorldLod.NearCells))
            throw new ArgumentOutOfRangeException(nameof(cells));
        float step = ChunkSize / cells;
        var vertices = new List<Vertex>();
        var indices = new List<int>();
        for (int z = 0; z <= cells; z++)
        {
            cancellation.ThrowIfCancellationRequested();
            for (int x = 0; x <= cells; x++)
            {
                float wx = key.X * ChunkSize + x * step, wz = key.Z * ChunkSize + z * step;
                var sample = Sample(wx, wz);
                var normal = Vector3.Normalize(new Vector3(Sample(wx - Step, wz).Height - Sample(wx + Step, wz).Height,
                    2 * Step, Sample(wx, wz - Step).Height - Sample(wx, wz + Step).Height));
                var color = Vector3.Lerp(new(.32f, .34f, .34f), sample.Color, Math.Clamp((normal.Y - .4f) * 2, 0, 1));
                vertices.Add(new(new(wx, sample.Height + .5f, wz), normal, color));
            }
        }
        for (int z = 0; z < cells; z++)
            for (int x = 0; x < cells; x++)
            {
                int a = WorldLod.Index(x, z, cells), b = WorldLod.Index(x + 1, z, cells);
                int c = WorldLod.Index(x, z + 1, cells), d = WorldLod.Index(x + 1, z + 1, cells);
                Triangle(a, c, b);
                Triangle(b, c, d);
            }
        void Triangle(int a, int b, int c)
        {
            if (a == b || b == c || c == a)
                return;
            indices.AddRange([a, b, c]);
            Water(a, b, c);
        }
        void Water(int a, int b, int c)
        {
            // Clip the same land triangle against water depth and the river corridor.
            // Interpolated levels remain identical along shared LOD edges.
            var polygon = new List<(Vector3 Position, float Level, float Coverage)>();
            foreach (int index in new[] { a, b, c })
            {
                var p = vertices[index].Position;
                var sample = Sample(p.X, p.Z);
                float coverage = sample.WaterCoverage;
                polygon.Add((p, sample.WaterLevel + .5f, coverage));
            }
            for (int pass = 0; pass < 2; pass++)
            {
                if (polygon.Count == 0)
                    return;
                var output = new List<(Vector3 Position, float Level, float Coverage)>();
                var previous = polygon[^1];
                float Signed((Vector3 Position, float Level, float Coverage) p) => pass == 0 ? p.Coverage : p.Level - p.Position.Y;
                foreach (var current in polygon)
                {
                    float before = Signed(previous), after = Signed(current);
                    if ((before > 0) != (after > 0))
                    {
                        float t = before / (before - after);
                        output.Add((Vector3.Lerp(previous.Position, current.Position, t),
                            previous.Level + (current.Level - previous.Level) * t,
                            previous.Coverage + (current.Coverage - previous.Coverage) * t));
                    }
                    if (after > 0)
                        output.Add(current);
                    previous = current;
                }
                polygon = output;
            }
            if (polygon.Count < 3)
                return;
            int start = vertices.Count;
            foreach (var p in polygon)
                vertices.Add(new(new(p.Position.X, p.Level + .0005f, p.Position.Z), Vector3.UnitY, new(.12f, .34f, .40f)));
            for (int n = 1; n < polygon.Count - 1; n++)
                indices.AddRange([start, start + n, start + n + 1]);
        }
        var random = new Random(unchecked(Seed ^ key.X * 73856093 ^ key.Z * 19349663));
        var objects = new List<ObjectInstance>();
        // One candidate per cell gives a minimum spacing and no cross-chunk collisions.
        for (int z = 0; z < 6; z++)
            for (int x = 0; x < 6; x++)
            {
                float wx = key.X * ChunkSize + (x + .3f + (float)random.NextDouble() * .4f) * ChunkSize / 6;
                float wz = key.Z * ChunkSize + (z + .3f + (float)random.NextDouble() * .4f) * ChunkSize / 6;
                var s = Sample(wx, wz);
                float chance = s.Biome switch
                {
                    WorldBiome.Forest => .8f,
                    WorldBiome.Swamp => .4f,
                    WorldBiome.Winter => .22f,
                    WorldBiome.Steppe => .16f,
                    _ => .045f
                };
                if (s.Water || Rivers.Sample(wx, wz).Coverage > -.14f || random.NextDouble() > chance)
                    continue;
                float h = Ground(wx, wz, WorldLod.NearCells);
                if (Math.Abs(Ground(wx + .05f, wz, WorldLod.NearCells) - h) > .07f || Math.Abs(Ground(wx, wz + .05f, WorldLod.NearCells) - h) > .07f)
                    continue;
                var kind = s.Biome switch
                {
                    WorldBiome.Forest => random.Next(2) == 0 ? ObjectKind.Broadleaf : ObjectKind.Pine,
                    WorldBiome.Winter => ObjectKind.Pine,
                    WorldBiome.Swamp => ObjectKind.Broadleaf,
                    WorldBiome.Steppe => ObjectKind.WideShrub,
                    _ => ObjectKind.Rock
                };
                float size = kind is ObjectKind.Pine or ObjectKind.Broadleaf ? .12f + (float)random.NextDouble() * .08f : .045f;
                objects.Add(new(kind, new(wx, Ground(wx, wz, cells) + .5f, wz), size, (float)random.NextDouble() * MathF.Tau, .85f + (float)random.NextDouble() * .3f));
            }
        return ObjectGeometry.Append(new(vertices.ToArray(), indices.ToArray()), objects);
    }
}

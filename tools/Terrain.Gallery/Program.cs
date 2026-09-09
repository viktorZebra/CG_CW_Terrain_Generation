using System.IO.Compression;
using System.Buffers.Binary;
using Terrain.Core.Models;
using Terrain.Core.Persistence;
using Terrain.Core.Rendering;
using Terrain.Core.Surface;
using Terrain.Core.Vegetation;

string root = Environment.CurrentDirectory;
string stage = args.FirstOrDefault() ?? "06-details";
if (stage is not ("06-details" or "07-atmosphere" or "08-1-water" or "08-2-channels" or "09-fast-shadows" or "10-before" or "10-erosion"))
    throw new ArgumentException("Unknown gallery stage.");
string folder = Path.Combine(root, "Карты высот", "Upgrade");
Directory.CreateDirectory(folder);
using var source = File.OpenRead(Path.Combine(root, "Карты высот", "Biomes", "03-alpine-snow.terrain.json"));
var original = await SceneFiles.ReadAsync(source);
var doc = original with { Version = SceneFiles.CurrentVersion, Details = new(.9f, Seed: 42), Shrubs = new(.75f, Seed: 42) };
if (stage != "06-details") doc = doc with { View = doc.View with { FogDensity = .12f } };
if (stage.StartsWith("08") || stage.StartsWith("09") || stage.StartsWith("10")) doc = doc with { Water = doc.Water! with { SeparateSurface = true, ChannelDepth = stage.StartsWith("08-1") ? 0 : .025f } };
if (stage.StartsWith("09")) doc = doc with { View = doc.View with { ShadowResolution = 256 } };
if (stage == "10-erosion") doc = doc with { Erosion = new(40, .45f) };
var mesh = Mesh.Build(doc.CreateMap(), doc.SurfaceSeed, doc.Water, doc.Vegetation, doc.Climate, doc.Shrubs, doc.Details, doc.Erosion);
string path = Path.Combine(folder, stage + ".terrain.json");
using (var file = File.Create(path)) await SceneFiles.WriteAsync(file, doc);
using (var file = File.OpenRead(path))
{
    var restored = await SceneFiles.ReadAsync(file);
    var rebuilt = Mesh.Build(restored.CreateMap(), restored.SurfaceSeed, restored.Water, restored.Vegetation, restored.Climate, restored.Shrubs, restored.Details, restored.Erosion);
    if (!mesh.Vertices.SequenceEqual(rebuilt.Vertices))
        throw new Exception("Roundtrip mismatch");
}
var frame = new CpuTerrainRenderer().Render(mesh, doc.View, 1000, 800);
Png(Path.Combine(folder, stage + "-preview.png"), frame);
Console.WriteLine($"{stage}: {mesh.Indices.Length / 3} triangles, {mesh.TreeCount} trees, {mesh.RockCount} rocks, {mesh.ShrubCount} shrubs; roundtrip PASS");
if (stage == "06-details")
{
    foreach (bool enabled in new[] { false, true })
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        var sample = Mesh.Build(doc.CreateMap(), doc.SurfaceSeed, doc.Water, doc.Vegetation, doc.Climate, doc.Shrubs, enabled ? doc.Details : null);
        double preparation = timer.Elapsed.TotalMilliseconds;
        var renderer = new CpuTerrainRenderer();
        renderer.Render(sample, doc.View, 1000, 800);
        var timings = new double[5];
        for (int i = 0; i < timings.Length; i++)
        {
            timer.Restart();
            renderer.Render(sample, doc.View, 1000, 800);
            timings[i] = timer.Elapsed.TotalMilliseconds;
        }
        Array.Sort(timings);
        Console.WriteLine($"Details={enabled}: prepare={preparation:F2} ms, CPU median={timings[2]:F2} ms, triangles={sample.Indices.Length / 3}");
    }
}

static void Png(string path, Frame frame)
{
    using var file = File.Create(path);
    file.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
    void Chunk(string name, byte[] data)
    {
        byte[] type = System.Text.Encoding.ASCII.GetBytes(name), len = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(len, data.Length);
        file.Write(len);
        file.Write(type);
        file.Write(data);
        uint crc = 0xffffffff;
        foreach (byte b in type.Concat(data))
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
                crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0);
        }
        BinaryPrimitives.WriteUInt32BigEndian(len, ~crc);
        file.Write(len);
    }
    byte[] header = new byte[13];
    BinaryPrimitives.WriteInt32BigEndian(header, frame.Width);
    BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), frame.Height);
    header[8] = 8;
    header[9] = 6;
    Chunk("IHDR", header);
    using var memory = new MemoryStream();
    using (var zip = new ZLibStream(memory, CompressionLevel.Optimal, true))
    {
        for (int y = 0; y < frame.Height; y++)
        {
            zip.WriteByte(0);
            for (int x = 0; x < frame.Width; x++)
            {
                int i = (y * frame.Width + x) * 4;
                zip.WriteByte(frame.Pixels[i + 2]);
                zip.WriteByte(frame.Pixels[i + 1]);
                zip.WriteByte(frame.Pixels[i]);
                zip.WriteByte(255);
            }
        }
    }
    Chunk("IDAT", memory.ToArray());
    Chunk("IEND", []);
}

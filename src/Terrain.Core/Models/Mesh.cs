using System.Numerics;
using Terrain.Core.Surface;
using Terrain.Core.Erosion;
using Terrain.Core.Climate;
using Terrain.Core.Hydrology;
using Terrain.Core.Vegetation;

namespace Terrain.Core.Models;

public sealed record Mesh(Vertex[] Vertices, int[] Indices, int TreeCount = 0, int RockCount = 0, ClimateMap? Climate = null, int ShrubCount = 0, HeightMap? WorkingMap = null, WaterMap? Water = null)
{
    public static Mesh Build(HeightMap map, int surfaceSeed = 0, HydrologyOptions? waterOptions = null, VegetationOptions? vegetationOptions = null, ClimateOptions? climateOptions = null, ShrubOptions? shrubOptions = null, DetailOptions? detailOptions = null, ErosionOptions? erosionOptions = null)
    {
        if (erosionOptions is not null)
            map = ThermalErosion.Apply(map, erosionOptions);
        var water = waterOptions is null ? null : HydrologyBuilder.Build(map, waterOptions);
        var working = map;
        if (water is not null && waterOptions!.SeparateSurface)
        {
            var prepared = WaterGeometry.Prepare(map, water, waterOptions);
            working = prepared.Ground;
            water = prepared.Water;
        }
        var climate = climateOptions is null ? null : ClimateBuilder.Build(map, climateOptions, water);
        var vertices = new Vertex[map.Values.Length];
        var indices = new int[(map.Width - 1) * (map.Height - 1) * 6];
        float scale = 2f / (Math.Max(map.Width, map.Height) - 1);
        for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
            {
                float h = water is null || water.SeparateGeometry ? working[x, y] : water.Levels[y * map.Width + x];
                var position = new Vector3((x - (map.Width - 1) / 2f) * scale, h, (y - (map.Height - 1) / 2f) * scale);
                vertices[y * map.Width + x] = new(position, Vector3.Zero, Vector3.Zero);
            }
        int n = 0;
        for (int y = 0; y < map.Height - 1; y++)
            for (int x = 0; x < map.Width - 1; x++)
            {
                int a = y * map.Width + x, b = a + 1, c = a + map.Width, d = c + 1;
                indices[n++] = a;
                indices[n++] = c;
                indices[n++] = b;
                indices[n++] = b;
                indices[n++] = c;
                indices[n++] = d;
            }
        // Area-weighted vertex normals: sum cross products of adjoining triangles.
        for (int i = 0; i < indices.Length; i += 3)
        {
            int a = indices[i], b = indices[i + 1], c = indices[i + 2];
            var normal = Vector3.Cross(vertices[b].Position - vertices[a].Position, vertices[c].Position - vertices[a].Position);
            vertices[a] = vertices[a] with
            {
                Normal = vertices[a].Normal + normal
            };
            vertices[b] = vertices[b] with
            {
                Normal = vertices[b].Normal + normal
            };
            vertices[c] = vertices[c] with
            {
                Normal = vertices[c].Normal + normal
            };
        }
        var surface = new TerrainSurface(surfaceSeed);
        for (int i = 0; i < vertices.Length; i++)
        {
            var vertex = vertices[i];
            var normal = Vector3.Normalize(vertex.Normal);
            vertices[i] = vertex with
            {
                Normal = normal,
                Color = surface.Sample(vertex.Position, normal, water?.SeparateGeometry == true ? 0 : water?.Coverage[i] ?? 0, water?.Moisture[i] ?? 0, water?.Bank[i] ?? 0, climate?.Samples[i], detailOptions?.SnowPatches == true).Color
            };
        }
        var terrain = new Mesh(vertices, indices, Climate: climate, WorkingMap: working, Water: water);
        var objects = vegetationOptions is null ? [] : ObjectScatter.Place(map, terrain, water, surfaceSeed, detailOptions is null ? vegetationOptions : vegetationOptions with
        {
            MaxRocks = 0
        });
        if (detailOptions is not null)
            objects = [.. objects, .. RockScatter.Place(map, terrain, water, objects, detailOptions)];
        if (water?.SeparateGeometry == true)
            terrain = WaterGeometry.Append(terrain, working, water);
        var decorated = objects.Length == 0 ? terrain : ObjectGeometry.Append(terrain, objects);
        return shrubOptions is null ? decorated : ObjectGeometry.Append(decorated,
            ShrubScatter.Place(map, terrain, water, objects, surfaceSeed, shrubOptions));
    }
}

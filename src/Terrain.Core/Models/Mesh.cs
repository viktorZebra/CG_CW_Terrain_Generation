using System.Numerics;

namespace Terrain.Core.Models;

public sealed record Mesh(Vertex[] Vertices, int[] Indices)
{
    public static Mesh Build(HeightMap map)
    {
        var vertices = new Vertex[map.Values.Length];
        var indices = new int[(map.Width - 1) * (map.Height - 1) * 6];
        float scale = 2f / (Math.Max(map.Width, map.Height) - 1);
        for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
            {
                float h = map[x, y];
                var position = new Vector3((x - (map.Width - 1) / 2f) * scale, h, (y - (map.Height - 1) / 2f) * scale);
                vertices[y * map.Width + x] = new(position, Vector3.Zero, Palette(h));
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
        for (int i = 0; i < vertices.Length; i++)
            vertices[i] = vertices[i] with
            {
                Normal = Vector3.Normalize(vertices[i].Normal)
            };
        return new(vertices, indices);
    }

    public static Vector3 Palette(float h) => h switch
    {
        <= .02f => new(.13f, .36f, .58f),
        < .55f => new(.24f, .52f, .27f),
        < .72f => new(.46f, .46f, .39f),
        < .90f => new(.60f, .61f, .60f),
        _ => new(.94f, .96f, .97f)
    };
}

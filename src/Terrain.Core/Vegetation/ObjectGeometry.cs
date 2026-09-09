using System.Numerics;
using Terrain.Core.Models;

namespace Terrain.Core.Vegetation;

/// <summary>Small procedural meshes; ordinary triangles participate in both renderers and shadow passes.</summary>
public static class ObjectGeometry
{
    private static readonly Mesh Pine = Create(ObjectKind.Pine);
    private static readonly Mesh Broadleaf = Create(ObjectKind.Broadleaf);
    private static readonly Mesh Rock = Create(ObjectKind.Rock);

    private static readonly Mesh RoundShrub = Create(ObjectKind.RoundShrub);
    private static readonly Mesh WideShrub = Create(ObjectKind.WideShrub);

    public static Mesh Append(Mesh terrain, IReadOnlyList<ObjectInstance> objects)
    {
        var vertices = new List<Vertex>(terrain.Vertices);
        var indices = new List<int>(terrain.Indices);
        int trees = terrain.TreeCount, rocks = terrain.RockCount, shrubs = terrain.ShrubCount;
        foreach (var item in objects)
        {
            var template = item.Kind switch
            {
                ObjectKind.Pine => Pine,
                ObjectKind.Broadleaf => Broadleaf,
                ObjectKind.RoundShrub => RoundShrub,
                ObjectKind.WideShrub => WideShrub,
                _ => Rock
            };
            int start = vertices.Count;
            float sin = MathF.Sin(item.Yaw), cos = MathF.Cos(item.Yaw);
            Vector3 Rotate(Vector3 v) => new(v.X * cos + v.Z * sin, v.Y, -v.X * sin + v.Z * cos);
            foreach (var vertex in template.Vertices)
                vertices.Add(new(item.Position + Rotate(vertex.Position) * item.Size, Rotate(vertex.Normal),
                    Vector3.Clamp(vertex.Color * item.Tint, Vector3.Zero, Vector3.One), AnchorHeight: item.Position.Y));
            foreach (int index in template.Indices)
                indices.Add(start + index);
            if (item.Kind == ObjectKind.Rock)
                rocks++;
            else if (item.Kind is ObjectKind.RoundShrub or ObjectKind.WideShrub)
                shrubs++;
            else
                trees++;
        }
        return new(vertices.ToArray(), indices.ToArray(), trees, rocks, terrain.Climate, shrubs, terrain.WorkingMap, terrain.Water);
    }

    private static Mesh Create(ObjectKind kind)
    {
        var vertices = new List<Vertex>();
        void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 color)
        {
            var cross = Vector3.Cross(b - a, c - a);
            if (cross.LengthSquared() < 1e-12f)
                return;
            var normal = Vector3.Normalize(cross);
            vertices.Add(new(a, normal, color));
            vertices.Add(new(b, normal, color));
            vertices.Add(new(c, normal, color));
        }
        void Ring(float bottom, float top, float r0, float r1, Vector3 color, int sides = 7)
        {
            for (int i = 0; i < sides; i++)
            {
                float a = i * MathF.Tau / sides, b = (i + 1) * MathF.Tau / sides;
                var p0 = new Vector3(MathF.Cos(a) * r0, bottom, MathF.Sin(a) * r0);
                var p1 = new Vector3(MathF.Cos(b) * r0, bottom, MathF.Sin(b) * r0);
                var q0 = new Vector3(MathF.Cos(a) * r1, top, MathF.Sin(a) * r1);
                var q1 = new Vector3(MathF.Cos(b) * r1, top, MathF.Sin(b) * r1);
                Triangle(p0, q0, p1, color);
                Triangle(p1, q0, q1, color);
                Triangle(new(0, bottom, 0), p0, p1, color * .85f);
                Triangle(new(0, top, 0), q1, q0, color);
            }
        }
        if (kind == ObjectKind.Rock)
        {
            // Buried lower ring avoids gaps where a boulder meets sloping ground.
            Ring(-.22f, .28f, .5f, .55f, new(.38f, .39f, .36f), 6);
            Ring(.28f, .65f, .55f, .22f, new(.49f, .48f, .43f), 6);
        }
        else if (kind is ObjectKind.RoundShrub or ObjectKind.WideShrub)
        {
            bool wide = kind == ObjectKind.WideShrub;
            float radius = wide ? .55f : .37f;
            float top = wide ? .42f : .70f;
            Ring(-.10f, top * .4f, .16f, radius, new(.24f, .36f, .13f), wide ? 6 : 7);
            Ring(top * .4f, top, radius, .12f, new(.36f, .48f, .20f), wide ? 6 : 7);
        }
        else
        {
            Ring(-.07f, .65f, .07f, .045f, new(.28f, .19f, .10f), 6);
            if (kind == ObjectKind.Pine)
            {
                Ring(.28f, .9f, .32f, 0, new(.13f, .30f, .17f));
                Ring(.60f, 1.2f, .24f, 0, new(.19f, .39f, .21f));
            }
            else
            {
                Ring(.42f, .78f, .13f, .35f, new(.26f, .43f, .15f));
                Ring(.78f, 1.18f, .35f, .09f, new(.34f, .51f, .20f));
            }
        }
        return new(vertices.ToArray(), Enumerable.Range(0, vertices.Count).ToArray());
    }
}

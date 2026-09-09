using System.Numerics;
using Terrain.Core.World;
using Xunit;

namespace Terrain.Core.Tests;

public sealed class WorldCloudTests
{
    [Fact]
    public void CloudsHaveClearGapsSoftEdgesAndBoundedOpacity()
    {
        int clear = 0, soft = 0, dense = 0;
        for (int z = -25; z <= 25; z++)
            for (int x = -25; x <= 25; x++)
            {
                var ray = Vector3.Normalize(new Vector3(x, 12, z));
                float alpha = WorldClouds.Opacity(ray, Vector3.Zero, 0, 42);
                Assert.InRange(alpha, 0, .78f);
                if (alpha == 0)
                    clear++;
                else if (alpha < .5f)
                    soft++;
                else
                    dense++;
            }
        Assert.True(clear > 100 && soft > 100 && dense > 30);
        Assert.Equal(0, WorldClouds.Opacity(-Vector3.UnitY, Vector3.Zero, 0, 42));
        Assert.Equal(0, WorldClouds.Opacity(Vector3.UnitX, Vector3.Zero, 0, 42));
    }

    [Fact]
    public void WindTransportsTheSameCloudShapesWithoutRegeneration()
    {
        var p = new Vector2(3.25f, -4.5f);
        Assert.InRange(Math.Abs(WorldClouds.Density(p, 0, 42) - WorldClouds.Density(p + WorldClouds.Wind * 60, 60, 42)), 0, 1e-5f);
        float changed = 0;
        for (int x = -10; x <= 10; x++)
            changed += Math.Abs(WorldClouds.Density(new(x, 4), 0, 42) - WorldClouds.Density(new(x, 4), 30, 42));
        Assert.True(changed > 1);
        // The world clock does not restart cloud movement at the day/night boundary.
        Assert.InRange(Math.Abs(WorldClouds.Density(p, 479.999f, 42) - WorldClouds.Density(p, 480.001f, 42)), 0, .001f);
    }

    [Fact]
    public void CloudLayerUsesWorldCoordinatesAndSeed()
    {
        var target = new Vector3(4, WorldClouds.Altitude, 6);
        var a = Vector3.Normalize(target);
        var camera = new Vector3(2, 0, -3);
        var b = Vector3.Normalize(target - camera);
        Assert.InRange(Math.Abs(WorldClouds.Opacity(a, Vector3.Zero, 10, 123) - WorldClouds.Opacity(b, camera, 10, 123)), 0, 1e-5f);
        int different = 0;
        for (int i = 0; i < 30; i++)
        {
            var p = new Vector2(i, -i);
            Assert.Equal(WorldClouds.Density(p, 3, 42), WorldClouds.Density(p, 3, 42));
            if (Math.Abs(WorldClouds.Density(p, 3, 42) - WorldClouds.Density(p, 3, 73)) > .1f)
                different++;
        }
        Assert.True(different > 5);
        Assert.Equal(DayNight.Sky(a, 30, .001f), WorldClouds.Sky(a, Vector3.Zero, 30, .001f, null, 10));
    }
}

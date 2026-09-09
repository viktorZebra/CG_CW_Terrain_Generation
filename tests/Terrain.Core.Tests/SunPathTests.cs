using System.Numerics;
using Terrain.Core.Rendering;
using Xunit;

namespace Terrain.Core.Tests;

public sealed class SunPathTests
{
    [Fact]
    public void ArcStartsOnLeftPassesThroughZenithAndEndsOnRight()
    {
        Assert.True(Vector3.Distance(SunPath.Direction(0), -Vector3.UnitX) < 1e-6);
        Assert.True(Vector3.Distance(SunPath.Direction(.5f), Vector3.UnitY) < 1e-6);
        Assert.True(Vector3.Distance(SunPath.Direction(1), Vector3.UnitX) < 1e-6);
        for (int i = 0; i <= 100; i++)
        {
            var light = SunPath.Direction(i / 100f);
            Assert.True(light.Y >= 0);
            Assert.True(Math.Abs(light.Length() - 1) < 1e-6);
        }
    }

    [Fact]
    public void MorningAndEveningLightOppositeSlopes()
    {
        var normal = Vector3.Normalize(new Vector3(-1, 1, 0));
        float morning = Scene.Shade(normal, Vector3.One, SunPath.Direction(.2f)).X;
        float evening = Scene.Shade(normal, Vector3.One, SunPath.Direction(.8f)).X;
        Assert.True(morning > evening + .5f);
    }

    [Fact]
    public void SunDiskIsCircularAcrossViewportAspectRatios()
    {
        var light = SunPath.Direction(.5f);
        foreach (float aspect in new[] { .5f, 1f, 2f })
        {
            var horizontal = SunPath.SkyColor(.5f + .01f / aspect, .12f, aspect, light, .001f);
            var vertical = SunPath.SkyColor(.5f, .13f, aspect, light, .001f);
            Assert.True(Vector3.Distance(horizontal, vertical) < 1e-6);
            Assert.True(horizontal.X > .9f);
        }
    }
}

using System.Numerics;
using Terrain.Core.Rendering;
using Xunit;
namespace Terrain.Core.Tests;

public sealed class AtmosphereTests
{
    [Fact]
    public void HazeUsesRadialDistanceAndZeroRestoresImage()
    {
        Assert.Equal(0, Atmosphere.Amount(100, 0));
        Assert.Equal(0, Atmosphere.Amount(.2f, .5f));
        Assert.True(Atmosphere.Amount(4, .2f) > Atmosphere.Amount(1, .2f));
        Assert.InRange(Atmosphere.Amount(100, .5f), 0, 1);
        var scene = new Scene();
        Assert.Equal(4, Atmosphere.Distance(.5f, .5f, 1, .25f, scene));
        Assert.True(Atmosphere.Distance(1, .5f, 1, .25f, scene) > 4);
        Assert.Equal(Atmosphere.Distance(1, .5f, 1, .25f, scene), Atmosphere.Distance(.5f, 1, 1, .25f, scene));
        Assert.NotEqual(Atmosphere.Color(Vector3.UnitY), Atmosphere.Color(Vector3.UnitX));
    }
}

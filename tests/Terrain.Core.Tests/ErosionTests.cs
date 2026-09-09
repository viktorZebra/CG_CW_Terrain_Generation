using Terrain.Core.Erosion;
using Terrain.Core.Models;
using Xunit;
namespace Terrain.Core.Tests;

public sealed class ErosionTests
{
    [Fact]
    public void PeakSpreadsConservativelyWithoutMutatingSource()
    {
        var source = new HeightMap(17, 9);
        source[8, 4] = 1;
        var eroded = ThermalErosion.Apply(source, new(40, .5f));
        Assert.Equal(1, source[8, 4]);
        Assert.True(eroded[8, 4] < .8f);
        Assert.InRange(Math.Abs(eroded.Values.Sum() - 1), 0, .00001f);
        Assert.All(eroded.Values, v => Assert.InRange(v, 0, 1));
        Assert.Equal(eroded.Values, ThermalErosion.Apply(source, new(40, .5f)).Values);
        Assert.Equal(source.Values, ThermalErosion.Apply(source, new(0)).Values);
        Assert.Equal(source.Values, ThermalErosion.Apply(source, new(30, 0)).Values);
    }
    [Fact]
    public void ConstantPlaneAndBoundaryMassAreStable()
    {
        var source = new HeightMap(13, 7);
        Array.Fill(source.Values, .3f);
        Assert.Equal(source.Values, ThermalErosion.Apply(source, new()).Values);
        source[0, 0] = 1;
        double mass = source.Values.Sum(v => (double)v);
        var eroded = ThermalErosion.Apply(source, new(60, .5f));
        Assert.InRange(Math.Abs(eroded.Values.Sum(v => (double)v) - mass), 0, .0001);
        Assert.All(eroded.Values, v => Assert.InRange(v, .29999f, 1));
    }
}

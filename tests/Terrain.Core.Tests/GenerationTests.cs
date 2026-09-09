using Terrain.Core.Models;
using Terrain.Core.Generation;
using Xunit;
using static Terrain.Core.Tests.TestSupport;

namespace Terrain.Core.Tests;

public sealed class GenerationTests
{
    [Theory]
    [InlineData(GeneratorKind.Hills)]
    [InlineData(GeneratorKind.Perlin)]
    [InlineData(GeneratorKind.Simplex)]
    [InlineData(GeneratorKind.DiamondSquare)]
    public void Generate_ProducesFiniteReproducibleSeedDependentHeights(GeneratorKind kind)
    {
        var options = new GenerationOptions(kind, 65);
        var a = Generators.Generate(options);
        var b = Generators.Generate(options);
        var c = Generators.Generate(options with
        {
            Seed = 99
        });
        Check(a.Values.All(v => float.IsFinite(v) && v >= 0 && v <= 1));
        Check(a.Values.SequenceEqual(b.Values));
        Check(!a.Values.SequenceEqual(c.Values));
        Near(a.Values.Min(), 0);
        Near(a.Values.Max(), 1);
        var processed = Generators.Generate(options with
        {
            Smooth = true,
            Valley = true
        });
        Check(processed.Values.All(v => float.IsFinite(v) && v >= 0 && v <= 1));
    }

    [Fact]
    public void ZeroHillsAndConstantMapsStayFinite()
    {
        var empty = Generators.Generate(new(GeneratorKind.Hills, Hills: 0, Smooth: true, Valley: true));
        Check(empty.Values.All(v => v == 0));
        var flat = new HeightMap(5, 3);
        Array.Fill(flat.Values, 10);
        flat.Normalize();
        Check(flat.Values.All(v => v == 0));
    }

    [Fact]
    public void SmoothingImpulseIsSymmetricAndDoesNotMutateSource()
    {
        var map = new HeightMap(7, 5);
        map[3, 2] = 1;
        var smooth = map.Smooth();
        Check(map[3, 2] == 1 && map.Values.Sum() == 1);
        Near(smooth[3, 2], 1f / 9);
        Near(smooth[2, 2], smooth[4, 2]);
        Near(smooth[3, 1], smooth[3, 3]);
        Near(smooth[0, 0], 0);
    }

    [Fact]
    public void SmoothingConstantRectangularMapIncludesCornersExactlyOnce()
    {
        var map = new HeightMap(9, 3);
        Array.Fill(map.Values, .4f);
        Check(map.Smooth().Values.All(v => Math.Abs(v - .4f) < 1e-6));
    }

    [Fact]
    public void DiamondSquareRejectsNonPowerOfTwoPlusOneDimensions()
    {
        bool threw = false;
        try
        {
            Generators.Generate(new(GeneratorKind.DiamondSquare, 100));
        }
        catch (ArgumentException) { threw = true; }
        Check(threw);
    }

    [Fact]
    public void NoiseIsContinuousAcrossLatticeBoundariesAndPerlinIsZeroAtIntegerPoints()
    {
        var noise = new GradientNoise(42);
        for (int x = -4; x <= 4; x++)
        {
            Near(noise.Perlin(x, 2), 0);
            Near(noise.Perlin(x - .0001f, .37f), noise.Perlin(x + .0001f, .37f), .001f);
            Near(noise.Simplex(x - .0001f, .37f), noise.Simplex(x + .0001f, .37f), .005f);
        }
    }
}

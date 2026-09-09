using System.Numerics;
using Terrain.Core.Models;
using Terrain.Core.Rendering;
using Terrain.Core.Rendering.Shadows;
using Xunit;
using static Terrain.Core.Tests.TestSupport;

namespace Terrain.Core.Tests;

public sealed class ShadowMapTests
{
    [Fact]
    public void OccluderCastsAShadowClearGroundAndTheTopRemainLit()
    {
        var scene = new Scene(0, 0, 0, HeightScale: 1, Light: Vector3.UnitY, ShadowResolution: 256);
        var map = ShadowMap.Build(OccluderScene(), scene);
        float Visibility(Vector3 p) => map.Visibility(map.Projection.Project(scene.WorldPosition(p)), 1, false);
        Near(Visibility(new(0, 0, 0)), 0);
        Near(Visibility(new(.8f, 0, 0)), 1);
        Near(Visibility(new(0, .5f, 0)), 1);
    }

    [Fact]
    public void RotatingTheModelChangesItsCastShadowWhileTheLightStaysFixed()
    {
        var mesh = OccluderScene();
        var original = new Scene(0, 0, 0, HeightScale: 1, Light: Vector3.UnitY, ShadowResolution: 256);
        var rotated = original with
        {
            RotationZ = 60
        };
        var a = ShadowMap.Build(mesh, original);
        var b = ShadowMap.Build(mesh, rotated);
        Near(a.Visibility(a.Projection.Project(original.WorldPosition(Vector3.Zero)), 1, false), 0);
        Near(b.Visibility(b.Projection.Project(rotated.WorldPosition(Vector3.Zero)), .5f, false), 1);
        Check(original.LightDirection == rotated.LightDirection);
        var pa = original.Project(mesh, 1, a.Projection);
        var pb = rotated.Project(mesh, 1, b.Projection);
        Check(pa[0].ShadowPosition != pb[0].ShadowPosition);
    }

    [Fact]
    public void ShadowCacheInvalidatesForModelLightChangesButReusesZoomPanAndFiltering()
    {
        var renderer = new CpuTerrainRenderer();
        var mesh = OccluderScene();
        var scene = new Scene(ShadowResolution: 64);
        var first = renderer.GetShadowMap(mesh, scene);
        Check(ReferenceEquals(first, renderer.GetShadowMap(mesh, scene with
        {
            Zoom = 2,
            PanX = .3f,
            PanY = .2f,
            SoftShadows = false
        })));
        Check(renderer.ShadowBuildCount == 1);
        foreach (var changed in new[] { scene with { RotationX = 90 }, scene with { RotationY = 90 }, scene with { RotationZ = 90 },
            scene with { Light = Vector3.UnitY }, scene with { HeightScale = 1.5f }, scene with { ShadowResolution = 128 } })
        {
            int before = renderer.ShadowBuildCount;
            renderer.GetShadowMap(mesh, changed);
            Check(renderer.ShadowBuildCount == before + 1);
        }
        int previous = renderer.ShadowBuildCount;
        renderer.GetShadowMap(OccluderScene(), scene);
        Check(renderer.ShadowBuildCount == previous + 1);
        var disabled = new CpuTerrainRenderer();
        disabled.Render(mesh, scene with
        {
            Shadows = false
        }, 64, 64);
        Check(disabled.ShadowBuildCount == 0);
    }

    [Fact]
    public void PcfAveragesComparisonsAndTreatsSamplesOutsideTheMapAsLit()
    {
        var projection = ShadowProjection.Create(OccluderScene(), new Scene());
        float[] depths = Enumerable.Repeat(1f, 9).ToArray();
        depths[4] = .2f;
        var map = new ShadowMap(projection, 3, depths);
        Near(map.Visibility(new(.5f, .5f, .5f), 1, false), 0);
        Near(map.Visibility(new(.5f, .5f, .5f), 1, true), 8f / 9);
        Near(map.Visibility(new(-.1f, .5f, .5f), 1, true), 1);
    }

    [Fact]
    public void DepthBiasPreventsAcneOnAFlatSurfaceAtALowLightAngle()
    {
        var mesh = Mesh.Build(new HeightMap(9, 7));
        foreach (var light in new[] { new Vector3(-1, .12f, .7f), Vector3.UnitY, -Vector3.UnitY })
        {
            var scene = new Scene(0, 0, 0, Light: light, ShadowResolution: 512);
            var map = ShadowMap.Build(mesh, scene);
            // Analytic plane y = constant, expressed in the light's normalized coordinates.
            var plane = new Vector3(map.Projection.Right.Y * map.Projection.Extent.X,
                map.Projection.Up.Y * map.Projection.Extent.Y, -scene.LightDirection.Y * map.Projection.Extent.Z);
            var gradient = new Vector2(-plane.X, -plane.Y) / plane.Z;
            Check(map.Depth.Span.ToArray().All(float.IsFinite));
            for (int x = -7; x <= 7; x++)
                for (int z = -4; z <= 4; z++)
                {
                    var p = map.Projection.Project(scene.WorldPosition(new(x / 10f, 0, z / 10f)));
                    Near(map.Visibility(p, Math.Abs(scene.LightDirection.Y), true, gradient), 1);
                }
        }
    }

    [Fact]
    public void ReceiverPlaneCorrectionKeepsSteepPcfSamplesLit()
    {
        var depths = new float[16 * 16];
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
                depths[y * 16 + x] = .2f + .5f * (x + .5f) / 16 + .2f * (y + .5f) / 16;
        var map = new ShadowMap(ShadowProjection.Create(OccluderScene(), new Scene()), 16, depths);
        var p = new Vector3(.513f, .521f, .2f + .5f * .513f + .2f * .521f);
        Check(map.Visibility(p, .1f, true) < 1, "Fixture must exhibit self-shadow without correction");
        Near(map.Visibility(p, .1f, true, new(.5f, .2f)), 1);
    }

    [Fact]
    public void ShadowsRemoveDirectLightButPreserveAmbientAndNeverBrightenAFrame()
    {
        Check(Vector3.Distance(Scene.Shade(Vector3.UnitY, Vector3.One, Vector3.UnitY, 0), new Vector3(.3f)) < 1e-6);
        var renderer = new CpuTerrainRenderer();
        var mesh = OccluderScene();
        var scene = new Scene(45, 0, 0, HeightScale: 1, Light: new(0, 1, 1), ShadowResolution: 256);
        var lit = renderer.Render(mesh, scene with
        {
            Shadows = false
        }, 256, 256);
        var shadow = renderer.Render(mesh, scene, 256, 256);
        int darker = 0;
        for (int i = 0; i < lit.Pixels.Length; i++)
        {
            Check(shadow.Pixels[i] <= lit.Pixels[i]);
            if (shadow.Pixels[i] + 5 < lit.Pixels[i])
                darker++;
        }
        Check(darker > 200, "No visible cast shadow");
    }
}

using System.Diagnostics;
using System.Numerics;
using Avalonia.Threading;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Terrain.App.Services;
using Terrain.App.Views;
using Terrain.Core.Models;
using Terrain.Core.Rendering;
using Terrain.Core.World;

namespace Terrain.App.SmokeTests;

internal static class WorldSmoke
{
    public static async Task<int> RunAsync(MainWindow window, string root)
    {
        var folder = Path.Combine(root, "Карты высот", "World");
        Directory.CreateDirectory(folder);
        var log = new List<string>();
        var gl = window.GlRenderer;
        TaskCompletionSource<Frame>? pending = null;
        void Captured(Frame frame) => Dispatcher.UIThread.Post(() => pending?.TrySetResult(frame));
        gl.Captured += Captured;
        try
        {
            var originalMesh = window.CurrentMesh;
            var originalScene = window.CurrentScene;
            var canceled = window.EnterWorld();
            window.ExitWorld();
            await canceled;
            if (!ReferenceEquals(originalMesh, window.CurrentMesh) || originalScene != window.CurrentScene)
                throw new Exception("Cancel did not restore editor");
            var timer = Stopwatch.StartNew();
            await window.EnterWorld();
            if (!window.CurrentScene.Walking || window.CurrentScene.WorldTime is null || ReferenceEquals(originalMesh, window.CurrentMesh))
                throw new Exception("World entry failed");
            log.Add($"World entry {timer.Elapsed.TotalSeconds:F2}s; {window.CurrentMesh!.Indices.Length / 3} triangles");
            var start = window.CurrentScene.ViewCamera;
            var moved = window.MoveInWorld(start, .1f);
            if (Vector3.Distance(start.Position, moved.Position) < .01f)
                throw new Exception("World walking is blocked");
            if (window.MoveInWorld(start, 1000).Position != start.Position)
                throw new Exception("Camera entered unpublished ground");
            void MapKey(bool down, Key key = Key.M) => window.NavigationSurface.RaiseEvent(new KeyEventArgs
            {
                RoutedEvent = down ? Avalonia.Input.InputElement.KeyDownEvent : Avalonia.Input.InputElement.KeyUpEvent,
                Key = key
            });
            MapKey(true);
            if (!window.IsWorldMapOpen || window.IsMouseLookActive)
                throw new Exception("M did not open map and release look");
            MapKey(true);
            if (!window.IsWorldMapOpen)
                throw new Exception("Held M toggles repeatedly");
            MapKey(false);
            await Task.Delay(200);
            using (var screenshot = new RenderTargetBitmap(new PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height)))
            {
                screenshot.Render(window);
                screenshot.Save(Path.Combine(folder, "09-world-map-preview.png"));
            }
            MapKey(true);
            MapKey(false);
            if (window.IsWorldMapOpen)
                throw new Exception("M did not close map");
            MapKey(true);
            MapKey(false);
            MapKey(true, Key.Escape);
            if (window.IsWorldMapOpen)
                throw new Exception("Escape did not close map");
            log.Add("M open/close, key-repeat guard, Escape, cursor release: PASS");
            window.ExitWorld();
            if (!ReferenceEquals(originalMesh, window.CurrentMesh) || originalScene != window.CurrentScene)
                throw new Exception("Exit did not restore editor");
            log.Add("Entry, cancellation, exit and original editor restoration: PASS");
            var terrain = new WorldTerrain(20260909);
            var stream = new WorldStream(terrain);
            var cpu = new CpuTerrainRenderer();
            async Task Render(string name, CameraPose camera, float time, float? cloudTime = null)
            {
                var keys = WorldLod.Select(camera);
                timer.Restart();
                var mesh = await Task.Run(() => stream.BuildDetailed(keys, default));
                double prepare = timer.Elapsed.TotalMilliseconds;
                stream.PublishDetailed(keys);
                camera = stream.Anchor(camera);
                if (stream.CachedCount > WorldStream.CacheLimit)
                    throw new Exception("Unbounded cache");
                var scene = new Scene(0, 0, HeightScale: 1, Light: DayNight.Light(time), ShadowResolution: 1024,
                    ShowSky: true, Camera: camera, Walking: true, WorldTime: time, WorldViewDistance: stream.ViewDistance(camera), CloudSeed: terrain.Seed, CloudTime: cloudTime ?? time);
                window.CurrentMesh = mesh;
                window.CurrentScene = scene;
                window.UseOpenGl = true;
                window.Redraw();
                await Task.Delay(150);
                pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
                gl.CaptureRequested = true;
                gl.RequestNextFrameRendering();
                var gpu = await pending.Task.WaitAsync(TimeSpan.FromSeconds(30));
                pending = null;
                timer.Restart();
                var frame = await Task.Run(() => cpu.Render(mesh, scene, gpu.Width, gpu.Height));
                double cpuMs = timer.Elapsed.TotalMilliseconds;
                double error = gpu.Pixels.Zip(frame.Pixels).Average(pair => Math.Abs(pair.First - pair.Second));
                if (error > 2)
                    throw new Exception($"{name}: CPU/GPU mismatch {error}");
                using (var file = File.Create(Path.Combine(folder, name + ".png")))
                    Images.Save(gpu, file, false);
                log.Add($"{name}: chunks={keys.Length}, near={keys.Count(k => k.Cells == 128)}, medium={keys.Count(k => k.Cells == 64)}, cache={stream.CachedCount}, triangles={mesh.Indices.Length / 3}, prepare={prepare:F0}ms, GL-warm={gl.LastFrameMilliseconds:F1}ms, CPU={cpuMs:F0}ms, mean BGRA={error:F4}/255, heap={GC.GetTotalMemory(true)} PASS");
            }
            for (int i = 0; i < 5; i++)
            {
                var p = terrain.BiomeExamples[i];
                var camera = terrain.Anchor(new(new(p.X, 0, p.Y), Pitch: -6));
                await Render($"0{i + 1}-{(WorldBiome)i}", camera, 150);
            }
            var forest = terrain.BiomeExamples[0];
            var view = terrain.Anchor(new(new(forest.X, 0, forest.Y), Pitch: 35));
            await Render("06-sunset", view with
            {
                Yaw = 90,
                Pitch = 3
            }, 295);
            await Render("07-moon-stars", view with
            {
                Pitch = 80
            }, 390);
            await Render("08-turn-behind", view with
            {
                Yaw = 180,
                Pitch = -4
            }, 150);
            var cloudView = view with
            {
                Pitch = 45
            };
            await Render("10-clouds-day", cloudView, 150, 0);
            await Render("11-clouds-moved", cloudView, 150, 30);
            await Render("12-clouds-night", cloudView with
            {
                Pitch = 65
            }, 390, 30);
            await Render("13-clouds-evening", cloudView with
            {
                Yaw = 90
            }, 290, 30);
            var spring = terrain.Rivers.Sources[1];
            float riverZ = spring.Z + 3;
            float riverX = Enumerable.Range(-80, 161).Select(i => spring.X + i * .05f)
                .OrderByDescending(x => terrain.Sample(x, riverZ).WaterCoverage).First();
            await Render("14-winter-river", terrain.Anchor(new(new(riverX + .45f, 0, riverZ + 1), Yaw: -12, Pitch: 8)), 150);
            await Render("15-winter-upland", terrain.Anchor(new(new(spring.X + 3, 0, spring.Z + 3), Yaw: -30, Pitch: 8)), 150);
            int uploads = gl.GeometryUploadCount, shadowBuilds = gl.ShadowBuildCount;
            window.CurrentScene = window.CurrentScene with
            {
                CloudTime = window.CurrentScene.CloudTime + 1
            };
            pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
            gl.CaptureRequested = true;
            window.Redraw();
            await pending.Task.WaitAsync(TimeSpan.FromSeconds(30));
            pending = null;
            if (uploads != gl.GeometryUploadCount || shadowBuilds != gl.ShadowBuildCount)
                throw new Exception("Cloud movement rebuilt geometry or terrain shadows");
            log.Add("Cloud animation without geometry upload or shadow rebuild: PASS");
            log.Add("PASS");
            return 0;
        }
        catch (Exception error) { log.Add(error.ToString()); return 1; }
        finally
        {
            gl.Captured -= Captured;
            File.WriteAllLines(Path.Combine(folder, "validation.txt"), log);
            foreach (string line in log)
                Console.WriteLine(line);
        }
    }
}

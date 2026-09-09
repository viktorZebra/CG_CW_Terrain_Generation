using System.Numerics;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Terrain.App.Services;
using Terrain.App.Views;
using Terrain.Core.Generation;
using Terrain.Core.Models;
using Terrain.Core.Rendering;

namespace Terrain.App.SmokeTests;

internal sealed class SmokeTestRunner
{
    // Runs against a real macOS window and GL context, not a mocked renderer.
    public async Task<int> RunAsync(MainWindow window, string repositoryRoot)
    {
        var gl = window.GlRenderer;
        var cpuRenderer = new CpuTerrainRenderer();
        TaskCompletionSource<Frame>? capture = null;
        void OnCaptured(Frame frame) => Dispatcher.UIThread.Post(() => capture?.TrySetResult(frame));
        gl.Captured += OnCaptured;
        string folder = Path.Combine(Path.GetTempPath(), "terrain-smoke");
        Directory.CreateDirectory(folder);
        var log = new List<string>();
        int exitCode = 0;
        try
        {
            foreach (var kind in Enum.GetValues<GeneratorKind>())
            {
                var generated = Generators.Generate(new(kind, 65, Smooth: true));
                window.SetMap(generated, Mesh.Build(generated), 0);
                window.CurrentScene = window.CurrentScene with
                {
                    RotationZ = 12
                };
                window.UseOpenGl = true;
                await Task.Delay(250);
                capture = new(TaskCreationOptions.RunContinuationsAsynchronously);
                gl.CaptureRequested = true;
                gl.RequestNextFrameRendering();
                Frame gpu = await capture.Task.WaitAsync(TimeSpan.FromSeconds(15));
                capture = null;
                var cpu = cpuRenderer.Render(window.CurrentMesh!, window.CurrentScene, gpu.Width, gpu.Height);
                long error = 0;
                int visible = 0;
                for (int i = 0; i < cpu.Pixels.Length; i += 4)
                {
                    for (int c = 0; c < 3; c++)
                        error += Math.Abs(cpu.Pixels[i + c] - gpu.Pixels[i + c]);
                    if (gpu.Pixels[i] != 29 || gpu.Pixels[i + 1] != 23 || gpu.Pixels[i + 2] != 18)
                        visible++;
                }
                double meanError = (double)error / (cpu.Width * cpu.Height * 3);
                log.Add($"{kind}: {gpu.Width}x{gpu.Height}, occupied={visible}, mean RGB error={meanError:F4}/255");
                using (var file = File.Create(Path.Combine(folder, kind + "-gpu.png")))
                    Images.Save(gpu, file, false);
                using (var file = File.Create(Path.Combine(folder, kind + "-cpu.png")))
                    Images.Save(cpu, file, false);
                if (visible < 1000 || meanError > 2)
                    throw new Exception("CPU/OpenGL mismatch: " + log[^1]);
                window.UseOpenGl = false;
                await Task.Delay(100);
            }
            // Analytic occluder: verify that GPU shadows really darken the receiver,
            // and that rotation invalidates the light pass while view zoom does not.
            var savedMesh = window.CurrentMesh;
            var savedScene = window.CurrentScene;
            Vertex P(float x, float y, float z) => new(new(x, y, z), Vector3.UnitY, Vector3.One);
            window.CurrentMesh = new Mesh([P(-1,0,-1), P(1,0,-1), P(-1,0,1), P(1,0,1),
                P(-.3f,.5f,-.3f), P(.3f,.5f,-.3f), P(-.3f,.5f,.3f), P(.3f,.5f,.3f)],
                [0, 2, 1, 1, 2, 3, 4, 6, 5, 5, 6, 7]);
            async Task<Frame> ShadowCase(string name, Scene settings)
            {
                window.CurrentScene = settings;
                window.UseOpenGl = true;
                window.Redraw();
                capture = new(TaskCreationOptions.RunContinuationsAsynchronously);
                gl.CaptureRequested = true;
                gl.RequestNextFrameRendering();
                var gpu = await capture.Task.WaitAsync(TimeSpan.FromSeconds(15));
                capture = null;
                var cpu = cpuRenderer.Render(window.CurrentMesh!, window.CurrentScene, gpu.Width, gpu.Height);
                double error = gpu.Pixels.Zip(cpu.Pixels).Average(p => Math.Abs(p.First - p.Second));
                if (error > .5)
                    throw new Exception($"{name}: CPU/GPU shadow mismatch {error}");
                using var file = File.Create(Path.Combine(folder, name + ".png"));
                Images.Save(gpu, file, false);
                log.Add($"{name}: mean BGRA error {error:F4}/255 PASS");
                return gpu;
            }
            foreach (float progress in new[] { 0f, .25f, .5f, .75f, 1f })
                await ShadowCase($"sun-{progress:F2}", savedScene with
                {
                    Light = SunPath.Direction(progress), ShowSky = true
                });
            var shadowScene = new Scene(45, 0, 0, HeightScale: 1, Light: new(0, 1, 1), ShadowResolution: 512);
            var withoutShadow = await ShadowCase("shadow-off", shadowScene with
            {
                Shadows = false
            });
            int builds = gl.ShadowBuildCount;
            var hardShadow = await ShadowCase("shadow-hard", shadowScene with
            {
                SoftShadows = false
            });
            if (gl.ShadowBuildCount != builds + 1)
                throw new Exception("Shadow enable did not build the map");
            int darker = hardShadow.Pixels.Zip(withoutShadow.Pixels).Count(p => p.First + 5 < p.Second);
            if (darker < 200)
                throw new Exception("GPU did not cast a visible shadow");
            builds = gl.ShadowBuildCount;
            await ShadowCase("shadow-soft", shadowScene);
            await ShadowCase("shadow-zoom", shadowScene with
            {
                Zoom = 1.2f,
                PanX = .1f
            });
            if (gl.ShadowBuildCount != builds)
                throw new Exception("Filtering/zoom unexpectedly rebuilt shadow map");
            await ShadowCase("shadow-rotated", shadowScene with
            {
                RotationZ = 60
            });
            if (gl.ShadowBuildCount != ++builds)
                throw new Exception("Rotation did not rebuild shadow map");
            await ShadowCase("shadow-light-changed", shadowScene with
            {
                RotationZ = 60,
                Light = new(.4f, 1, .3f)
            });
            if (gl.ShadowBuildCount != ++builds)
                throw new Exception("Light change did not rebuild shadow map");
            await ShadowCase("shadow-height-changed", shadowScene with
            {
                RotationZ = 60,
                Light = new(.4f, 1, .3f),
                HeightScale = 1.3f
            });
            if (gl.ShadowBuildCount != ++builds)
                throw new Exception("Height change did not rebuild shadow map");
            log.Add("GPU shadow visibility and cache invalidation: PASS");
            window.CurrentMesh = savedMesh;
            window.CurrentScene = savedScene;
            // Non-square image roundtrip through both export encoders and the import path.
            var rectangle = new HeightMap(17, 9);
            for (int i = 0; i < rectangle.Values.Length; i++)
                rectangle.Values[i] = (float)i / (rectangle.Values.Length - 1);
            foreach (bool bmp in new[] { false, true })
            {
                using var stream = new MemoryStream();
                Images.Save(Images.Preview(rectangle), stream, bmp);
                stream.Position = 0;
                var loaded = Images.Load(stream);
                if (loaded.Width != 17 || loaded.Height != 9 || loaded.Values.Zip(rectangle.Values).Any(p => Math.Abs(p.First - p.Second) > 1f / 255))
                    throw new Exception("Image roundtrip failed");
                log.Add((bmp ? "BMP" : "PNG") + ": rectangular image roundtrip PASS");
            }
            string oldMaps = Path.Combine(repositoryRoot, "Карты высот");
            if (Directory.Exists(oldMaps))
            {
                foreach (string file in Directory.EnumerateFiles(oldMaps, "*", SearchOption.AllDirectories).Where(p => p.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".png", StringComparison.OrdinalIgnoreCase)))
                {
                    using var stream = File.OpenRead(file);
                    var loaded = Images.Load(stream);
                    var importedMesh = Mesh.Build(loaded);
                    var rendered = SoftwareRenderer.Render(new Scene().Project(importedMesh, 1), importedMesh.Indices, new Scene().LightDirection, 256, 256);
                    if (!rendered.Pixels.Where((_, i) => i % 4 == 0).Any(v => v != 29))
                        throw new Exception("Empty imported scene: " + file);
                    log.Add($"Import {Path.GetFileName(file)} {loaded.Width}x{loaded.Height}: PASS");
                }
            }
            window.Width = 980;
            window.Height = 700;
            window.CurrentScene = window.CurrentScene with
            {
                Zoom = 4,
                PanX = .2f
            };
            window.UseOpenGl = true;
            window.Redraw();
            await Task.Delay(250);
            capture = new(TaskCreationOptions.RunContinuationsAsynchronously);
            gl.CaptureRequested = true;
            gl.RequestNextFrameRendering();
            var resized = await capture.Task.WaitAsync(TimeSpan.FromSeconds(15));
            capture = null;
            var resizedCpu = cpuRenderer.Render(window.CurrentMesh!, window.CurrentScene, resized.Width, resized.Height);
            double resizedError = resized.Pixels.Zip(resizedCpu.Pixels).Average(p => Math.Abs(p.First - p.Second));
            if (resizedError > 2)
                throw new Exception("Resize/zoom clipping mismatch");
            log.Add($"Resize + zoom/clipping: mean error {resizedError:F4}/255 PASS");
            window.UseOpenGl = false;
            window.ResetView();
            await Task.Delay(500);
            using (var windowImage = new RenderTargetBitmap(new PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height)))
            {
                windowImage.Render(window);
                windowImage.Save(Path.Combine(folder, "window-cpu.png"));
            }
            log.Add("PASS");
        }
        catch (Exception error) { log.Add(error.ToString()); exitCode = 1; }
        finally
        {
            File.WriteAllLines(Path.Combine(folder, "result.txt"), log);
            foreach (string line in log)
                Console.WriteLine(line);
            gl.Captured -= OnCaptured;
        }
        return exitCode;
    }
}

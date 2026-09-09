using System.Numerics;
using Terrain.App.Rendering.OpenGl;
using Terrain.Core.Rendering.Shadows;
using System.Diagnostics;
using Avalonia;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Terrain.Core.Models;
using Terrain.Core.Rendering;

namespace Terrain.App.Controls;

public sealed class OpenGlTerrain : OpenGlControlBase
{
    private GlApi? api;
    private uint program, vao, vbo, ebo, depth, skyProgram;
    private uint shadowProgram, shadowFramebuffer, shadowTexture;
    private ShadowKey? shadowKey;
    private ShadowProjection? shadowProjection;
    public int ShadowBuildCount
    {
        get; private set;
    }
    private int bufferWidth, bufferHeight;
    private (Mesh? Mesh, float Scale, float X, float Y, float Z, bool Walking, ShadowProjection? Shadow) uploaded;
    public int GeometryUploadCount
    {
        get; private set;
    }
    private string? failure;
    public Mesh? Mesh
    {
        get; set;
    }
    public Scene Scene { get; set; } = new();
    public bool CaptureRequested
    {
        get; set;
    }
    public double LastFrameMilliseconds
    {
        get; private set;
    }
    public event Action<string>? Status;
    public event Action<Frame>? Captured;

    protected override void OnOpenGlInit(GlInterface gl)
    {
        try
        {
            api = new(gl);
            uploaded = default;
            uint vertex = api.Compile(0x8B31, """
                #version 150
                in vec3 aPosition;
                uniform vec3 cameraPosition;
                uniform vec3 cameraRight;
                uniform vec3 cameraUp;
                uniform vec3 cameraBack;
                uniform vec3 projection;
                uniform vec3 panAspect;
                in vec3 aNormal;
                in vec3 aColor;
                in vec3 aShadow;
                out vec3 normal;
                out vec3 color;
                out vec3 shadowPosition;
                void main() {
                    vec3 delta = aPosition - cameraPosition;
                    vec3 view = vec3(dot(delta,cameraRight),dot(delta,cameraUp),dot(delta,cameraBack));
                    float distance = -view.z;
                    gl_Position = vec4((view.x*projection.x+panAspect.x*distance)/panAspect.z,
                        view.y*projection.x+panAspect.y*distance,projection.y*distance-projection.z,distance);
                    normal = aNormal; color = aColor; shadowPosition = aShadow;
                }
                """);
            uint fragment = 0;
            try
            {
                fragment = api.Compile(0x8B30, """
                    #version 150
                    in vec3 normal;
                    in vec3 color;
                    in vec3 shadowPosition;
                    uniform vec3 light;
                    uniform vec3 fog;
                    uniform vec3 fogColor;
                    uniform vec3 fogViewport;
                    uniform vec3 fogPan;
                    uniform vec3 worldLighting;
                    uniform vec3 cloudState;
                    uniform vec3 cameraPosition;
                    uniform vec3 cameraRight;
                    uniform vec3 cameraUp;
                    uniform vec3 cameraBack;
                    uniform sampler2D shadowMap;
                    uniform int shadowsEnabled;
                    uniform int softShadows;
                    out vec4 outputColor;
                    """ + WorldSkyShader.Functions + """
                    float visibility(float nDotL, vec2 depthGradient) {
                        vec3 p = shadowPosition;
                        if (shadowsEnabled == 0 || p.x < 0.0 || p.x >= 1.0 || p.y < 0.0 || p.y >= 1.0 || p.z < 0.0 || p.z > 1.0) return 1.0;
                        ivec2 size = textureSize(shadowMap, 0);
                        ivec2 center = ivec2(floor(p.xy * vec2(size)));
                        float bias = 0.0015 + 0.004 * (1.0 - clamp(nDotL, 0.0, 1.0));
                        float visible = 0.0;
                        int radius = softShadows != 0 ? 1 : 0;
                        for (int y = -radius; y <= radius; y++)
                        for (int x = -radius; x <= radius; x++) {
                            ivec2 texel = center + ivec2(x, y);
                            if (any(lessThan(texel, ivec2(0))) || any(greaterThanEqual(texel, size))) visible += 1.0;
                            else {
                                vec2 delta = (vec2(texel) + 0.5) / vec2(size) - p.xy;
                                float reference = p.z + dot(depthGradient, delta) - bias;
                                if (reference <= texelFetch(shadowMap, texel, 0).r) visible += 1.0;
                            }
                        }
                        return visible / float((2 * radius + 1) * (2 * radius + 1));
                    }
                    void main() {
                        vec3 n = dot(normal, normal) > 1e-12 ? normalize(normal) : vec3(0.0, 1.0, 0.0);
                        float nDotL = dot(n, light);
                        // Derivatives are evaluated outside divergent shadow branches.
                        vec3 plane = cross(dFdx(shadowPosition), dFdy(shadowPosition));
                        vec2 gradient = abs(plane.z) > length(plane) * 1e-5 ? -plane.xy / plane.z : vec2(0.0);
                        float intensity = 0.3 + 0.7 * max(0.0, nDotL) * visibility(nDotL, gradient);
                        vec3 shaded = clamp(color * intensity, 0.0, 1.0) * worldLighting.y;
                        vec2 ndc = gl_FragCoord.xy / fogViewport.xy * 2.0 - 1.0;
                        vec3 view = vec3((ndc.x * fogViewport.z - fogPan.x) / fog.y,
                            (ndc.y - fogPan.y) / fog.y, 1.0) / gl_FragCoord.w;
                        float amount = 1.0 - exp(-fog.x * max(0.0, length(view) - 0.3));
                        vec3 atmosphere = fogColor;
                        if (worldLighting.x > 0.0) {
                            amount = smoothstep(fog.z * .45, fog.z, length(view));
                            vec3 ray = normalize(cameraRight * view.x + cameraUp * view.y - cameraBack * view.z);
                            atmosphere = cloudyWorldSky(ray,cameraPosition,worldLighting.z,1.0/fogViewport.y/fog.y,cloudState);
                        }
                        outputColor = vec4(mix(shaded, atmosphere, amount), 1.0);
                    }
                    """);
                program = api.Load<GlApi.CreateProgram>("glCreateProgram")();
                api.Load<GlApi.Attach>("glAttachShader")(program, vertex);
                api.Load<GlApi.Attach>("glAttachShader")(program, fragment);
                api.Load<GlApi.One>("glLinkProgram")(program);
                api.Verify(program, true);
            }
            finally
            {
                api.Load<GlApi.One>("glDeleteShader")(vertex);
                if (fragment != 0)
                    api.Load<GlApi.One>("glDeleteShader")(fragment);
            }
            api.Load<GlApi.Gen>("glGenVertexArrays")(1, out vao);
            api.Load<GlApi.Gen>("glGenBuffers")(1, out vbo);
            api.Load<GlApi.Gen>("glGenBuffers")(1, out ebo);
            api.Load<GlApi.Gen>("glGenRenderbuffers")(1, out depth);
            skyProgram = api.Link(SkyShader.Vertex, SkyShader.Fragment);
            shadowProgram = api.Link("""
                #version 150
                in vec3 aPosition;
                void main() { gl_Position = vec4(aPosition, 1.0); }
                """, """
                #version 150
                void main() { }
                """);
            api.Load<GlApi.Gen>("glGenFramebuffers")(1, out shadowFramebuffer);
            api.Load<GlApi.Gen>("glGenTextures")(1, out shadowTexture);
            api.Load<GlApi.One>("glActiveTexture")(0x84C0);
            api.Load<GlApi.Bind>("glBindTexture")(0x0DE1, shadowTexture);
            api.Load<GlApi.TexParameter>("glTexParameteri")(0x0DE1, 0x2801, 0x2600); // min NEAREST
            api.Load<GlApi.TexParameter>("glTexParameteri")(0x0DE1, 0x2800, 0x2600); // mag NEAREST
            api.Load<GlApi.TexParameter>("glTexParameteri")(0x0DE1, 0x2802, 0x812F); // clamp S
            api.Load<GlApi.TexParameter>("glTexParameteri")(0x0DE1, 0x2803, 0x812F); // clamp T
            api.Load<GlApi.TexParameter>("glTexParameteri")(0x0DE1, 0x884C, 0); // explicit shader comparisons
            api.Load<GlApi.TexImage>("glTexImage2D")(0x0DE1, 0, 0x8CAC, 1, 1, 0, 0x1902, 0x1406, 0);
        }
        catch (Exception error) { failure = error.Message; Status?.Invoke("OpenGL недоступен: " + failure); }
    }

    protected override unsafe void OnOpenGlRender(GlInterface gl, int framebuffer)
    {
        if (api is null || failure is not null || Mesh is null)
            return;
        try
        {
            var timer = Stopwatch.StartNew();
            double scaling = TopLevelScaling();
            int width = Math.Max(1, (int)Math.Round(Bounds.Width * scaling));
            int height = Math.Max(1, (int)Math.Round(Bounds.Height * scaling));
            api.Load<GlApi.Bind>("glBindFramebuffer")(0x8D40, (uint)framebuffer);
            api.Load<GlApi.Bind>("glBindRenderbuffer")(0x8D41, depth);
            if (width != bufferWidth || height != bufferHeight)
            {
                api.Load<GlApi.Storage>("glRenderbufferStorage")(0x8D41, 0x81A6, width, height);
                bufferWidth = width;
                bufferHeight = height;
            }
            api.Load<GlApi.AttachBuffer>("glFramebufferRenderbuffer")(0x8D40, 0x8D00, 0x8D41, depth);
            if (api.Load<GlApi.CheckFramebuffer>("glCheckFramebufferStatus")(0x8D40) != 0x8CD5)
                throw new InvalidOperationException("Не удалось создать framebuffer с Z-буфером.");
            api.Load<GlApi.Viewport>("glViewport")(0, 0, width, height);
            api.Load<GlApi.One>("glDisable")(0x0BE2); // blending
            api.Load<GlApi.One>("glDisable")(0x0B44); // culling: terrain is visible from both sides
            api.Load<GlApi.One>("glDisable")(0x0C11); // scissor
            api.Load<GlApi.One>("glDisable")(0x8DB9); // match CPU's direct RGB output
            api.Load<GlApi.One>("glEnable")(0x0B71);
            api.Load<GlApi.Boolean>("glDepthMask")(1);
            api.Load<GlApi.DoubleValue>("glClearDepth")(1);
            api.Load<GlApi.One>("glDepthFunc")(0x0201);
            bool shadowRebuilt = Scene.Shadows && PrepareShadowMap();
            api.Load<GlApi.Bind>("glBindFramebuffer")(0x8D40, (uint)framebuffer);
            api.Load<GlApi.Viewport>("glViewport")(0, 0, width, height);
            api.Load<GlApi.ClearColor>("glClearColor")(18 / 255f, 23 / 255f, 29 / 255f, 1);
            api.Load<GlApi.One>("glClear")(0x4000 | 0x0100);
            if (Scene.ShowSky)
            {
                api.Load<GlApi.One>("glDisable")(0x0B71);
                api.Load<GlApi.One>("glBindVertexArray")(vao);
                api.Load<GlApi.One>("glUseProgram")(skyProgram);
                api.Load<GlApi.Uniform3>("glUniform3f")(api.Load<GlApi.Location>("glGetUniformLocation")(skyProgram, "worldClock"), Scene.WorldTime ?? -1, 0, 0);
                var cloudState = Terrain.Core.World.WorldClouds.Parameters(Scene.CloudSeed, Scene.CloudTime);
                var skyCamera = Scene.ViewCamera.Position;
                api.Load<GlApi.Uniform3>("glUniform3f")(api.Load<GlApi.Location>("glGetUniformLocation")(skyProgram, "cloudState"), cloudState.X, cloudState.Y, cloudState.Z);
                api.Load<GlApi.Uniform3>("glUniform3f")(api.Load<GlApi.Location>("glGetUniformLocation")(skyProgram, "skyCamera"), skyCamera.X, skyCamera.Y, skyCamera.Z);
                var marker = Scene.SkySun;
                api.Load<GlApi.Uniform3>("glUniform3f")(api.Load<GlApi.Location>("glGetUniformLocation")(skyProgram, "sun"), marker.X, marker.Y, marker.Z);
                var sun = Scene.LightDirection;
                api.Load<GlApi.Uniform3>("glUniform3f")(api.Load<GlApi.Location>("glGetUniformLocation")(skyProgram, "light"), sun.X, sun.Y, sun.Z);
                api.Load<GlApi.Uniform3>("glUniform3f")(api.Load<GlApi.Location>("glGetUniformLocation")(skyProgram, "viewport"), width, height, CameraPose.FocalLength * Scene.Zoom);
                api.Load<GlApi.UniformInt>("glUniform1i")(api.Load<GlApi.Location>("glGetUniformLocation")(skyProgram, "walking"), Scene.Walking ? 1 : 0);
                void SkyVector(string name, System.Numerics.Vector3 axis)
                {
                    var vector = Scene.ViewCamera.WorldDirection(axis);
                    api.Load<GlApi.Uniform3>("glUniform3f")(api.Load<GlApi.Location>("glGetUniformLocation")(skyProgram, name), vector.X, vector.Y, vector.Z);
                }
                SkyVector("skyRight", System.Numerics.Vector3.UnitX);
                SkyVector("skyUp", System.Numerics.Vector3.UnitY);
                SkyVector("skyForward", -System.Numerics.Vector3.UnitZ);
                api.Load<GlApi.DrawArrays>("glDrawArrays")(0x0004, 0, 3);
                api.Load<GlApi.One>("glEnable")(0x0B71);
            }
            api.Load<GlApi.One>("glUseProgram")(program);
            api.Load<GlApi.One>("glBindVertexArray")(vao);
            var geometryKey = (Mesh, Scene.HeightScale, Scene.RotationX, Scene.RotationY, Scene.RotationZ, Scene.Walking, Scene.Shadows ? shadowProjection : null);
            if (uploaded != geometryKey)
            {
                var packed = new float[Mesh.Vertices.Length * 12];
                for (int i = 0; i < Mesh.Vertices.Length; i++)
                {
                    var vertex = Mesh.Vertices[i];
                    var world = Scene.WorldVertexPosition(vertex);
                    var normal = Scene.WorldNormal(vertex);
                    var shadow = Scene.Shadows ? shadowProjection!.Project(world) : Vector3.Zero;
                    int j = i * 12;
                    packed[j] = world.X;
                    packed[j + 1] = world.Y;
                    packed[j + 2] = world.Z;
                    packed[j + 3] = normal.X;
                    packed[j + 4] = normal.Y;
                    packed[j + 5] = normal.Z;
                    packed[j + 6] = vertex.Color.X;
                    packed[j + 7] = vertex.Color.Y;
                    packed[j + 8] = vertex.Color.Z;
                    packed[j + 9] = shadow.X;
                    packed[j + 10] = shadow.Y;
                    packed[j + 11] = shadow.Z;
                }
                api.Load<GlApi.Bind>("glBindBuffer")(0x8892, vbo);
                fixed (float* pointer = packed)
                    api.Load<GlApi.BufferData>("glBufferData")(0x8892, packed.Length * sizeof(float), pointer, 0x88E4);
                api.Load<GlApi.Bind>("glBindBuffer")(0x8893, ebo);
                fixed (int* pointer = Mesh.Indices)
                    api.Load<GlApi.BufferData>("glBufferData")(0x8893, Mesh.Indices.Length * sizeof(int), pointer, 0x88E4);
                uploaded = geometryKey;
                GeometryUploadCount++;
            }
            api.Load<GlApi.Bind>("glBindBuffer")(0x8892, vbo);
            api.Load<GlApi.Bind>("glBindBuffer")(0x8893, ebo);
            string[] attributes = ["aPosition", "aNormal", "aColor", "aShadow"];
            for (int i = 0; i < attributes.Length; i++)
            {
                uint location = (uint)api.Load<GlApi.Location>("glGetAttribLocation")(program, attributes[i]);
                api.Load<GlApi.One>("glEnableVertexAttribArray")(location);
                api.Load<GlApi.Attribute>("glVertexAttribPointer")(location, 3, 0x1406, 0, 12 * sizeof(float), i * 3 * sizeof(float));
            }
            void Uniform(string name, Vector3 value) => api.Load<GlApi.Uniform3>("glUniform3f")(
                api.Load<GlApi.Location>("glGetUniformLocation")(program, name), value.X, value.Y, value.Z);
            var camera = Scene.ViewCamera;
            Uniform("cameraPosition", camera.Position);
            Uniform("cloudState", Terrain.Core.World.WorldClouds.Parameters(Scene.CloudSeed, Scene.CloudTime));
            Uniform("cameraRight", camera.WorldDirection(Vector3.UnitX));
            Uniform("cameraUp", camera.WorldDirection(Vector3.UnitY));
            Uniform("cameraBack", camera.WorldDirection(Vector3.UnitZ));
            float near = Scene.NearDistance, far = CameraPose.Far;
            Uniform("projection", new(CameraPose.FocalLength * Scene.Zoom, (far + near) / (far - near), 2 * far * near / (far - near)));
            Uniform("panAspect", new(Scene.PanX, Scene.PanY, (float)width / height));
            Uniform("fog", new(Scene.FogDensity, CameraPose.FocalLength * Scene.Zoom, Scene.WorldViewDistance));
            Uniform("fogColor", Scene.WorldTime is { } time ? Terrain.Core.World.DayNight.Horizon(time) : Atmosphere.Color(Scene.LightDirection));
            Uniform("worldLighting", new(Scene.WorldTime.HasValue ? 1 : 0, Scene.WorldTime is { } t ? Terrain.Core.World.DayNight.Exposure(t) : 1, Scene.WorldTime ?? 0));
            Uniform("fogViewport", new(width, height, (float)width / height));
            Uniform("fogPan", new(Scene.PanX, Scene.PanY, 0));
            var light = Scene.LightDirection;
            api.Load<GlApi.Uniform3>("glUniform3f")(api.Load<GlApi.Location>("glGetUniformLocation")(program, "light"), light.X, light.Y, light.Z);
            api.Load<GlApi.One>("glActiveTexture")(0x84C0);
            api.Load<GlApi.Bind>("glBindSampler")(0, 0);
            api.Load<GlApi.Bind>("glBindTexture")(0x0DE1, shadowTexture);
            api.Load<GlApi.UniformInt>("glUniform1i")(api.Load<GlApi.Location>("glGetUniformLocation")(program, "shadowMap"), 0);
            api.Load<GlApi.UniformInt>("glUniform1i")(api.Load<GlApi.Location>("glGetUniformLocation")(program, "shadowsEnabled"), Scene.Shadows ? 1 : 0);
            api.Load<GlApi.UniformInt>("glUniform1i")(api.Load<GlApi.Location>("glGetUniformLocation")(program, "softShadows"), Scene.SoftShadows ? 1 : 0);
            api.Load<GlApi.Draw>("glDrawElements")(0x0004, Mesh.Indices.Length, 0x1405, 0);
            // Include completion, not just command submission, in the displayed frame time.
            api.Load<GlApi.Empty>("glFinish")();
            timer.Stop();
            LastFrameMilliseconds = timer.Elapsed.TotalMilliseconds;
            Status?.Invoke($"OpenGL · {width}×{height} · кадр {timer.Elapsed.TotalMilliseconds:F1} мс" + (shadowRebuilt ? " · тени обновлены" : ""));
            if (CaptureRequested)
            {
                CaptureRequested = false;
                var pixels = new byte[width * height * 4];
                api.Load<GlApi.PixelStore>("glPixelStorei")(0x0D05, 1);
                fixed (byte* pointer = pixels)
                    api.Load<GlApi.ReadPixels>("glReadPixels")(0, 0, width, height, 0x80E1, 0x1401, pointer);
                var flipped = new byte[pixels.Length];
                for (int y = 0; y < height; y++)
                    Array.Copy(pixels, y * width * 4, flipped, (height - y - 1) * width * 4, width * 4);
                Captured?.Invoke(new(width, height, flipped));
            }
            api.Load<GlApi.One>("glBindVertexArray")(0);
            api.Load<GlApi.One>("glUseProgram")(0);
            api.Load<GlApi.One>("glDisable")(0x0B71);
            api.Load<GlApi.Bind>("glBindTexture")(0x0DE1, 0);
        }
        catch (Exception error) { failure = error.Message; Status?.Invoke("Ошибка OpenGL: " + failure); }
    }
    private unsafe bool PrepareShadowMap()
    {
        var nextKey = ShadowKey.From(Mesh!, Scene);
        if (shadowKey == nextKey)
            return false;
        int size = Scene.ShadowResolution;
        if (size < 16 || size > 4096)
            throw new ArgumentOutOfRangeException(nameof(Scene.ShadowResolution));
        var projection = ShadowProjection.Create(Mesh!, Scene);
        api!.Load<GlApi.One>("glActiveTexture")(0x84C0);
        api.Load<GlApi.Bind>("glBindTexture")(0x0DE1, shadowTexture);
        if (shadowKey?.Resolution != size)
            api.Load<GlApi.TexImage>("glTexImage2D")(0x0DE1, 0, 0x8CAC, size, size, 0, 0x1902, 0x1406, 0);
        api.Load<GlApi.Bind>("glBindFramebuffer")(0x8D40, shadowFramebuffer);
        api.Load<GlApi.AttachTexture>("glFramebufferTexture2D")(0x8D40, 0x8D00, 0x0DE1, shadowTexture, 0);
        api.Load<GlApi.One>("glDrawBuffer")(0);
        api.Load<GlApi.One>("glReadBuffer")(0);
        if (api.Load<GlApi.CheckFramebuffer>("glCheckFramebufferStatus")(0x8D40) != 0x8CD5)
            throw new InvalidOperationException("Не удалось создать карту глубины света.");
        api.Load<GlApi.Viewport>("glViewport")(0, 0, size, size);
        api.Load<GlApi.One>("glClear")(0x0100);
        api.Load<GlApi.One>("glUseProgram")(shadowProgram);
        api.Load<GlApi.One>("glBindVertexArray")(vao);
        var positions = new float[Mesh!.Vertices.Length * 3];
        for (int i = 0; i < Mesh.Vertices.Length; i++)
        {
            var p = projection.Project(Scene.WorldVertexPosition(Mesh.Vertices[i])) * 2 - System.Numerics.Vector3.One;
            positions[i * 3] = p.X;
            positions[i * 3 + 1] = p.Y;
            positions[i * 3 + 2] = p.Z;
        }
        api.Load<GlApi.Bind>("glBindBuffer")(0x8892, vbo);
        fixed (float* pointer = positions)
            api.Load<GlApi.BufferData>("glBufferData")(0x8892, positions.Length * sizeof(float), pointer, 0x88E0);
        api.Load<GlApi.Bind>("glBindBuffer")(0x8893, ebo);
        fixed (int* pointer = Mesh.Indices)
            api.Load<GlApi.BufferData>("glBufferData")(0x8893, Mesh.Indices.Length * sizeof(int), pointer, 0x88E0);
        uint location = (uint)api.Load<GlApi.Location>("glGetAttribLocation")(shadowProgram, "aPosition");
        api.Load<GlApi.One>("glEnableVertexAttribArray")(location);
        api.Load<GlApi.Attribute>("glVertexAttribPointer")(location, 3, 0x1406, 0, 3 * sizeof(float), 0);
        api.Load<GlApi.Draw>("glDrawElements")(0x0004, Mesh.Indices.Length, 0x1405, 0);
        shadowProjection = projection;
        shadowKey = nextKey;
        ShadowBuildCount++;
        return true;
    }
    private double TopLevelScaling() => Avalonia.Controls.TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        if (api is null)
            return;
        if (depth != 0)
            api.Load<GlApi.Delete>("glDeleteRenderbuffers")(1, ref depth);
        if (vbo != 0)
            api.Load<GlApi.Delete>("glDeleteBuffers")(1, ref vbo);
        if (ebo != 0)
            api.Load<GlApi.Delete>("glDeleteBuffers")(1, ref ebo);
        if (vao != 0)
            api.Load<GlApi.Delete>("glDeleteVertexArrays")(1, ref vao);
        if (program != 0)
            api.Load<GlApi.One>("glDeleteProgram")(program);
        if (skyProgram != 0)
            api.Load<GlApi.One>("glDeleteProgram")(skyProgram);
        if (shadowProgram != 0)
            api.Load<GlApi.One>("glDeleteProgram")(shadowProgram);
        if (shadowTexture != 0)
            api.Load<GlApi.Delete>("glDeleteTextures")(1, ref shadowTexture);
        if (shadowFramebuffer != 0)
            api.Load<GlApi.Delete>("glDeleteFramebuffers")(1, ref shadowFramebuffer);
        shadowProgram = shadowTexture = shadowFramebuffer = 0;
        shadowKey = null;
        shadowProjection = null;
        api = null;
        program = vao = vbo = ebo = depth = skyProgram = 0;
        bufferWidth = bufferHeight = 0;
        failure = null;
    }
}

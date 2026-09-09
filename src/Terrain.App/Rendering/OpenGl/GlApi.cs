using System.Runtime.InteropServices;
using Avalonia.OpenGL;

namespace Terrain.App.Rendering.OpenGl;

// Thin function-pointer bindings, not a rendering engine. All drawing logic is ours.
internal sealed unsafe class GlApi(GlInterface gl)
{
    public T Load<T>(string name) where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(gl.GetProcAddress(name));
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void Gen(int count, out uint value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void Delete(int count, ref uint value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void Bind(uint target, uint value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void One(uint value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void Empty();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void Boolean(byte value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void DoubleValue(double value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void Viewport(int x, int y, int width, int height);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void ClearColor(float r, float g, float b, float a);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void BufferData(uint target, nint size, void* data, uint usage);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void Attribute(uint index, int size, uint type, byte normalized, int stride, nint pointer);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void Draw(uint mode, int count, uint type, nint indices);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate uint CreateShader(uint type);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate uint CreateProgram();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void ShaderSource(uint shader, int count, nint strings, nint lengths);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void GetStatus(uint obj, uint parameter, out int value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void GetLog(uint obj, int maxLength, out int length, nint log);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void Attach(uint program, uint shader);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate int Location(uint program, [MarshalAs(UnmanagedType.LPStr)] string name);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void Uniform3(int location, float x, float y, float z);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void Storage(uint target, uint format, int width, int height);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void AttachBuffer(uint target, uint attachment, uint renderbufferTarget, uint renderbuffer);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate uint CheckFramebuffer(uint target);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void ReadPixels(int x, int y, int width, int height, uint format, uint type, void* pixels);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void PixelStore(uint name, int value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void UniformInt(int location, int value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void TexImage(uint target, int level, int internalFormat, int width, int height, int border, uint format, uint type, nint pixels);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void TexParameter(uint target, uint name, int value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void AttachTexture(uint target, uint attachment, uint textureTarget, uint texture, int level);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void DrawArrays(uint mode, int first, int count);

    public uint Link(string vertexSource, string fragmentSource)
    {
        uint vertex = Compile(0x8B31, vertexSource), fragment = 0, program = 0;
        try
        {
            fragment = Compile(0x8B30, fragmentSource);
            program = Load<CreateProgram>("glCreateProgram")();
            Load<Attach>("glAttachShader")(program, vertex);
            Load<Attach>("glAttachShader")(program, fragment);
            Load<One>("glLinkProgram")(program);
            Verify(program, true);
            return program;
        }
        catch { if (program != 0) Load<One>("glDeleteProgram")(program); throw; }
        finally
        {
            Load<One>("glDeleteShader")(vertex);
            if (fragment != 0)
                Load<One>("glDeleteShader")(fragment);
        }
    }

    public uint Compile(uint type, string source)
    {
        uint shader = Load<CreateShader>("glCreateShader")(type);
        nint text = Marshal.StringToHGlobalAnsi(source);
        try
        {
            Load<ShaderSource>("glShaderSource")(shader, 1, (nint)(&text), 0);
            Load<One>("glCompileShader")(shader);
            Verify(shader, false);
            return shader;
        }
        catch { Load<One>("glDeleteShader")(shader); throw; }
        finally { Marshal.FreeHGlobal(text); }
    }
    public void Verify(uint obj, bool program)
    {
        Load<GetStatus>(program ? "glGetProgramiv" : "glGetShaderiv")(obj, program ? 0x8B82u : 0x8B81u, out int success);
        if (success != 0)
            return;
        nint buffer = Marshal.AllocHGlobal(8192);
        try
        {
            Load<GetLog>(program ? "glGetProgramInfoLog" : "glGetShaderInfoLog")(obj, 8192, out _, buffer);
            throw new InvalidOperationException(Marshal.PtrToStringAnsi(buffer));
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }
}

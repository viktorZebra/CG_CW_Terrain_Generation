using System.Runtime.InteropServices;
using Avalonia;

namespace Terrain.App.Services;

/// <summary>App-local relative mouse/trackpad input. All calls run on the UI thread.</summary>
internal sealed class MacMouseLook
{
    private const string Graphics = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
    private const string ObjC = "/usr/lib/libobjc.A.dylib";
    [DllImport(Graphics)] private static extern int CGAssociateMouseAndMouseCursorPosition(uint connected);
    [DllImport(ObjC)] private static extern nint objc_getClass(string name);
    [DllImport(ObjC)] private static extern nint sel_registerName(string name);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern nint AddMonitor(nint receiver, nint selector, ulong mask, nint block);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern void RemoveMonitor(nint receiver, nint selector, nint monitor);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern double GetDouble(nint receiver, nint selector);

    // Global Objective-C blocks must remain valid for the entire process lifetime.
    [StructLayout(LayoutKind.Sequential)]
    private struct Block
    {
        public nint Isa; public int Flags, Reserved; public nint Invoke, Descriptor;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct Descriptor
    {
        public nuint Reserved, Size;
    }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint EventHandler(nint block, nint mouseEvent);
    private static class BlockRuntime
    {
        // One block, descriptor and rooted delegate for the process, not one allocation per capture.
        // removeMonitor can defer observer destruction until an autorelease pool drains.
        private static readonly EventHandler Handler = OnMotion;
        internal static readonly nint Pointer = Create();
        internal static MacMouseLook? Owner;

        private static nint Create()
        {
            nint descriptor = Marshal.AllocHGlobal(Marshal.SizeOf<Descriptor>());
            Marshal.StructureToPtr(new Descriptor { Size = (nuint)Marshal.SizeOf<Block>() }, descriptor, false);
            nint block = Marshal.AllocHGlobal(Marshal.SizeOf<Block>());
            Marshal.StructureToPtr(new Block
            {
                Isa = NativeLibrary.GetExport(NativeLibrary.Load("/usr/lib/libSystem.B.dylib"), "_NSConcreteGlobalBlock"),
                Flags = 1 << 28,
                Invoke = Marshal.GetFunctionPointerForDelegate(Handler),
                Descriptor = descriptor
            }, block, false);
            return block;
        }

        private static nint OnMotion(nint unused, nint mouseEvent)
        {
            if (Owner is { Active: true } owner)
                owner.OnMotion(mouseEvent);
            return mouseEvent;
        }
    }
    private nint monitor, deltaX, deltaY;
    private Vector pending;
    public bool Active
    {
        get; private set;
    }

    public bool Begin()
    {
        if (Active)
            return true;
        if (!OperatingSystem.IsMacOS())
            return false;
        try
        {
            deltaX = sel_registerName("deltaX");
            deltaY = sel_registerName("deltaY");
            BlockRuntime.Owner?.End();
            // Moved, left/right/other dragged: holding the trackpad also remains supported.
            monitor = AddMonitor(objc_getClass("NSEvent"), sel_registerName("addLocalMonitorForEventsMatchingMask:handler:"),
                (1UL << 5) | (1UL << 6) | (1UL << 7) | (1UL << 27), BlockRuntime.Pointer);
            if (monitor == 0 || CGAssociateMouseAndMouseCursorPosition(0) != 0)
            {
                End();
                return false;
            }
            pending = default;
            BlockRuntime.Owner = this;
            Active = true;
            return true;
        }
        catch
        {
            End();
            throw;
        }
    }

    private void OnMotion(nint mouseEvent)
    {
        double x = GetDouble(mouseEvent, deltaX), y = GetDouble(mouseEvent, deltaY);
        if (double.IsFinite(x) && double.IsFinite(y))
            pending += new Vector(x, y);
    }

    public Vector ReadDelta()
    {
        var delta = pending;
        pending = default; // A movement is consumed once, never replayed on an idle timer tick.
        return delta;
    }

    public void End()
    {
        bool wasActive = Active;
        Active = false;
        pending = default;
        if (!OperatingSystem.IsMacOS())
            return;
        if (BlockRuntime.Owner == this)
            BlockRuntime.Owner = null;
        if (wasActive)
            CGAssociateMouseAndMouseCursorPosition(1);
        nint removed = monitor;
        monitor = 0;
        if (removed != 0)
            RemoveMonitor(objc_getClass("NSEvent"), sel_registerName("removeMonitor:"), removed);
        // The shared global block is deliberately not freed: AppKit can still release it later.
    }
}

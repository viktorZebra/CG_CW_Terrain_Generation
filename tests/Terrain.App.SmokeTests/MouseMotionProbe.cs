using System.Runtime.InteropServices;

namespace Terrain.App.SmokeTests;

// Sends native relative movement through this app's event queue, without global input injection.
internal static class MouseMotionProbe
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public double X, Y;
    }
    private const string Graphics = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
    private const string ObjC = "/usr/lib/libobjc.A.dylib";
    [DllImport(Graphics)] private static extern nint CGEventCreateMouseEvent(nint source, uint type, Point point, uint button);
    [DllImport(Graphics)] private static extern void CGEventSetIntegerValueField(nint value, uint field, long number);
    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")] private static extern void CFRelease(nint value);
    [DllImport(ObjC)] private static extern nint objc_getClass(string name);
    [DllImport(ObjC)] private static extern nint sel_registerName(string name);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern nint Send(nint receiver, nint selector);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern nint ConvertEvent(nint receiver, nint selector, nint value);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern void Post(nint receiver, nint selector, nint value, byte atStart);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern void Activate(nint receiver, nint selector, byte ignoreOtherApps);
    public static void ActivateApplication()
    {
        if (!OperatingSystem.IsMacOS())
            return;
        nint app = Send(objc_getClass("NSApplication"), sel_registerName("sharedApplication"));
        Activate(app, sel_registerName("activateIgnoringOtherApps:"), 1);
    }

    [DllImport(ObjC)] private static extern nint objc_autoreleasePoolPush();
    [DllImport(ObjC)] private static extern void objc_autoreleasePoolPop(nint pool);

    public static void WithAutoreleasePool(Action action)
    {
        nint pool = objc_autoreleasePoolPush();
        try
        {
            action();
        }
        finally { objc_autoreleasePoolPop(pool); }
    }

    public static void Move(long dx, long dy, bool dragging = false)
    {
        nint value = CGEventCreateMouseEvent(0, dragging ? 6u : 5u, default, 0);
        if (value == 0)
            throw new Exception("Cannot create test mouse event");
        try
        {
            CGEventSetIntegerValueField(value, 4, dx);
            CGEventSetIntegerValueField(value, 5, dy);
            nint nativeEvent = ConvertEvent(objc_getClass("NSEvent"), sel_registerName("eventWithCGEvent:"), value);
            nint app = Send(objc_getClass("NSApplication"), sel_registerName("sharedApplication"));
            Post(app, sel_registerName("postEvent:atStart:"), nativeEvent, 0);
        }
        finally { CFRelease(value); }
    }
}

using System.Numerics;
using System.Runtime.InteropServices;
using OpenTabletDriver.Plugin;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.Output;
using OpenTabletDriver.Plugin.Tablet;

namespace TabletUtilityPack;

internal static class DragScrollState
{
    private static readonly object Sync = new();

    private static bool _active;
    private static Vector2? _last;

    public static void Start()
    {
        lock (Sync)
        {
            _active = true;
            // Binding reports use device coordinates, while this filter runs in
            // post-transform screen coordinates. Seed from the filter instead.
            _last = null;
        }
    }

    public static void Stop()
    {
        lock (Sync)
        {
            _active = false;
            _last = null;
        }
    }

    public static bool TryStep(Vector2 position, out Vector2 delta, out bool resetRemainder)
    {
        lock (Sync)
        {
            if (!_active)
            {
                delta = default;
                resetRemainder = true;
                return false;
            }

            if (_last is null)
            {
                _last = position;
                delta = default;
                resetRemainder = true;
                return false;
            }

            delta = position - _last.Value;
            _last = position;
            resetRemainder = false;
            return true;
        }
    }
}

[PluginName("Hold Pen Drag Scroll")]
public sealed class HoldPenDragScroll : IStateBinding
{
    public void Press(TabletReference tablet, IDeviceReport report)
    {
        DragScrollState.Start();
        Log.Debug(nameof(HoldPenDragScroll), "Drag scroll active");
    }

    public void Release(TabletReference tablet, IDeviceReport report)
    {
        DragScrollState.Stop();
        Log.Debug(nameof(HoldPenDragScroll), "Drag scroll inactive");
    }

    public override string ToString() => "Hold Pen Drag Scroll";
}

[PluginName("Pen Drag Scroll Filter")]
public sealed class PenDragScrollFilter : IPositionedPipelineElement<IDeviceReport>
{
    private const float UnitsPerScrollPixel = 1.0f;
    private const int MaxPixelsPerReport = 80;
    private Vector2 _remainder;

    public PipelinePosition Position => PipelinePosition.PostTransform;

    public event Action<IDeviceReport>? Emit;

    public void Consume(IDeviceReport value)
    {
        if (value is not IAbsolutePositionReport positionReport)
        {
            Emit?.Invoke(value);
            return;
        }

        if (!DragScrollState.TryStep(positionReport.Position, out var delta, out var resetRemainder))
        {
            if (resetRemainder)
                _remainder = default;

            Emit?.Invoke(value);
            return;
        }

        _remainder += delta;

        var horizontal = TakePixels(ref _remainder.X);
        var vertical = TakePixels(ref _remainder.Y);

        if (horizontal != 0 || vertical != 0)
            MacNativeScroll.Post(vertical, horizontal);

        Emit?.Invoke(value);
    }

    public override string ToString() => "Pen Drag Scroll Filter";

    private static int TakePixels(ref float value)
    {
        if (MathF.Abs(value) < UnitsPerScrollPixel)
            return 0;

        var pixels = (int)(value / UnitsPerScrollPixel);
        value -= pixels * UnitsPerScrollPixel;
        return Math.Clamp(pixels, -MaxPixelsPerReport, MaxPixelsPerReport);
    }
}

internal static class MacNativeScroll
{
    private const int kCGHIDEventTap = 0;
    private const int kCGScrollEventUnitPixel = 0;

    public static void Post(int verticalPixels, int horizontalPixels)
    {
        var ev = CGEventCreateScrollWheelEvent2(IntPtr.Zero, kCGScrollEventUnitPixel, 2, verticalPixels, horizontalPixels, 0);
        if (ev == IntPtr.Zero)
        {
            Log.Write(nameof(MacNativeScroll), "CGEventCreateScrollWheelEvent2 returned null.", LogLevel.Error);
            return;
        }

        CGEventPost(kCGHIDEventTap, ev);
        CFRelease(ev);
    }

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern IntPtr CGEventCreateScrollWheelEvent2(IntPtr source, int units, uint wheelCount, int wheel1, int wheel2, int wheel3);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern void CGEventPost(int tap, IntPtr ev);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRelease(IntPtr cf);
}

using System.Numerics;
using OpenTabletDriver.Desktop.Contracts;
using OpenTabletDriver.Native.OSX;
using OpenTabletDriver.Plugin;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.DependencyInjection;
using OpenTabletDriver.Plugin.Output;
using OpenTabletDriver.Plugin.Platform.Display;
using OpenTabletDriver.Plugin.Tablet;

namespace TabletUtilityPack;

internal readonly record struct MonitorArea(Vector2 Center, float Width, float Height)
{
    public Vector2 TopLeft => Center - new Vector2(Width / 2f, Height / 2f);

    public bool IsUsable => Width > 0 && Height > 0;
}

internal sealed record SmartMonitorTarget(
    MonitorArea Source,
    MonitorArea Target,
    int Index,
    string Signature);

internal static class SmartMonitorCycleState
{
    private static readonly object Sync = new();
    private static SmartMonitorTarget? _target;
    private static int _requestVersion;
    private static string _order = "LeftToRight";
    private static string _direction = "Forward";

    public static SmartMonitorTarget? Current
    {
        get
        {
            lock (Sync)
                return _target;
        }
    }

    public static void Request(string order, string direction)
    {
        lock (Sync)
        {
            _order = string.IsNullOrWhiteSpace(order) ? "LeftToRight" : order.Trim();
            _direction = string.IsNullOrWhiteSpace(direction) ? "Forward" : direction.Trim();
            _requestVersion++;
        }
    }

    public static (int Version, string Order, string Direction) GetRequest()
    {
        lock (Sync)
            return (_requestVersion, _order, _direction);
    }

    public static void Set(SmartMonitorTarget? target)
    {
        lock (Sync)
            _target = target;
    }
}

[PluginName("Smart Monitor Cycle")]
public sealed class SmartMonitorCycle : IStateBinding
{
    private string _order = "LeftToRight";
    private string _direction = "Forward";

    [Property("Order")]
    public string Order
    {
        get => _order;
        set => _order = string.IsNullOrWhiteSpace(value) ? "LeftToRight" : value.Trim();
    }

    [Property("Direction")]
    public string Direction
    {
        get => _direction;
        set => _direction = string.IsNullOrWhiteSpace(value) ? "Forward" : value.Trim();
    }

    public void Press(TabletReference tablet, IDeviceReport report)
    {
        SmartMonitorCycleState.Request(_order, _direction);
        Log.Write(nameof(SmartMonitorCycle), $"Smart monitor cycle request: {_order}, {_direction}.", LogLevel.Info);
    }

    public void Release(TabletReference tablet, IDeviceReport report)
    {
    }

    public override string ToString() => $"Smart Monitor Cycle: {_order}, {_direction}";
}

[PluginName("Smart Monitor Cycle Filter")]
public sealed class SmartMonitorCycleFilter : IPositionedPipelineElement<IDeviceReport>
{
    private int _handledRequestVersion = SmartMonitorCycleState.GetRequest().Version;
    private bool _initialized;

    [Resolved]
    public IVirtualScreen Screen { get; set; } = null!;

    [Resolved]
    public IDriverDaemon Daemon { get; set; } = null!;

    [TabletReference]
    public TabletReference Tablet { get; set; } = null!;

    public PipelinePosition Position => PipelinePosition.PostTransform;

    public event Action<IDeviceReport>? Emit;

    public void Consume(IDeviceReport value)
    {
        if (value is not ITabletReport tabletReport)
        {
            Emit?.Invoke(value);
            return;
        }

        var request = SmartMonitorCycleState.GetRequest();
        if (!_initialized)
        {
            _initialized = true;
            AlignToConfiguredDisplay(tabletReport.Position, request.Order);
        }

        if (request.Version != _handledRequestVersion)
        {
            _handledRequestVersion = request.Version;
            ResolveTarget(tabletReport.Position, request.Order, request.Direction);
        }

        var target = SmartMonitorCycleState.Current;
        if (target is null || !target.Source.IsUsable || !target.Target.IsUsable)
        {
            Emit?.Invoke(value);
            return;
        }

        tabletReport.Position = MapArea(tabletReport.Position, target.Source, target.Target);

        Emit?.Invoke(value);
    }

    public override string ToString() => "Smart Monitor Cycle Filter";

    private void AlignToConfiguredDisplay(Vector2 currentPosition, string order)
    {
        try
        {
            var displays = GetOrderedDisplays(GetCurrentDisplays(), order);
            if (displays.Count == 0)
            {
                SmartMonitorCycleState.Set(null);
                return;
            }

            var sourceArea = GetConfiguredOutputArea(displays, currentPosition);
            var index = FindNearestDisplayIndex(displays, sourceArea.Center);
            var targetArea = displays[index];
            var signature = GetSignature(displays);

            SmartMonitorCycleState.Set(new SmartMonitorTarget(sourceArea, targetArea, index, signature));
            Log.Write(nameof(SmartMonitorCycleFilter), $"Smart monitor aligned: configured {FormatArea(sourceArea)}, live {FormatArea(targetArea)}.", LogLevel.Info);
        }
        catch (Exception ex)
        {
            SmartMonitorCycleState.Set(null);
            Log.Write(nameof(SmartMonitorCycleFilter), ex.ToString(), LogLevel.Error);
        }
    }

    private void ResolveTarget(Vector2 currentPosition, string order, string direction)
    {
        try
        {
            if (Screen is null)
            {
                SmartMonitorCycleState.Set(null);
                Log.Write(nameof(SmartMonitorCycleFilter), "IVirtualScreen was not resolved.", LogLevel.Error);
                return;
            }

            var displays = GetOrderedDisplays(GetCurrentDisplays(), order);
            Log.Write(nameof(SmartMonitorCycleFilter), $"Smart monitor cycle filter sees {displays.Count} display(s).", LogLevel.Info);

            if (displays.Count == 0)
            {
                SmartMonitorCycleState.Set(null);
                return;
            }

            var signature = GetSignature(displays);
            var currentTarget = SmartMonitorCycleState.Current;
            var sourceArea = GetConfiguredOutputArea(displays, currentPosition);
            int currentIndex;

            if (currentTarget is not null && currentTarget.Signature == signature)
            {
                currentIndex = currentTarget.Index;
            }
            else if (currentTarget is not null)
            {
                currentIndex = FindNearestDisplayIndex(displays, currentTarget.Target.Center);
            }
            else
            {
                currentIndex = FindNearestDisplayIndex(displays, sourceArea.Center);
            }

            var step = IsBackward(direction) ? -1 : 1;
            var nextIndex = PositiveModulo(currentIndex + step, displays.Count);
            var targetArea = displays[nextIndex];

            SmartMonitorCycleState.Set(new SmartMonitorTarget(sourceArea, targetArea, nextIndex, signature));
            Log.Write(nameof(SmartMonitorCycleFilter), $"Smart monitor target {nextIndex + 1}/{displays.Count}: configured {FormatArea(sourceArea)}, live {FormatArea(targetArea)}.", LogLevel.Info);
        }
        catch (Exception ex)
        {
            SmartMonitorCycleState.Set(null);
            Log.Write(nameof(SmartMonitorCycleFilter), ex.ToString(), LogLevel.Error);
        }
    }

    private IReadOnlyList<MonitorArea> GetCurrentDisplays()
    {
        if (OperatingSystem.IsMacOS())
        {
            try
            {
                const int capacity = 32;
                var displayIds = new uint[capacity];
                var error = OSX.CGGetActiveDisplayList(capacity, displayIds, out var count);
                if (error == 0 && count > 0)
                {
                    var bounds = displayIds
                        .Take((int)Math.Min(count, capacity))
                        .Select(OSX.CGDisplayBounds)
                        .ToList();
                    var offsetX = bounds.Min(bound => bound.origin.x);
                    var offsetY = bounds.Min(bound => bound.origin.y);

                    return bounds.Select(bound => new MonitorArea(
                            new Vector2(
                                (float)(bound.origin.x - offsetX + bound.size.width / 2d),
                                (float)(bound.origin.y - offsetY + bound.size.height / 2d)),
                            (float)bound.size.width,
                            (float)bound.size.height))
                        .Where(display => display.IsUsable)
                        .ToList();
                }

                Log.Write(nameof(SmartMonitorCycleFilter), $"Live macOS display query failed with error {error}; using OTD display cache.", LogLevel.Warning);
            }
            catch (Exception ex)
            {
                Log.Write(nameof(SmartMonitorCycleFilter), $"Live macOS display query failed: {ex.Message}; using OTD display cache.", LogLevel.Warning);
            }
        }

        return Screen.Displays
            .Select(display => new MonitorArea(
                display.Position + new Vector2(display.Width / 2f, display.Height / 2f),
                display.Width,
                display.Height))
            .Where(display => display.IsUsable)
            .ToList();
    }

    private MonitorArea GetConfiguredOutputArea(IReadOnlyList<MonitorArea> displays, Vector2 currentPosition)
    {
        try
        {
            var settings = Daemon?.GetSettings().GetAwaiter().GetResult();
            var profile = settings?.Profiles.GetProfile(Tablet.Properties.Name);
            var area = profile?.AbsoluteModeSettings.Display.Area;
            if (area is not null)
            {
                var configured = new MonitorArea(area.Position, area.Width, area.Height);
                if (configured.IsUsable)
                    return configured;
            }
        }
        catch (Exception ex)
        {
            Log.Write(nameof(SmartMonitorCycleFilter), $"Unable to read configured display area: {ex.Message}", LogLevel.Warning);
        }

        var fallbackIndex = FindNearestDisplayIndex(displays, currentPosition);
        return displays[fallbackIndex];
    }

    private static List<MonitorArea> GetOrderedDisplays(IEnumerable<MonitorArea> source, string order)
    {
        var displays = source.Where(display => display.IsUsable);

        if (order.Equals("System", StringComparison.OrdinalIgnoreCase))
            return displays.ToList();

        if (order.Equals("TopToBottom", StringComparison.OrdinalIgnoreCase))
            return displays
                .OrderBy(display => display.Center.Y)
                .ThenBy(display => display.Center.X)
                .ToList();

        return displays
            .OrderBy(display => display.Center.X)
            .ThenBy(display => display.Center.Y)
            .ToList();
    }

    private static int FindNearestDisplayIndex(IReadOnlyList<MonitorArea> displays, Vector2 point)
    {
        var bestIndex = 0;
        var bestDistance = float.MaxValue;

        for (var i = 0; i < displays.Count; i++)
        {
            var distance = Vector2.DistanceSquared(displays[i].Center, point);
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            bestIndex = i;
        }

        return bestIndex;
    }

    private static string GetSignature(IEnumerable<MonitorArea> displays)
    {
        return string.Join("|", displays.Select(display =>
            $"{display.Center.X:0.###},{display.Center.Y:0.###},{display.Width:0.###},{display.Height:0.###}"));
    }

    private static bool IsBackward(string direction)
    {
        return direction.Equals("Backward", StringComparison.OrdinalIgnoreCase) ||
               direction.Equals("Previous", StringComparison.OrdinalIgnoreCase) ||
               direction.Equals("Reverse", StringComparison.OrdinalIgnoreCase) ||
               direction.Equals("Left", StringComparison.OrdinalIgnoreCase);
    }

    private static int PositiveModulo(int value, int divisor)
    {
        var result = value % divisor;
        return result < 0 ? result + divisor : result;
    }

    private static Vector2 MapArea(Vector2 position, MonitorArea source, MonitorArea target)
    {
        var sourceTopLeft = source.TopLeft;
        var targetTopLeft = target.TopLeft;
        var unit = new Vector2(
            (position.X - sourceTopLeft.X) / source.Width,
            (position.Y - sourceTopLeft.Y) / source.Height);

        return new Vector2(
            targetTopLeft.X + unit.X * target.Width,
            targetTopLeft.Y + unit.Y * target.Height);
    }

    private static string FormatArea(MonitorArea area) =>
        $"{area.Width:0.###}x{area.Height:0.###} at {area.Center}";
}

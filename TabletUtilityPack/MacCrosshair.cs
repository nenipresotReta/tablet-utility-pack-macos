using OpenTabletDriver.Plugin;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.Tablet;

namespace TabletUtilityPack;

[PluginName("Toggle Crosshair")]
public sealed class ToggleCrosshair : IStateBinding
{
    public void Press(TabletReference tablet, IDeviceReport report)
    {
        NativeHelper.Start(
            "MacCrosshairOverlay",
            "MacCrosshairHelper",
            "toggle",
            Environment.ProcessId.ToString());
    }

    public void Release(TabletReference tablet, IDeviceReport report)
    {
    }

    public override string ToString() => "Toggle Crosshair";
}

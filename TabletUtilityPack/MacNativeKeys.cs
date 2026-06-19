using OpenTabletDriver.Plugin;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.Tablet;

namespace TabletUtilityPack;

public abstract class MacNativeKeyBinding : IStateBinding
{
    protected abstract string Hotkey { get; }
    protected abstract string DisplayName { get; }

    public void Press(TabletReference tablet, IDeviceReport report)
    {
        NativeHelper.Start("MacNativeKeyHelper", "MacNativeKeyHelper", Hotkey, "down");
    }

    public void Release(TabletReference tablet, IDeviceReport report)
    {
        NativeHelper.Start("MacNativeKeyHelper", "MacNativeKeyHelper", Hotkey, "up");
    }

    public override string ToString() => DisplayName;
}

[PluginName("Mac Space Left")]
public sealed class MacSpaceLeft : MacNativeKeyBinding
{
    protected override string Hotkey => "LeftControl+Left";
    protected override string DisplayName => "Mac Space Left";
}

[PluginName("Mac Space Right")]
public sealed class MacSpaceRight : MacNativeKeyBinding
{
    protected override string Hotkey => "LeftControl+Right";
    protected override string DisplayName => "Mac Space Right";
}

[PluginName("Mac Mission Control")]
public sealed class MacMissionControl : MacNativeKeyBinding
{
    protected override string Hotkey => "LeftControl+Up";
    protected override string DisplayName => "Mac Mission Control";
}

[PluginName("Mac App Windows")]
public sealed class MacAppWindows : MacNativeKeyBinding
{
    protected override string Hotkey => "LeftControl+Down";
    protected override string DisplayName => "Mac App Windows";
}

[PluginName("Mac Native Hotkey")]
public sealed class MacNativeHotkey : IStateBinding
{
    private string _hotkey = "LeftControl+Right";

    [Property("Hotkey")]
    public string Hotkey
    {
        get => _hotkey;
        set => _hotkey = string.IsNullOrWhiteSpace(value) ? "LeftControl+Right" : value.Trim();
    }

    public void Press(TabletReference tablet, IDeviceReport report)
    {
        NativeHelper.Start("MacNativeKeyHelper", "MacNativeKeyHelper", _hotkey, "down");
    }

    public void Release(TabletReference tablet, IDeviceReport report)
    {
        NativeHelper.Start("MacNativeKeyHelper", "MacNativeKeyHelper", _hotkey, "up");
    }

    public override string ToString() => $"Mac Native Hotkey: {_hotkey}";
}

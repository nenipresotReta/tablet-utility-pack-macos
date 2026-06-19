using OpenTabletDriver.Plugin;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.DependencyInjection;
using OpenTabletDriver.Plugin.Platform.Keyboard;
using OpenTabletDriver.Plugin.Tablet;

namespace TabletUtilityPack;

[PluginName("Cycle Keys")]
public sealed class CycleKeys : IStateBinding
{
    private readonly object _sync = new();
    private string[] _keys = ["P", "E"];
    private string _keysText = "P+E";
    private string? _pressedKey;
    private int _nextKeyIndex;

    [Resolved]
    public IVirtualKeyboard Keyboard { get; set; } = null!;

    [Property("Keys")]
    public string Keys
    {
        get => _keysText;
        set
        {
            var parsed = ParseKeys(value);

            lock (_sync)
            {
                _keysText = string.Join("+", parsed);
                _keys = parsed;
                _pressedKey = null;
                _nextKeyIndex = 0;
            }
        }
    }

    public void Press(TabletReference tablet, IDeviceReport report)
    {
        string key;

        lock (_sync)
        {
            if (_pressedKey is not null || _keys.Length == 0)
                return;

            key = _keys[_nextKeyIndex];
            _pressedKey = key;
        }

        Keyboard.Press(key);
        Log.Debug(nameof(CycleKeys), $"Pressed {key}");
    }

    public void Release(TabletReference tablet, IDeviceReport report)
    {
        string? key;

        lock (_sync)
        {
            key = _pressedKey;
            if (key is null)
                return;

            _pressedKey = null;
            _nextKeyIndex = (_nextKeyIndex + 1) % _keys.Length;
        }

        Keyboard.Release(key);
        Log.Debug(nameof(CycleKeys), $"Released {key}");
    }

    public override string ToString() => $"Cycle Keys: {_keysText}";

    private static string[] ParseKeys(string? value)
    {
        var source = string.IsNullOrWhiteSpace(value) ? "P+E" : value;
        var keys = source
            .Split(['+', ',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .ToArray();

        if (keys.Length == 0)
            throw new ArgumentException("At least one key is required.", nameof(value));

        return keys;
    }
}

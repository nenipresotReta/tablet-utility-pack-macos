using System.Diagnostics;
using System.Reflection;
using OpenTabletDriver.Plugin;

namespace TabletUtilityPack;

internal static class NativeHelper
{
    public static void Start(string executableName, string logGroup, params string[] arguments)
    {
        var helperPath = Find(executableName);
        if (helperPath is null)
        {
            Log.Write(logGroup, $"{executableName} not found in plugin directories.", LogLevel.Error);
            return;
        }

        try
        {
            var startInfo = new ProcessStartInfo(helperPath)
            {
                UseShellExecute = false,
                CreateNoWindow = true
            };

            foreach (var argument in arguments)
                startInfo.ArgumentList.Add(argument);

            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            Log.Write(logGroup, $"Unable to start {executableName}: {ex.Message}", LogLevel.Error);
        }
    }

    private static string? Find(string executableName)
    {
        var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (!string.IsNullOrWhiteSpace(assemblyDir))
        {
            var sibling = Path.Combine(assemblyDir, executableName);
            if (File.Exists(sibling))
                return sibling;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var pluginRoot = Path.Combine(home, "Library", "Application Support", "OpenTabletDriver", "Plugins");
        if (!Directory.Exists(pluginRoot))
            return null;

        return Directory.EnumerateFiles(pluginRoot, executableName, SearchOption.AllDirectories)
            .FirstOrDefault();
    }
}

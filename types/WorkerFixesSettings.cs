using MelonLoader;

namespace WorkerFixes.Types;

public static class WorkerFixesSettings
{
    public static MelonPreferences_Entry<bool> Enabled { get; private set; }

    public static MelonPreferences_Entry<float> StandDistance { get; private set; }

    public static MelonPreferences_Entry<bool> DebugLogging { get; private set; }

    public static void Register()
    {
        var category = MelonPreferences.CreateCategory("WorkerFixes", "Worker Fixes");
        Enabled = category.CreateEntry(
            "Enabled",
            true,
            "Enabled",
            "Between tasks, workers stand beside an assigned station instead of outside.");
        StandDistance = category.CreateEntry(
            "StandDistance",
            0f,
            "Stand distance",
            "Meters to stand off the station access point.");
        DebugLogging = category.CreateEntry(
            "DebugLogging",
            true,
            "Debug logging",
            "Write idle-retarget decisions to the MelonLoader log.");
    }
}

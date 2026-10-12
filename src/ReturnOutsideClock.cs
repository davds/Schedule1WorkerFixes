using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.GameTime;
using WorkerFixes.Types;

namespace WorkerFixes;

internal static class ReturnOutsideClock
{
    public const int ReturnTime = 400;

    private const int FallbackWakeTime = 700;

    private static bool _loggedTimeFailure;

    public static bool IsInEffect()
    {
        return TryGetState(out var inWindow, out _) && inWindow;
    }

    public static bool TryGetState(out bool inWindow, out int day)
    {
        inWindow = false;
        day = -1;
        if (WorkerFixesSettings.ReturnOutsideAt4Am == null || !WorkerFixesSettings.ReturnOutsideAt4Am.Value)
            return false;
        if (WorkerFixesSettings.Enabled == null || !WorkerFixesSettings.Enabled.Value)
            return false;

        try
        {
            if (!NetworkSingleton<TimeManager>.InstanceExists)
                return false;

            var time = NetworkSingleton<TimeManager>.Instance;
            if (time == null)
                return false;

            day = time.ElapsedDays;
            var now = time.CurrentTime;
            var wake = ResolveWakeTime();
            inWindow = now >= ReturnTime && now < wake;
            return true;
        }
        catch (Exception exception)
        {
            if (!_loggedTimeFailure)
            {
                WorkerFixesLog.Error($"4:00 AM clock check failed: {exception}");
                _loggedTimeFailure = true;
            }

            return false;
        }
    }

    private static int ResolveWakeTime()
    {
        if (!NetworkSingleton<SleepController>.InstanceExists)
            return FallbackWakeTime;

        var wake = SleepController.WakeTime;
        if (wake > ReturnTime && wake <= 2359)
            return wake;

        return FallbackWakeTime;
    }
}

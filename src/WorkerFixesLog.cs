using MelonLoader;
using WorkerFixes.Types;

namespace WorkerFixes;

internal static class WorkerFixesLog
{
    public static void Info(string message)
    {
        if (WorkerFixesSettings.DebugLogging != null && !WorkerFixesSettings.DebugLogging.Value)
            return;

        MelonLogger.Msg(message);
    }

    public static void Error(string message)
    {
        MelonLogger.Error(message);
    }
}

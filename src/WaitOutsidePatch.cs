using HarmonyLib;
using Il2CppScheduleOne.Employees;

namespace WorkerFixes;

[HarmonyPatch(typeof(Employee), nameof(Employee.SetWaitOutside))]
internal static class WaitOutsidePatch
{
    [HarmonyPrefix]
    public static void Prefix(Employee __instance, bool wait)
    {
        if (wait)
            StationStandPoint.Apply(__instance, "prefix");
        else
            StationStandPoint.Restore(__instance, "prefix");
    }

    [HarmonyPostfix]
    public static void Postfix(Employee __instance, bool wait)
    {
        if (wait)
            StationStandPoint.Apply(__instance, "postfix");
        else
            StationStandPoint.Restore(__instance, "postfix");
    }
}

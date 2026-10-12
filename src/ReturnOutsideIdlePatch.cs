using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Il2CppScheduleOne.Employees;

namespace WorkerFixes;

[HarmonyPatch]
internal static class ReturnOutsideIdlePatch
{
    static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(Employee), "ShouldIdle");
        yield return AccessTools.Method(typeof(Botanist), "ShouldIdle");
        yield return AccessTools.Method(typeof(Chemist), "ShouldIdle");
        yield return AccessTools.Method(typeof(Packager), "ShouldIdle");
    }

    [HarmonyPostfix]
    static void Postfix(ref bool __result)
    {
        if (ReturnOutsideClock.IsInEffect())
            __result = true;
    }
}

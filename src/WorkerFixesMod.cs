using System.Linq;
using Il2CppFishNet;
using Il2CppScheduleOne.Employees;
using MelonLoader;
using UnityEngine;
using WorkerFixes.Types;

[assembly: MelonInfo(typeof(WorkerFixes.WorkerFixesMod), "Worker Fixes", "1.0.5", "davd")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace WorkerFixes;

public class WorkerFixesMod : MelonMod
{
    private bool _redirectedExistingIdle;
    private bool _loggedNotServer;
    private bool _loggedNoEmployees;
    private float _redirectAfter;

    public override void OnInitializeMelon()
    {
        WorkerFixesSettings.Register();
        IdleRouteStore.Load();
        if (!HarmonyInstance.GetPatchedMethods().Any())
            HarmonyInstance.PatchAll(typeof(WaitOutsidePatch));

        var patched = HarmonyInstance.GetPatchedMethods().ToList();
        LoggerInstance.Msg($"Workers will idle beside their assigned stations. Patched {patched.Count} method(s).");
        foreach (var method in patched)
            LoggerInstance.Msg($"Patched {method.DeclaringType?.FullName}.{method.Name}");
    }

    public override void OnSceneWasInitialized(int buildIndex, string sceneName)
    {
        _redirectedExistingIdle = false;
        _loggedNotServer = false;
        _loggedNoEmployees = false;
        _redirectAfter = 0f;
        WorkerFixesLog.Info($"Scene initialized: {sceneName} ({buildIndex}).");
    }

    public override void OnUpdate()
    {
        if (_redirectedExistingIdle || !WorkerFixesSettings.Enabled.Value)
            return;

        if (!InstanceFinder.IsServer)
        {
            if (!_loggedNotServer)
            {
                WorkerFixesLog.Info("Idle scan waiting: InstanceFinder.IsServer is false.");
                _loggedNotServer = true;
            }

            return;
        }

        if (_redirectAfter <= 0f)
            _redirectAfter = Time.time + 5f;
        if (Time.time < _redirectAfter)
            return;

        var employees = UnityEngine.Object.FindObjectsOfType<Employee>();
        if (employees == null || employees.Length == 0)
        {
            if (!_loggedNoEmployees)
            {
                WorkerFixesLog.Info("Idle scan: no employees in the scene yet.");
                _loggedNoEmployees = true;
            }

            return;
        }

        var waiting = 0;
        for (var i = 0; i < employees.Length; i++)
        {
            var employee = employees[i];
            if (employee == null || !employee.IsWaitingOutside)
                continue;

            waiting++;
            StationStandPoint.Apply(employee, "scan");
        }

        WorkerFixesLog.Info($"Idle scan: {employees.Length} employees, {waiting} already waiting outside.");
        _redirectedExistingIdle = true;
    }
}

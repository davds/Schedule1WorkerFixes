using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Employees;
using Il2CppScheduleOne.EntityFramework;
using Il2CppScheduleOne.Growing;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Management;
using Il2CppScheduleOne.ObjectScripts;
using UnityEngine;
using UnityEngine.AI;
using WorkerFixes.Types;

namespace WorkerFixes;

internal static class StationStandPoint
{
    private const string StandObjectName = "WorkerFixesStand";

    private static readonly Dictionary<int, Transform> Stands = new();
    private static readonly Dictionary<int, string> LastDecision = new();

    public static string Describe(Employee employee)
    {
        if (employee == null)
            return "employee=null";

        return $"{employee.name} index={employee.EmployeeIndex} server={employee.IsServer} canWork={SafeCanWork(employee)} waiting={employee.IsWaitingOutside}";
    }

    public static void Apply(Employee employee, string source)
    {
        try
        {
            ApplyCore(employee, source);
        }
        catch (Exception exception)
        {
            WorkerFixesLog.Error($"Apply ({source}) failed for {Describe(employee)}: {exception}");
        }
    }

    public static void Restore(Employee employee, string source)
    {
        try
        {
            RestoreCore(employee, source);
        }
        catch (Exception exception)
        {
            WorkerFixesLog.Error($"Restore ({source}) failed for {Describe(employee)}: {exception}");
        }
    }

    private static void ApplyCore(Employee employee, string source)
    {
        if (employee == null)
        {
            LogDecision(null, $"Apply ({source}) skipped: employee is null.");
            return;
        }

        if (!employee.IsServer)
        {
            LogDecision(employee, $"Apply skipped: not the server. {Describe(employee)}");
            return;
        }

        if (!WorkerFixesSettings.Enabled.Value)
        {
            LogDecision(employee, $"Apply skipped: mod disabled. {Describe(employee)}");
            RestoreCore(employee, source);
            return;
        }

        if (!employee.CanWork())
        {
            LogDecision(employee, $"Apply skipped: CanWork is false. {Describe(employee)}");
            RestoreCore(employee, source);
            return;
        }

        if (!TryGetStandPosition(employee, out var position, out var positionReason))
        {
            LogDecision(employee, $"Apply skipped: {positionReason} {Describe(employee)}");
            RestoreCore(employee, source);
            return;
        }

        var idle = employee.WaitOutside;
        if (idle == null)
        {
            LogDecision(employee, $"Apply skipped: WaitOutside is null. {Describe(employee)}");
            return;
        }

        if (!ShouldRetarget(employee, idle.IdlePoint, out var retargetReason))
        {
            LogDecision(employee, $"Apply skipped: {retargetReason} Current={PointName(idle.IdlePoint)} {Describe(employee)}");
            return;
        }

        var stand = GetOrCreateStand(employee);
        var moved = (stand.position - position).sqrMagnitude > 0.01f;
        stand.position = position;
        idle.IdlePoint = stand;

        var setDestination = employee.IsWaitingOutside && employee.Movement != null && (moved || !idle.IsAtIdleLocation());
        if (setDestination)
            employee.Movement.SetDestination(position);

        LogDecision(employee, $"Apply retargeted to {position.x:0.0}, {position.y:0.0}, {position.z:0.0}. {positionReason} {Describe(employee)}");
    }

    private static void LogDecision(Employee employee, string message)
    {
        var id = employee != null ? employee.GetInstanceID() : 0;
        if (LastDecision.TryGetValue(id, out var previous) && previous == message)
            return;

        LastDecision[id] = message;
        WorkerFixesLog.Info(message);
    }

    private static void RestoreCore(Employee employee, string source)
    {
        if (employee == null || !employee.IsServer)
            return;

        var idle = employee.WaitOutside;
        if (idle == null || !IsOurStand(employee, idle.IdlePoint))
            return;

        if (!TryGetDefaultPoint(employee, out var defaultPoint))
        {
            LogDecision(employee, $"Restore failed: no property idle point. {Describe(employee)}");
            return;
        }

        idle.IdlePoint = defaultPoint;
        LogDecision(employee, $"Restore returned to {PointName(defaultPoint)} {Describe(employee)}");
    }

    private static bool SafeCanWork(Employee employee)
    {
        try
        {
            return employee.CanWork();
        }
        catch
        {
            return false;
        }
    }

    private static string PointName(Transform point)
    {
        if (point == null)
            return "null";

        return $"{point.name} {point.position}";
    }

    private static bool ShouldRetarget(Employee employee, Transform current, out string reason)
    {
        if (current == null)
        {
            reason = "idle point is null.";
            return true;
        }

        if (IsOurStand(employee, current))
        {
            reason = "idle point is already the near-station stand.";
            return true;
        }

        if (!TryGetDefaultPoint(employee, out var defaultPoint))
        {
            reason = "could not resolve the property's outdoor idle point.";
            return false;
        }

        if (current == defaultPoint)
        {
            reason = "idle point is the property default.";
            return true;
        }

        reason = $"idle point {PointName(current)} is not the property default {PointName(defaultPoint)}.";
        return false;
    }

    private static bool IsOurStand(Employee employee, Transform point)
    {
        return point != null
            && Stands.TryGetValue(employee.GetInstanceID(), out var stand)
            && stand == point;
    }

    private static Transform GetOrCreateStand(Employee employee)
    {
        var id = employee.GetInstanceID();
        if (Stands.TryGetValue(id, out var existing) && existing != null)
            return existing;

        var standObject = new GameObject(StandObjectName);
        if (employee.AssignedProperty != null)
            standObject.transform.SetParent(employee.AssignedProperty.transform, false);

        Stands[id] = standObject.transform;
        return standObject.transform;
    }

    private static bool TryGetDefaultPoint(Employee employee, out Transform point)
    {
        point = null;
        var property = employee.AssignedProperty;
        var points = property != null ? property.EmployeeIdlePoints : null;
        var index = employee.EmployeeIndex;
        if (points == null || index < 0 || index >= points.Length)
            return false;

        point = points[index];
        return point != null;
    }

    private static bool TryGetStandPosition(Employee employee, out Vector3 position, out string reason)
    {
        position = default;
        if (TryGetClosestStation(employee, out var station, out reason))
        {
            return SampleBeside(
                employee,
                station.name,
                station.transform.position,
                station.TryCast<ITransitEntity>(),
                false,
                reason,
                out position,
                out reason);
        }

        var stationReason = reason;
        if (TryStandAtFirstRouteSource(employee, out position, out reason))
            return true;

        if (reason == null)
            reason = stationReason;
        return false;
    }

    private static bool TryStandAtFirstRouteSource(Employee employee, out Vector3 position, out string reason)
    {
        position = default;
        reason = null;
        var packager = employee.TryCast<Packager>();
        var routes = packager != null && packager.configuration != null && packager.configuration.Routes != null
            ? packager.configuration.Routes.Routes
            : null;
        if (routes == null || routes.Count == 0)
            return false;

        var route = IdleRouteStore.Resolve(packager, routes, out var matchedSaved);
        var source = route != null ? route.Source : null;
        if (source == null && routes[0] != null && routes[0].Source != null)
        {
            route = routes[0];
            source = route.Source;
            matchedSaved = false;
        }

        if (source == null)
        {
            reason = "handler has no stations and the idle route has no From.";
            return false;
        }

        var buildable = source.TryCast<BuildableItem>();
        var hasCenter = buildable != null || source.LinkOrigin != null;
        var center = buildable != null
            ? buildable.transform.position
            : source.LinkOrigin != null ? source.LinkOrigin.position : Vector3.zero;
        var selection = matchedSaved
            ? $"idle route From is {source.Name}."
            : $"first route From is {source.Name}.";
        return SampleBeside(employee, source.Name, center, source, !hasCenter, selection, out position, out reason);
    }

    private static bool SampleBeside(
        Employee employee,
        string label,
        Vector3 center,
        ITransitEntity transit,
        bool collapseCenterWithoutAccess,
        string selection,
        out Vector3 position,
        out string reason)
    {
        position = default;
        var anchor = center;
        if (transit != null)
        {
            Transform access = null;
            try
            {
                access = NavMeshUtility.GetReachableAccessPoint(transit, employee);
            }
            catch (Exception exception)
            {
                access = null;
                WorkerFixesLog.Info($"GetReachableAccessPoint threw for {label}: {exception.GetType().Name}: {exception.Message}");
            }

            if (access == null && transit.AccessPoints != null && transit.AccessPoints.Length > 0)
                access = transit.AccessPoints[0];
            if (access != null)
                anchor = access.position;
        }

        if (collapseCenterWithoutAccess)
        {
            if ((anchor - center).sqrMagnitude < 0.04f)
            {
                reason = $"first route From {label} has no access point.";
                return false;
            }

            center = anchor;
        }

        var away = anchor - center;
        away.y = 0f;
        if (away.sqrMagnitude < 0.04f)
        {
            var angle = employee.EmployeeIndex * 47f * Mathf.Deg2Rad;
            away = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        }
        else
        {
            away.Normalize();
            var spread = ((employee.EmployeeIndex % 5) - 2) * 18f;
            away = Quaternion.Euler(0f, spread, 0f) * away;
        }

        var distance = Mathf.Clamp(WorkerFixesSettings.StandDistance.Value, 0f, 4f);
        var desired = anchor + (away * distance);
        var hit = new NavMeshHit();
        var sampledDesired = NavMeshUtility.SamplePosition(desired, out hit, 2.5f, NavMesh.AllAreas, false);
        var sampledAnchor = !sampledDesired && NavMeshUtility.SamplePosition(anchor, out hit, 2.5f, NavMesh.AllAreas, false);
        if (!sampledDesired && !sampledAnchor)
        {
            reason = $"navmesh sample failed near {label}. desired={desired} anchor={anchor} transit={transit != null}.";
            return false;
        }

        position = hit.position;
        reason = $"{selection} Standing at {position}.";
        return true;
    }

    private static bool TryGetClosestStation(Employee employee, out BuildableItem closest, out string reason)
    {
        closest = null;
        var items = new List<BuildableItem>();
        var seen = new HashSet<int>();
        var detail = CollectStations(employee, items, seen);

        if (items.Count == 0)
        {
            reason = $"no assigned stations. {detail}";
            return false;
        }

        var from = employee.transform.position;
        var scoreByTime = employee.TryCast<Botanist>() != null || employee.TryCast<Chemist>() != null;
        var closestDistance = float.MaxValue;
        BuildableItem soonest = null;
        var soonestMinutes = float.MaxValue;
        var soonestDistance = float.MaxValue;

        foreach (var item in items)
        {
            if (item == null)
                continue;

            var distance = (item.transform.position - from).sqrMagnitude;
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = item;
            }

            if (!scoreByTime || !TryGetRemainingMinutes(item, out var minutes))
                continue;

            var sooner = minutes < soonestMinutes - 0.05f;
            var tie = Mathf.Abs(minutes - soonestMinutes) <= 0.05f && distance < soonestDistance;
            if (soonest == null || sooner || tie)
            {
                soonest = item;
                soonestMinutes = minutes;
                soonestDistance = distance;
            }
        }

        if (soonest != null)
        {
            closest = soonest;
            reason = $"soonest station is {soonest.name} in {soonestMinutes:0.#} min ({items.Count} assigned). {detail}";
            return true;
        }

        if (closest == null)
        {
            reason = $"{items.Count} assigned entries were null.";
            return false;
        }

        reason = $"closest station is {closest.name} ({items.Count} assigned). {detail}";
        return true;
    }

    private static bool TryGetRemainingMinutes(BuildableItem item, out float minutes)
    {
        minutes = 0f;
        var pot = item.TryCast<Pot>();
        if (pot != null)
            return TryGetPotMinutes(pot, out minutes);

        var bed = item.TryCast<MushroomBed>();
        if (bed != null)
            return TryGetMushroomMinutes(bed, out minutes);

        var rack = item.TryCast<DryingRack>();
        if (rack != null)
            return TryGetDryingMinutes(rack, out minutes);

        var oven = item.TryCast<LabOven>();
        if (oven != null)
            return TryGetOvenMinutes(oven, out minutes);

        var cauldron = item.TryCast<Cauldron>();
        if (cauldron != null)
            return TryGetCauldronMinutes(cauldron, out minutes);

        var mixer = item.TryCast<MixingStation>();
        if (mixer != null)
            return TryGetMixingMinutes(mixer, out minutes);

        var chemistry = item.TryCast<ChemistryStation>();
        if (chemistry != null)
            return TryGetChemistryMinutes(chemistry, out minutes);

        return false;
    }

    private static bool TryGetPotMinutes(Pot pot, out float minutes)
    {
        minutes = 0f;
        var plant = pot.Plant;
        if (plant == null)
            return false;

        if (plant.IsFullyGrown || plant.NormalizedGrowthProgress >= 1f)
            return true;

        var progress = Mathf.Clamp01(plant.NormalizedGrowthProgress);
        var speed = Mathf.Max(pot.GrowSpeedMultiplier, 0.05f);
        minutes = plant.GrowthTime * (1f - progress) / speed;
        return true;
    }

    private static bool TryGetMushroomMinutes(MushroomBed bed, out float minutes)
    {
        minutes = 0f;
        var colony = bed.CurrentColony;
        if (colony == null)
            return false;

        if (colony.IsFullyGrown)
            return true;

        var progress = colony.GrowthProgress;
        if (progress > 1f)
            progress /= 100f;
        progress = Mathf.Clamp01(progress);
        if (progress >= 1f)
            return true;

        var growTime = Mathf.Max(colony._growTime, 1);
        minutes = growTime * (1f - progress);
        return true;
    }

    private static bool TryGetDryingMinutes(DryingRack rack, out float minutes)
    {
        minutes = 0f;
        var operations = rack.DryingOperations;
        var configuration = rack.stationConfiguration;
        if (operations == null || operations.Count == 0 || configuration == null || configuration.TargetQuality == null)
            return false;

        var multiplier = rack.GetDryMultiplier();
        if (multiplier <= 0.01f)
            multiplier = 1f;

        var minutesPerTier = DryingRack.DRY_MINS_PER_TIER / multiplier;
        if (minutesPerTier <= 0f)
            return false;

        var target = (int)configuration.TargetQuality.Value;
        var best = float.MaxValue;
        var found = false;
        for (var i = 0; i < operations.Count; i++)
        {
            var operation = operations[i];
            if (operation == null)
                continue;

            found = true;
            var remaining = RemainingDryingMinutes(operation, target, minutesPerTier);
            if (remaining < best)
                best = remaining;
        }

        if (!found)
            return false;

        minutes = best;
        return true;
    }

    private static float RemainingDryingMinutes(DryingOperation operation, int targetQuality, float minutesPerTier)
    {
        var currentQuality = (int)operation.GetQuality();
        var tiersLeft = targetQuality - currentQuality;
        if (tiersLeft <= 0)
            return 0f;

        var elapsed = Mathf.Max(0f, operation.Time);
        var elapsedInTier = elapsed % minutesPerTier;
        return Mathf.Max(0f, ((tiersLeft - 1) * minutesPerTier) + (minutesPerTier - elapsedInTier));
    }

    private static bool TryGetOvenMinutes(LabOven oven, out float minutes)
    {
        minutes = 0f;
        var operation = oven.CurrentOperation;
        if (operation == null)
            return false;

        minutes = Mathf.Max(0f, operation.cookDuration - operation.CookProgress);
        return true;
    }

    private static bool TryGetCauldronMinutes(Cauldron cauldron, out float minutes)
    {
        minutes = 0f;
        if (!cauldron.isCooking)
            return false;

        minutes = Mathf.Max(0f, cauldron.RemainingCookTime);
        return true;
    }

    private static bool TryGetMixingMinutes(MixingStation mixer, out float minutes)
    {
        minutes = 0f;
        var operation = mixer.CurrentMixOperation;
        if (operation == null)
            return false;

        if (mixer.IsMixingDone)
            return true;

        minutes = Mathf.Max(0f, (mixer.MixTimePerItem * operation.Quantity) - mixer.CurrentMixTime);
        return true;
    }

    private static bool TryGetChemistryMinutes(ChemistryStation station, out float minutes)
    {
        minutes = 0f;
        var operation = station.CurrentCookOperation;
        var recipe = operation != null ? operation.Recipe : null;
        if (operation == null || recipe == null)
            return false;

        minutes = Mathf.Max(0f, recipe.CookTime_Mins - operation.CurrentTime);
        return true;
    }

    private static string CollectStations(Employee employee, List<BuildableItem> items, HashSet<int> seen)
    {
        var botanist = employee.TryCast<Botanist>();
        var chemist = employee.TryCast<Chemist>();
        var packager = employee.TryCast<Packager>();
        var cleaner = employee.TryCast<Cleaner>();
        var counts = new List<string>();

        if (botanist != null)
        {
            var config = botanist.configuration;
            counts.Add(config == null ? "botanist config=null" : "botanist");
            if (config != null)
            {
                AddSelected(config.Assigns, items, seen);
                AddItems(config.AssignedPots, items, seen);
                AddItems(config.AssignedRacks, items, seen);
                AddItems(config.AssignedBeds, items, seen);
                AddItems(config.AssignedSpawnStations, items, seen);
            }
        }

        if (chemist != null)
        {
            var config = chemist.configuration;
            counts.Add(config == null ? "chemist config=null" : "chemist");
            if (config != null)
            {
                AddSelected(config.Stations, items, seen);
                AddItems(config.ChemStations, items, seen);
                AddItems(config.LabOvens, items, seen);
                AddItems(config.Cauldrons, items, seen);
                AddItems(config.MixStations, items, seen);
            }
        }

        if (packager != null)
        {
            var config = packager.configuration;
            counts.Add(config == null ? "handler config=null" : "handler");
            if (config != null)
            {
                AddSelected(config.Stations, items, seen);
                AddItems(config.AssignedStations, items, seen);
                AddItems(config.AssignedBrickPresses, items, seen);
            }
        }

        if (cleaner != null)
        {
            var config = cleaner.configuration;
            counts.Add(config == null ? "cleaner config=null" : "cleaner");
            if (config != null)
            {
                AddSelected(config.Bins, items, seen);
                AddItems(config.binItems, items, seen);
            }
        }

        if (counts.Count == 0)
            counts.Add("role cast failed");

        counts.Add($"found={items.Count}");
        return string.Join(", ", counts);
    }

    private static void AddSelected(ObjectListField field, List<BuildableItem> items, HashSet<int> seen)
    {
        var selected = field != null ? field.SelectedObjects : null;
        if (selected == null)
            return;

        for (var i = 0; i < selected.Count; i++)
            AddItem(selected[i], items, seen);
    }

    private static void AddItems<T>(Il2CppSystem.Collections.Generic.List<T> list, List<BuildableItem> items, HashSet<int> seen)
        where T : BuildableItem
    {
        if (list == null)
            return;

        for (var i = 0; i < list.Count; i++)
            AddItem(list[i], items, seen);
    }

    private static void AddItem(BuildableItem item, List<BuildableItem> items, HashSet<int> seen)
    {
        if (item == null || !seen.Add(item.GetInstanceID()))
            return;

        items.Add(item);
    }
}

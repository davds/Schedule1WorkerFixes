using System.Collections.Generic;
using System.IO;
using Il2CppScheduleOne.Employees;
using Il2CppScheduleOne.Management;
using MelonLoader.Utils;
using WorkerFixes.Types;

namespace WorkerFixes;

internal static class IdleRouteStore
{
    private static readonly Dictionary<string, IdleRouteChoice> Choices = new();
    private static bool _loaded;

    public static void Load()
    {
        Choices.Clear();
        _loaded = true;
        var path = FilePath();
        if (!File.Exists(path))
            return;

        foreach (var line in File.ReadAllLines(path))
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                continue;

            var split = line.IndexOf('=');
            if (split <= 0)
                continue;

            var handlerId = line.Substring(0, split).Trim();
            var routeKey = line.Substring(split + 1).Trim();
            var bar = routeKey.IndexOf('|');
            var choice = new IdleRouteChoice
            {
                HandlerId = handlerId,
                SourceId = bar < 0 ? routeKey : routeKey.Substring(0, bar),
                DestinationId = bar < 0 ? "" : routeKey.Substring(bar + 1),
            };
            if (handlerId.Length > 0)
                Choices[handlerId] = choice;
        }
    }

    public static string HandlerId(Packager packager)
    {
        if (packager == null)
            return null;

        var id = packager.GUID.ToString();
        if (string.IsNullOrEmpty(id) || id == "00000000-0000-0000-0000-000000000000")
            return null;

        return id;
    }

    public static string RouteKey(AdvancedTransitRoute route)
    {
        if (route == null)
            return null;

        return EntityId(route.Source) + "|" + EntityId(route.Destination);
    }

    public static AdvancedTransitRoute Resolve(
        Packager packager,
        Il2CppSystem.Collections.Generic.List<AdvancedTransitRoute> routes,
        out bool matchedSaved)
    {
        matchedSaved = false;
        if (routes == null || routes.Count == 0)
            return null;

        EnsureLoaded();
        var handlerId = HandlerId(packager);
        if (handlerId != null && Choices.TryGetValue(handlerId, out var choice))
        {
            var anyIdentified = false;
            for (var i = 0; i < routes.Count; i++)
            {
                var route = routes[i];
                if (route == null)
                    continue;

                if (HasIdentity(route))
                    anyIdentified = true;
                if (RouteKey(route) == choice.RouteKey)
                {
                    matchedSaved = true;
                    return route;
                }
            }

            if (anyIdentified)
            {
                Choices.Remove(handlerId);
                Save();
            }
        }

        return routes[0];
    }

    public static void Select(Packager packager, AdvancedTransitRoute route)
    {
        var handlerId = HandlerId(packager);
        var routeKey = RouteKey(route);
        if (handlerId == null || routeKey == null)
            return;

        EnsureLoaded();
        var bar = routeKey.IndexOf('|');
        Choices[handlerId] = new IdleRouteChoice
        {
            HandlerId = handlerId,
            SourceId = bar < 0 ? routeKey : routeKey.Substring(0, bar),
            DestinationId = bar < 0 ? "" : routeKey.Substring(bar + 1),
        };
        Save();
    }

    public static void Clear(Packager packager)
    {
        var handlerId = HandlerId(packager);
        if (handlerId == null)
            return;

        EnsureLoaded();
        if (!Choices.Remove(handlerId))
            return;

        Save();
    }

    public static bool IsExplicit(Packager packager, AdvancedTransitRoute route)
    {
        EnsureLoaded();
        var handlerId = HandlerId(packager);
        if (handlerId == null || !Choices.TryGetValue(handlerId, out var choice))
            return false;

        return RouteKey(route) == choice.RouteKey;
    }

    private static void EnsureLoaded()
    {
        if (!_loaded)
            Load();
    }

    private static string EntityId(ITransitEntity entity)
    {
        if (entity == null)
            return "";

        try
        {
            return entity.GUID.ToString() ?? "";
        }
        catch (Exception)
        {
            return "";
        }
    }

    private static bool HasIdentity(AdvancedTransitRoute route)
    {
        var key = RouteKey(route);
        return !string.IsNullOrEmpty(key)
            && key != "|"
            && key.IndexOf("00000000-0000-0000-0000-000000000000", StringComparison.Ordinal) < 0;
    }

    private static void Save()
    {
        var path = FilePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var lines = new List<string>
        {
            "# handlerGuid=sourceGuid|destinationGuid",
        };
        foreach (var choice in Choices.Values)
            lines.Add(choice.HandlerId + "=" + choice.RouteKey);

        File.WriteAllLines(path, lines);
    }

    private static string FilePath()
    {
        return Path.Combine(MelonEnvironment.UserDataDirectory, "WorkerFixes", "IdleRoutes.txt");
    }
}

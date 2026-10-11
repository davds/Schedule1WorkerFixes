using HarmonyLib;
using Il2CppScheduleOne.Employees;
using Il2CppScheduleOne.Management;
using Il2CppScheduleOne.UI.Management;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace WorkerFixes;

[HarmonyPatch]
internal static class RouteIdleButton
{
    private const string ButtonName = "WorkerFixesIdle";

    private static readonly Color SelectedColor = new(0.18f, 0.62f, 0.32f, 1f);
    private static readonly Color IdleColor = new(0.16f, 0.16f, 0.16f, 0.9f);

    [HarmonyPatch(typeof(RouteEntryUI), nameof(RouteEntryUI.RefreshUI))]
    [HarmonyPostfix]
    public static void AfterRefresh(RouteEntryUI __instance)
    {
        try
        {
            Sync(__instance);
        }
        catch (Exception exception)
        {
            WorkerFixesLog.Error($"Idle route button refresh failed: {exception}");
        }
    }

    [HarmonyPatch(typeof(RouteEntryUI), nameof(RouteEntryUI.ClearRoute))]
    [HarmonyPostfix]
    public static void AfterClear(RouteEntryUI __instance)
    {
        try
        {
            var button = FindButton(__instance);
            if (button != null)
                button.gameObject.SetActive(false);
        }
        catch (Exception exception)
        {
            WorkerFixesLog.Error($"Idle route button clear failed: {exception}");
        }
    }

    private static void Sync(RouteEntryUI entry)
    {
        if (entry == null)
            return;

        if (!TryGetSingleHandler(entry, out var packager, out var routes))
        {
            var hidden = FindButton(entry);
            if (hidden != null)
                hidden.gameObject.SetActive(false);
            return;
        }

        var route = entry.AssignedRoute;
        if (route == null)
        {
            var unused = FindButton(entry);
            if (unused != null)
                unused.gameObject.SetActive(false);
            return;
        }

        var button = EnsureButton(entry);
        button.gameObject.SetActive(true);
        var effective = IdleRouteStore.Resolve(packager, routes, out _);
        var image = button.GetComponent<Image>();
        if (image != null)
            image.color = SameRoute(route, effective) ? SelectedColor : IdleColor;
    }

    private static void OnClicked(Button button)
    {
        var entry = button != null ? button.GetComponentInParent<RouteEntryUI>() : null;
        if (entry == null || entry.AssignedRoute == null)
            return;

        if (!TryGetSingleHandler(entry, out var packager, out var routes))
            return;

        var route = entry.AssignedRoute;
        if (IdleRouteStore.IsExplicit(packager, route))
        {
            IdleRouteStore.Clear(packager);
            WorkerFixesLog.Info($"Idle route cleared for {packager.FullName}. Using the first route.");
        }
        else
        {
            var effective = IdleRouteStore.Resolve(packager, routes, out _);
            if (SameRoute(route, effective))
                return;

            IdleRouteStore.Select(packager, route);
            var from = route.Source != null ? route.Source.Name : "unset";
            WorkerFixesLog.Info($"Idle route for {packager.FullName} is From {from}.");
        }

        RefreshRows(entry);
        if (packager.IsWaitingOutside)
            StationStandPoint.Apply(packager, "route");
    }

    private static void RefreshRows(RouteEntryUI entry)
    {
        var list = entry.GetComponentInParent<RouteListFieldUI>();
        var rows = list != null ? list.RouteEntries : null;
        if (rows == null)
        {
            Sync(entry);
            return;
        }

        for (var i = 0; i < rows.Length; i++)
        {
            if (rows[i] != null)
                Sync(rows[i]);
        }
    }

    private static bool TryGetSingleHandler(
        RouteEntryUI entry,
        out Packager packager,
        out Il2CppSystem.Collections.Generic.List<AdvancedTransitRoute> routes)
    {
        packager = null;
        routes = null;
        var list = entry.GetComponentInParent<RouteListFieldUI>();
        if (list == null || list.Fields == null || list.Fields.Count != 1)
            return false;

        var field = list.Fields[0];
        var config = field != null && field.ParentConfig != null
            ? field.ParentConfig.TryCast<PackagerConfiguration>()
            : null;
        packager = config != null ? config.packager : null;
        routes = field != null ? field.Routes : null;
        return packager != null && routes != null;
    }

    private static bool SameRoute(AdvancedTransitRoute left, AdvancedTransitRoute right)
    {
        if (left == null || right == null)
            return false;

        return left.Pointer == right.Pointer || IdleRouteStore.RouteKey(left) == IdleRouteStore.RouteKey(right);
    }

    private static Button FindButton(RouteEntryUI entry)
    {
        var child = entry.transform.Find(ButtonName);
        return child != null ? child.GetComponent<Button>() : null;
    }

    private static Button EnsureButton(RouteEntryUI entry)
    {
        var existing = FindButton(entry);
        if (existing != null)
            return existing;

        var go = new GameObject(ButtonName);
        go.AddComponent<RectTransform>();
        go.transform.SetParent(entry.transform, false);
        var rightmost = FindLastButton(entry);
        if (rightmost != null)
            go.transform.SetSiblingIndex(rightmost.transform.GetSiblingIndex());
        else
            go.transform.SetAsLastSibling();

        var rect = go.GetComponent<RectTransform>();
        PlaceLeftOf(rect, rightmost);

        var layout = go.AddComponent<LayoutElement>();
        layout.preferredWidth = 56f;
        layout.preferredHeight = 24f;
        layout.flexibleWidth = 0f;
        layout.minWidth = 56f;

        var image = go.AddComponent<Image>();
        image.color = IdleColor;
        image.raycastTarget = true;

        var button = go.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener((UnityAction)(() => OnClicked(button)));

        var labelObject = new GameObject("Label");
        labelObject.AddComponent<RectTransform>();
        labelObject.transform.SetParent(go.transform, false);
        var labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        var label = labelObject.AddComponent<TextMeshProUGUI>();
        var sample = entry.SourceLabel != null ? entry.SourceLabel : entry.DestinationLabel;
        if (sample != null)
        {
            label.font = sample.font;
            label.fontSharedMaterial = sample.fontSharedMaterial;
        }

        label.text = "Idle";
        label.fontSize = 12f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.enableWordWrapping = false;
        label.raycastTarget = false;
        return button;
    }

    private static RectTransform FindLastButton(RouteEntryUI entry)
    {
        RectTransform last = null;
        for (var i = 0; i < entry.transform.childCount; i++)
        {
            var child = entry.transform.GetChild(i);
            if (child.name == ButtonName || child.GetComponent<Button>() == null)
                continue;

            var childRect = child.GetComponent<RectTransform>();
            if (childRect != null)
                last = childRect;
        }

        return last;
    }

    private static void PlaceLeftOf(RectTransform rect, RectTransform neighbor)
    {
        const float width = 56f;
        if (neighbor == null)
        {
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(width, -6f);
            rect.anchoredPosition = new Vector2(-4f, 0f);
            return;
        }

        rect.anchorMin = neighbor.anchorMin;
        rect.anchorMax = neighbor.anchorMax;
        rect.pivot = neighbor.pivot;
        rect.sizeDelta = new Vector2(width, neighbor.sizeDelta.y);
        rect.anchoredPosition = neighbor.anchoredPosition - new Vector2(width + 4f, 0f);
    }
}

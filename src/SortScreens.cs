using System;
using System.Collections.Generic;
using Common.UI.Unity;
using HarmonyLib;
using Strategy.UI.Elements.Soldiers;
using TMPro;
using UnityEngine;
using Xenonauts.Common.Util.Comparers;

namespace MultiColumnSort;

// The soldier lists this mod knows: their header toggles, their comparer and how to re-sort them.
internal static class SortScreens
{
    private sealed class Screen
    {
        public string Label;
        public WeakReference<object> Element;
        public Dictionary<SortField, GHIToggle> Toggles;
        public StrategyActorComparer Comparer;
        public string RowsField;
    }

    private static readonly List<Screen> Screens = new List<Screen>();

    // The marker each header currently carries, so it can be taken off again.
    private static readonly Dictionary<TMP_Text, string> Markers = new Dictionary<TMP_Text, string>();

    public static void Register(object element, Dictionary<SortField, GHIToggle> toggles, string rowsField, string label)
    {
        try
        {
            var comparer = Traverse.Create(element).Field("_comparer").GetValue<StrategyActorComparer>();
            if (comparer == null || toggles == null)
            {
                ModLog.Warn($"{label}: comparer or header toggles not found; multi-sort is off for this screen");
                return;
            }
            Screens.RemoveAll(s => !s.Element.TryGetTarget(out _) || s.Label == label);
            Screens.Add(new Screen
            {
                Label = label,
                Element = new WeakReference<object>(element),
                Toggles = toggles,
                Comparer = comparer,
                RowsField = rowsField,
            });
            ModLog.Info($"{label}: registered");
        }
        catch (Exception e)
        {
            ModLog.Warn($"{label}: could not register: {e.Message}");
        }
    }

    // Called when a screen opens. hardReset: the game puts this screen back on its default sort.
    public static void Entered(object element, bool hardReset)
    {
        var screen = Screens.Find(s => s.Element.TryGetTarget(out var e) && ReferenceEquals(e, element));
        if (screen == null)
            return;
        if (hardReset || SortLevels.Active(screen.Comparer) == null)
            SortLevels.Clear(screen.Comparer);
        Refresh(screen);
    }

    // Returns false when the click was handled here and the game must not see it.
    public static bool HeaderClicked(GHIToggle toggle)
    {
        Screen screen = null;
        var field = SortField.Default;
        foreach (var s in Screens)
        {
            foreach (var pair in s.Toggles)
            {
                if (pair.Value == toggle)
                {
                    screen = s;
                    field = pair.Key;
                    break;
                }
            }
            if (screen != null)
                break;
        }
        if (screen == null || !toggle.IsInteractable())
            return true;

        var comparer = screen.Comparer;
        if (!ModifierHeld())
        {
            // A normal click is the game's single-column sort.
            SortLevels.Clear(comparer);
            Refresh(screen);
            return true;
        }
        // The first-level column itself, or a first level this mod cannot break ties on: let the game handle it.
        if (field == comparer.sortField || !SortKeys.Supports(comparer.sortField) || !SortKeys.Supports(field))
        {
            ModLog.Info($"{screen.Label}: modifier-click on {field} passed to the game (first level is {comparer.sortField})");
            return true;
        }
        if (!screen.Element.TryGetTarget(out var element))
            return true;

        var levels = SortLevels.For(comparer);
        if (levels.Anchor != comparer.sortField)
        {
            levels.Levels.Clear();
            levels.Anchor = comparer.sortField;
        }
        levels.Cycle(field);
        try
        {
            Traverse.Create(element).Field(screen.RowsField).Method("Sort").GetValue();
        }
        catch (Exception e)
        {
            ModLog.Warn($"{screen.Label}: re-sort failed: {e.Message}");
        }
        Refresh(screen);
        ModLog.Info($"{screen.Label}: sort is {comparer.sortField} ({comparer.sortOrder}) then {Describe(levels)}");
        return false;
    }

    private static bool ModifierHeld() =>
        Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)
        || Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand)
        || Input.GetKey(KeyCode.LeftWindows) || Input.GetKey(KeyCode.RightWindows)
        || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

    private static string Describe(SortLevels levels)
    {
        if (levels.Levels.Count == 0)
            return "nothing";
        var parts = new List<string>();
        foreach (var level in levels.Levels)
            parts.Add($"{level.Field} ({level.Order})");
        return string.Join(", ", parts);
    }

    // Marks each extra level's header with its position (2, 3, ...) and direction.
    private static void Refresh(Screen screen)
    {
        try
        {
            var levels = SortLevels.Active(screen.Comparer);
            foreach (var pair in screen.Toggles)
            {
                if (pair.Value == null)
                    continue;
                var text = pair.Value.GetComponentInChildren<TMP_Text>(true);
                if (text == null)
                    continue;
                string marker = "";
                int index = levels == null ? -1 : levels.Levels.FindIndex(l => l.Field == pair.Key);
                if (index >= 0)
                    marker = $"<sup>{index + 2}{Arrow(text, levels.Levels[index].Order)}</sup>";

                string current = text.text ?? "";
                if (Markers.TryGetValue(text, out var old) && old.Length > 0 && current.EndsWith(old))
                    current = current.Substring(0, current.Length - old.Length);
                if (marker.Length > 0)
                {
                    text.richText = true;
                    Markers[text] = marker;
                }
                else
                {
                    Markers.Remove(text);
                }
                text.text = current + marker;
            }
        }
        catch (Exception e)
        {
            ModLog.Warn($"{screen.Label}: header markers failed: {e.Message}");
        }
    }

    // Arrows when the header font has them, plain characters otherwise.
    private static string Arrow(TMP_Text text, int order)
    {
        char arrow = order < 0 ? '▼' : '▲';
        return text.font != null && text.font.HasCharacter(arrow, true) ? arrow.ToString() : order < 0 ? "-" : "+";
    }
}

using System;
using Artitas;
using Common.UI.Unity;
using HarmonyLib;
using Strategy.UI.Elements.Soldiers;
using UnityEngine.EventSystems;
using Xenonauts.Common.Util.Comparers;
using Xenonauts.Strategy.UI;

namespace MultiColumnSort;

// --- Screen registration: one "created" and one "opened" patch per soldier list ---

[HarmonyPatch(typeof(SoldiersManageElement), nameof(SoldiersManageElement.OnCreate))]
public static class ManageCreatePatch
{
    [HarmonyPostfix]
    public static void Postfix(SoldiersManageElement __instance) =>
        SortScreens.Register(__instance, __instance.columnToggles, "_soldierControllers", "manage soldiers");
}

[HarmonyPatch(typeof(SoldiersManageElement), nameof(SoldiersManageElement.OnEnter))]
public static class ManageEnterPatch
{
    [HarmonyPostfix]
    public static void Postfix(SoldiersManageElement __instance) => SortScreens.Entered(__instance, false);
}

[HarmonyPatch(typeof(SoldiersRecruitElement), nameof(SoldiersRecruitElement.OnCreate))]
public static class RecruitCreatePatch
{
    [HarmonyPostfix]
    public static void Postfix(SoldiersRecruitElement __instance) =>
        SortScreens.Register(__instance, __instance.columnToggles, "_soldierControllers", "recruit soldiers");
}

[HarmonyPatch(typeof(SoldiersRecruitElement), nameof(SoldiersRecruitElement.OnEnter))]
public static class RecruitEnterPatch
{
    [HarmonyPostfix]
    public static void Postfix(SoldiersRecruitElement __instance) => SortScreens.Entered(__instance, false);
}

[HarmonyPatch(typeof(SoldiersMemorialElement), nameof(SoldiersMemorialElement.OnCreate))]
public static class MemorialCreatePatch
{
    [HarmonyPostfix]
    public static void Postfix(SoldiersMemorialElement __instance) =>
        SortScreens.Register(__instance, __instance.columnToggles, "_rowControllers", "memorial");
}

// The memorial and the picker always open on their default sort, so extra levels do not carry over.
[HarmonyPatch(typeof(SoldiersMemorialElement), nameof(SoldiersMemorialElement.OnEnter))]
public static class MemorialEnterPatch
{
    [HarmonyPrefix]
    public static void Prefix(SoldiersMemorialElement __instance) => SortScreens.Entered(__instance, true);
}

[HarmonyPatch(typeof(SoldierSelectionElement), nameof(SoldierSelectionElement.OnCreate))]
public static class SelectionCreatePatch
{
    [HarmonyPostfix]
    public static void Postfix(SoldierSelectionElement __instance) =>
        SortScreens.Register(__instance, __instance.columnToggles, "_controllers", "soldier picker");
}

[HarmonyPatch(typeof(SoldierSelectionElement), nameof(SoldierSelectionElement.OnEnter))]
public static class SelectionEnterPatch
{
    [HarmonyPrefix]
    public static void Prefix(SoldierSelectionElement __instance) => SortScreens.Entered(__instance, true);
}

// --- Header clicks ---

// A click on a sort header with Shift held changes the extra levels and never reaches
// the game, so the first-level column and its highlighted header stay as they are.
[HarmonyPatch(typeof(GHIToggle), nameof(GHIToggle.OnPointerClick))]
public static class HeaderClickPatch
{
    [HarmonyPrefix]
    public static bool Prefix(GHIToggle __instance, PointerEventData eventData)
    {
        try
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left)
                return true;
            return SortScreens.HeaderClicked(__instance);
        }
        catch (Exception e)
        {
            ModLog.Warn("header click failed: " + e.Message);
            return true;
        }
    }
}

// --- Comparison ---

// When two soldiers are equal on the first-level column, the extra levels decide. Everything else,
// including the first level itself and the final name tie-break, is left to the game. Runs before
// Soldier Total Stats' own patch so a list sorted by TOT can have extra levels too.
[HarmonyPatch(typeof(StrategyActorComparer), nameof(StrategyActorComparer.Compare))]
public static class ComparePatch
{
    private static bool _failed;

    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    public static bool Prefix(StrategyActorComparer __instance, Entity x, Entity y, ref int __result)
    {
        var levels = SortLevels.Active(__instance);
        if (levels == null || x is null || y is null || Equals(x, y))
            return true;
        try
        {
            if (SortKeys.Compare(__instance.sortField, x, y) != 0)
                return true;
            foreach (var level in levels.Levels)
            {
                int? result = SortKeys.Compare(level.Field, x, y);
                if (result.HasValue && result.Value != 0)
                {
                    __result = level.Order * Math.Sign(result.Value);
                    return false;
                }
            }
        }
        catch (Exception e)
        {
            if (!_failed)
            {
                _failed = true;
                ModLog.Warn("comparison failed, extra sort levels are off for this list: " + e);
            }
            SortLevels.Clear(__instance);
        }
        return true;
    }
}

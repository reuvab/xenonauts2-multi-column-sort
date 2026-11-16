using System;
using System.Collections.Generic;
using Artitas;
using Artitas.Utils;
using Common.Util;
using Common.Utilities.Comparers;
using HarmonyLib;
using Strategy.UI.Elements.Soldiers;
using UnityEngine;
using Xenonauts.Common.Components.Combatant;
using Xenonauts.Common.Systems;
using Xenonauts.Common.Util;
using Xenonauts.Common.Util.Comparers;
using Xenonauts.Strategy.Factories;

namespace MultiColumnSort;

// Compares two soldiers on one column, without direction and without the game's name tie-break.
// The game's own comparer cannot say "these two are equal on this column" (it always falls back
// to the name), so each column's key is read here the same way the game reads it.
internal static class SortKeys
{
    // The TOT column of the Soldier Total Stats mod, when that mod is installed.
    private const SortField TotalSortField = (SortField)1000;

    private static bool _totalLookedUp;
    private static Func<Entity, int> _total;

    public static bool Supports(SortField field) =>
        field == TotalSortField ? Total() != null : field != SortField.Default && Enum.IsDefined(typeof(SortField), field);

    // Numbers are rounded so two soldiers who show the same value count as equal.
    // Returns null for a column this mod does not know.
    public static int? Compare(SortField field, Entity x, Entity y)
    {
        switch (field)
        {
            case SortField.Rank:
                return StrategyActorRankComparer.Default.Compare(x, y);
            case SortField.Name:
                return EntityNameComparer.Default.Compare(x, y);
            case SortField.Hp:
                return Rounded(x.GetWoundUnmodifiedHitPoints().Maximum, y.GetWoundUnmodifiedHitPoints().Maximum);
            case SortField.Stress:
                return Rounded(x.Stress().Value, y.Stress().Value);
            case SortField.TimeUnits:
                return Rounded(x.TimeUnits().Value, y.TimeUnits().Value);
            case SortField.Accuracy:
                return Rounded(x.Accuracy().Value, y.Accuracy().Value);
            case SortField.Strength:
                return Rounded(x.Strength().Value, y.Strength().Value);
            case SortField.Reflexes:
                return Rounded(x.Reflexes().Value, y.Reflexes().Value);
            case SortField.Bravery:
                return Rounded(x.Bravery().Value, y.Bravery().Value);
            case SortField.Status:
                return StrategyActorStatusComparer.Default.Compare(x, y);
            case SortField.Loadout:
                return StrategyActorLoadoutProfileComparer.Default.Compare(x, y);
            case SortField.Missions:
                return Comparer<int>.Default.Compare(
                    x.History().Get(ActorHistoryExtensions.REG_TOTAL_MISSION_COUNT, 0),
                    y.History().Get(ActorHistoryExtensions.REG_TOTAL_MISSION_COUNT, 0));
            case SortField.Kills:
                return Comparer<int>.Default.Compare(
                    x.History().Get(ActorHistoryExtensions.REG_TOTAL_KILL_COUNT, 0),
                    y.History().Get(ActorHistoryExtensions.REG_TOTAL_KILL_COUNT, 0));
            case SortField.LastMissionDate:
                return Comparer<DateTime>.Default.Compare(LastMissionDate(x), LastMissionDate(y));
            case SortField.LastMissionName:
                return StringComparer.OrdinalIgnoreCase.Compare(LastMissionName(x), LastMissionName(y));
            case TotalSortField:
                var total = Total();
                return total == null ? (int?)null : total(x).CompareTo(total(y));
            default:
                return null;
        }
    }

    private static int Rounded(float a, float b) => Mathf.RoundToInt(a).CompareTo(Mathf.RoundToInt(b));

    private static DateTime LastMissionDate(Entity actor)
    {
        if (!actor.HasCombatantMissionHistory())
            return DateTime.MinValue;
        var campaign = actor.World.GetSystem<CampaignLogicSystem>().Campaign;
        return (from info in actor.CombatantMissionHistory().GetLastMissionInfo<MissionInfo>(campaign)
                select info.Date).GetOr(DateTime.MinValue);
    }

    private static string LastMissionName(Entity actor)
    {
        if (!actor.HasCombatantMissionHistory())
            return "";
        var campaign = actor.World.GetSystem<CampaignLogicSystem>().Campaign;
        return (from GCMissionInfo info in actor.CombatantMissionHistory().GetLastMissionInfo<MissionInfo>(campaign)
                select $"{info.Name}, {info.GeoRegion.ToHumanReadableString()}").GetOr("");
    }

    private static Func<Entity, int> Total()
    {
        if (_totalLookedUp)
            return _total;
        _totalLookedUp = true;
        try
        {
            var type = AccessTools.TypeByName("SoldierTotalStats.TotalStats");
            var method = type == null ? null : AccessTools.Method(type, "Compute");
            if (method != null)
                _total = (Func<Entity, int>)Delegate.CreateDelegate(typeof(Func<Entity, int>), method);
        }
        catch (Exception e)
        {
            ModLog.Warn("Soldier Total Stats found but its total could not be read: " + e.Message);
        }
        ModLog.Info(_total != null ? "TOT column available as a sort level" : "Soldier Total Stats not installed; TOT is not a sort level");
        return _total;
    }
}

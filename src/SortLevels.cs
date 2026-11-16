using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Strategy.UI.Elements.Soldiers;
using Xenonauts.Common.Util.Comparers;

namespace MultiColumnSort;

// The extra sort levels of one list, kept beside the game's comparer, which only knows the first.
internal sealed class SortLevels
{
    public struct Level
    {
        public SortField Field;
        public int Order; // -1 or 1, same meaning as the game's sortOrder
    }

    private static readonly ConditionalWeakTable<StrategyActorComparer, SortLevels> Table =
        new ConditionalWeakTable<StrategyActorComparer, SortLevels>();

    // The first-level column these levels were added under. They stop applying when it changes.
    public SortField Anchor;
    public readonly List<Level> Levels = new List<Level>();

    public static SortLevels For(StrategyActorComparer comparer) => Table.GetOrCreateValue(comparer);

    public static SortLevels Active(StrategyActorComparer comparer) =>
        Table.TryGetValue(comparer, out var levels) && levels.Levels.Count > 0 && levels.Anchor == comparer.sortField
            ? levels
            : null;

    public static void Clear(StrategyActorComparer comparer)
    {
        if (Table.TryGetValue(comparer, out var levels))
            levels.Levels.Clear();
    }

    // First use sorts high to low like the game's columns, the second reverses, the third removes.
    public void Cycle(SortField field)
    {
        int i = Levels.FindIndex(l => l.Field == field);
        if (i < 0)
            Levels.Add(new Level { Field = field, Order = -1 });
        else if (Levels[i].Order == -1)
            Levels[i] = new Level { Field = field, Order = 1 };
        else
            Levels.RemoveAt(i);
    }
}

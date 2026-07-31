using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ShelfLimiter
{
    [StaticConstructorOnStartup]
    public static class ShelfLimiterBootstrap
    {
        static ShelfLimiterBootstrap()
        {
            new Harmony("kolop.shelflimiter").PatchAll();
        }
    }

    internal sealed class ShelfLimitData
    {
        internal Dictionary<string, int> Limits = new Dictionary<string, int>();
        internal readonly Dictionary<string, string> Buffers = new Dictionary<string, string>();
    }

    internal static class ShelfLimits
    {
        private static readonly ConditionalWeakTable<Building_Storage, ShelfLimitData> Data =
            new ConditionalWeakTable<Building_Storage, ShelfLimitData>();

        internal static Building_Storage ActiveShelf;

        internal static bool IsSupportedShelf(Building_Storage shelf)
        {
            if (shelf == null || shelf.def == null)
            {
                return false;
            }

            return shelf.def.defName == "Shelf" || shelf.def.defName == "ShelfSmall";
        }

        internal static ShelfLimitData For(Building_Storage shelf)
        {
            return Data.GetOrCreateValue(shelf);
        }

        internal static void ReplaceLimits(Building_Storage shelf, Dictionary<string, int> limits)
        {
            ShelfLimitData data = For(shelf);
            data.Limits = limits ?? new Dictionary<string, int>();
            data.Buffers.Clear();
        }

        internal static bool TryGetLimit(Building_Storage shelf, ThingDef def, out int limit)
        {
            limit = 0;
            return IsSupportedShelf(shelf) && def != null &&
                   For(shelf).Limits.TryGetValue(def.defName, out limit);
        }

        internal static int StoredCount(Building_Storage shelf, ThingDef def)
        {
            if (shelf == null || !shelf.Spawned)
            {
                return 0;
            }

            int count = 0;
            foreach (Thing thing in shelf.GetSlotGroup().HeldThings)
            {
                if (thing.def == def)
                {
                    count += thing.stackCount;
                }
            }

            return count;
        }

        internal static int AbsoluteCapacity(Building_Storage shelf, ThingDef def)
        {
            if (shelf == null || def == null || shelf.def == null || shelf.def.building == null)
            {
                return 0;
            }

            int cellCount = shelf.GetSlotGroup().CellsList.Count;
            int stacksPerCell = Math.Max(1, shelf.def.building.maxItemsInCell);
            long capacity = (long)cellCount * stacksPerCell * Math.Max(1, def.stackLimit);
            return capacity > int.MaxValue ? int.MaxValue : (int)capacity;
        }

        internal static int IncomingCount(Building_Storage shelf, ThingDef def, Pawn excludedPawn = null)
        {
            if (shelf == null || !shelf.Spawned || shelf.Map == null)
            {
                return 0;
            }

            int count = 0;
            IReadOnlyList<Pawn> pawns = shelf.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn == excludedPawn)
                {
                    continue;
                }

                Job job = pawn.CurJob;
                if (job == null || job.def != JobDefOf.HaulToCell)
                {
                    continue;
                }

                IntVec3 destination = job.GetTarget(TargetIndex.B).Cell;
                if (!destination.IsValid || ShelfAt(destination, shelf.Map) != shelf)
                {
                    continue;
                }

                Thing carried = pawn.carryTracker.CarriedThing;
                if (carried != null && carried.def == def)
                {
                    count += carried.stackCount;
                    continue;
                }

                Thing target = job.GetTarget(TargetIndex.A).Thing;
                if (target != null && target.def == def)
                {
                    count += Math.Min(Math.Max(job.count, 0), target.stackCount);
                }
            }

            return count;
        }

        internal static int Remaining(Building_Storage shelf, ThingDef def, Pawn excludedPawn = null)
        {
            int limit;
            if (!TryGetLimit(shelf, def, out limit))
            {
                return int.MaxValue;
            }

            return Math.Max(0, limit - StoredCount(shelf, def) - IncomingCount(shelf, def, excludedPawn));
        }

        internal static int ExcessFor(Thing thing)
        {
            if (thing == null || !thing.Spawned)
            {
                return 0;
            }

            SlotGroup slotGroup = thing.GetSlotGroup();
            Building_Storage shelf = slotGroup == null ? null : slotGroup.parent as Building_Storage;
            int limit;
            if (!TryGetLimit(shelf, thing.def, out limit))
            {
                return 0;
            }

            int amountStillAllowed = limit;
            foreach (Thing stored in shelf.GetSlotGroup().HeldThings)
            {
                if (stored.def != thing.def)
                {
                    continue;
                }

                int keptFromStack = Math.Min(stored.stackCount, Math.Max(amountStillAllowed, 0));
                if (stored == thing)
                {
                    return stored.stackCount - keptFromStack;
                }

                amountStillAllowed -= keptFromStack;
            }

            return 0;
        }

        internal static Building_Storage ShelfAt(IntVec3 cell, Map map)
        {
            if (map == null || !cell.IsValid)
            {
                return null;
            }

            SlotGroup slotGroup = cell.GetSlotGroup(map);
            return slotGroup == null ? null : slotGroup.parent as Building_Storage;
        }
    }

    [HarmonyPatch(typeof(ITab_Storage), "FillTab")]
    internal static class StorageTabContextPatch
    {
        private static void Prefix()
        {
            Building_Storage shelf = Find.Selector.SingleSelectedThing as Building_Storage;
            ShelfLimits.ActiveShelf = ShelfLimits.IsSupportedShelf(shelf) ? shelf : null;
        }

        private static void Postfix()
        {
            ShelfLimits.ActiveShelf = null;
        }

        private static Exception Finalizer(Exception __exception)
        {
            ShelfLimits.ActiveShelf = null;
            return __exception;
        }
    }

    [HarmonyPatch(typeof(Listing_Tree), "get_LabelWidth")]
    internal static class StorageRowWidthPatch
    {
        private static void Postfix(Listing_Tree __instance, ref float __result)
        {
            if (ShelfLimits.ActiveShelf != null && __instance is Listing_TreeThingFilter)
            {
                __result = Math.Max(120f, __instance.ColumnWidth - 92f);
            }
        }
    }

    [HarmonyPatch(typeof(Listing_TreeThingFilter), "DoThingDef")]
    internal static class StorageLimitFieldPatch
    {
        private static readonly AccessTools.FieldRef<Listing_TreeThingFilter, Rect> VisibleRect =
            AccessTools.FieldRefAccess<Listing_TreeThingFilter, Rect>("visibleRect");

        private static void Prefix(Listing_TreeThingFilter __instance, ThingDef tDef)
        {
            Building_Storage shelf = ShelfLimits.ActiveShelf;
            if (shelf == null)
            {
                return;
            }

            Rect row = new Rect(0f, __instance.CurHeight, __instance.ColumnWidth, 20f);
            if (!VisibleRect(__instance).Overlaps(row))
            {
                return;
            }

            ShelfLimitData data = ShelfLimits.For(shelf);
            string buffer;
            if (!data.Buffers.TryGetValue(tDef.defName, out buffer))
            {
                int savedLimit;
                buffer = data.Limits.TryGetValue(tDef.defName, out savedLimit)
                    ? savedLimit.ToString()
                    : string.Empty;
            }

            Rect fieldRect = new Rect(__instance.ColumnWidth - 62f, __instance.CurHeight, 58f, 20f);
            string entered = Widgets.TextField(fieldRect, buffer, 10);
            string digits = new string(entered.Where(char.IsDigit).ToArray());

            int oldLimit;
            bool previouslyLimited = data.Limits.TryGetValue(tDef.defName, out oldLimit);
            bool changed = false;

            if (digits.Length == 0)
            {
                if (previouslyLimited)
                {
                    data.Limits.Remove(tDef.defName);
                    changed = true;
                }
            }
            else
            {
                long typedLimit;
                if (long.TryParse(digits, out typedLimit))
                {
                    int absoluteMaximum = ShelfLimits.AbsoluteCapacity(shelf, tDef);
                    int newLimit = (int)Math.Min(typedLimit, absoluteMaximum);
                    digits = newLimit.ToString();
                    if (!previouslyLimited || oldLimit != newLimit)
                    {
                        data.Limits[tDef.defName] = newLimit;
                        changed = true;
                    }
                }
            }

            data.Buffers[tDef.defName] = digits;

            TooltipHandler.TipRegion(fieldRect,
                "Maximum amount on this shelf: " + ShelfLimits.AbsoluteCapacity(shelf, tDef) +
                ". Leave empty for vanilla behavior.");

            if (changed)
            {
                shelf.Notify_SettingsChanged();
            }
        }
    }

    [HarmonyPatch(typeof(Building_Storage), "ExposeData")]
    internal static class SaveShelfLimitsPatch
    {
        private static void Postfix(Building_Storage __instance)
        {
            if (!ShelfLimits.IsSupportedShelf(__instance))
            {
                return;
            }

            Dictionary<string, int> limits = ShelfLimits.For(__instance).Limits;
            Scribe_Collections.Look(ref limits, "shelfLimiterLimits", LookMode.Value, LookMode.Value);

            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                ShelfLimits.ReplaceLimits(__instance, limits);
            }
        }
    }

    [HarmonyPatch(typeof(StoreUtility), "IsGoodStoreCell")]
    internal static class RejectFullLimitedShelfPatch
    {
        private static void Postfix(IntVec3 c, Map map, Thing t, ref bool __result)
        {
            if (!__result || t == null)
            {
                return;
            }

            Building_Storage shelf = ShelfLimits.ShelfAt(c, map);
            int limit;
            if (ShelfLimits.TryGetLimit(shelf, t.def, out limit) &&
                ShelfLimits.Remaining(shelf, t.def) <= 0)
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(StoreUtility), "IsInValidBestStorage")]
    internal static class ExistingExcessNeedsHaulingPatch
    {
        private static void Postfix(Thing t, ref bool __result)
        {
            if (__result && ShelfLimits.ExcessFor(t) > 0)
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(HaulAIUtility), "HaulToStorageJob")]
    internal static class HaulExistingExcessPatch
    {
        private static bool Prefix(Pawn p, Thing t, ref Job __result)
        {
            int excess = ShelfLimits.ExcessFor(t);
            if (excess <= 0)
            {
                return true;
            }

            IntVec3 foundCell;
            IHaulDestination destination;
            if (!StoreUtility.TryFindBestBetterStorageFor(t, p, p.Map, StoragePriority.Unstored,
                    p.Faction, out foundCell, out destination))
            {
                __result = null;
                return false;
            }

            if (destination is ISlotGroupParent)
            {
                __result = HaulAIUtility.HaulToCellStorageJob(p, t, foundCell, false);
            }
            else
            {
                Thing container = destination as Thing;
                __result = container == null ? null : HaulAIUtility.HaulToContainerJob(p, t, container);
            }

            if (__result != null)
            {
                __result.count = Math.Min(__result.count, excess);
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(HaulAIUtility), "HaulToCellStorageJob")]
    internal static class CapHaulAmountPatch
    {
        private static void Postfix(Pawn p, Thing t, IntVec3 storeCell, ref Job __result)
        {
            if (__result == null || p == null || t == null)
            {
                return;
            }

            Building_Storage shelf = ShelfLimits.ShelfAt(storeCell, p.Map);
            int limit;
            if (!ShelfLimits.TryGetLimit(shelf, t.def, out limit))
            {
                return;
            }

            int remaining = ShelfLimits.Remaining(shelf, t.def, p);
            if (remaining <= 0)
            {
                __result = null;
                return;
            }

            __result.count = Math.Min(__result.count, remaining);
        }
    }
}

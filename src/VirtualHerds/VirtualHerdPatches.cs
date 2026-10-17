using HarmonyLib;
using RimWorld.Planet;
using Verse;

namespace VirtualHerds
{
    // These are topology notifications only. The bounded map fallback remains authoritative.
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SpawnSetup))]
    internal static class VirtualHerdPawnSpawnPatch
    {
        private static void Postfix(Pawn __instance)
        {
            __instance?.MapHeld?.GetComponent<VirtualHerdMapComponent>()?.NotifyPawnTopologyChanged();
        }
    }

    [HarmonyPatch(typeof(Pawn), "DeSpawn")]
    internal static class VirtualHerdPawnDeSpawnPatch
    {
        private static void Prefix(Pawn __instance)
        {
            __instance?.MapHeld?.GetComponent<VirtualHerdMapComponent>()?.NotifyPawnTopologyChanged();
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SetFaction))]
    internal static class VirtualHerdPawnFactionPatch
    {
        private static void Postfix(Pawn __instance)
        {
            __instance?.MapHeld?.GetComponent<VirtualHerdMapComponent>()?.NotifyPawnTopologyChanged();
        }
    }

    [HarmonyPatch(typeof(SettlementAbandonUtility), nameof(SettlementAbandonUtility.TryAbandonViaInterface))]
    internal static class VirtualHerdSettlementAbandonPatch
    {
        private static MapParent bypassFor;

        private static bool Prefix(MapParent settlement)
        {
            if (settlement == null)
                return true;
            if (bypassFor == settlement)
            {
                bypassFor = null;
                return true;
            }
            var component = settlement.Map?.GetComponent<VirtualHerdMapComponent>();
            var count = component?.StoredCount ?? 0;
            if (count <= 0)
                return true;
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "Abandon this map and permanently discard " + count + " stored virtual animals?",
                () =>
                {
                    bypassFor = settlement;
                    SettlementAbandonUtility.TryAbandonViaInterface(settlement);
                }, true));
            return false;
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace VirtualHerds
{
    public sealed class WorkGiver_HaulVirtualHerdFood : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.HaulableEver);

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            return pawn?.Map?.listerThings?.ThingsInGroup(ThingRequestGroup.HaulableEver)
                ?.Where(VirtualHerdUtility.IsCompatibleFood) ?? Enumerable.Empty<Thing>();
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (pawn?.Map == null || !VirtualHerdUtility.IsCompatibleFood(t) || t.IsForbidden(pawn) || !pawn.CanReserve(t, 1, -1, null, forced))
                return false;
            return FindBarn(pawn.Map, t, pawn, forced) != null;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            var barn = FindBarn(pawn?.Map, t, pawn, forced);
            return barn == null ? null : HaulAIUtility.HaulToContainerJob(pawn, t, barn);
        }

        private static Building_VirtualBarn FindBarn(Map map, Thing food, Pawn pawn, bool forced)
        {
            if (map == null)
                return null;
            return map.listerBuildings.AllBuildingsColonistOfClass<Building_VirtualBarn>()
                .Where(barn => barn != null && !barn.Destroyed && (barn.FoodFilter == null || barn.FoodFilter.Allows(food)))
                .Where(barn => map.GetComponent<VirtualHerdMapComponent>()?.FoodCompatibleWithStoredSpecies(food.def) == true)
                .Where(barn => barn.MapHeld == map && barn.GetDirectlyHeldThings().GetCountCanAccept(food) > 0)
                .OrderBy(barn => barn.FoodInventory.TotalStackCount)
                .FirstOrDefault(barn => map.GetComponent<VirtualHerdMapComponent>()?.NeedsFoodHauling == true
                    && pawn.CanReserve(barn, 1, -1, null, forced));
        }
    }
}

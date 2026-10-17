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

        public static Thing FindFoodForBarn(Pawn pawn, Building_VirtualBarn barn)
        {
            if (pawn?.Map == null || barn?.Map != pawn.Map)
                return null;
            return GenClosest.ClosestThingReachable(pawn.Position, pawn.Map,
                ThingRequest.ForGroup(ThingRequestGroup.HaulableEver), PathEndMode.ClosestTouch,
                TraverseParms.For(pawn), 9999f,
                food => VirtualHerdUtility.IsCompatibleFood(food) && !food.IsForbidden(pawn)
                    && barn.FoodFilter.Allows(food)
                    && pawn.Map.GetComponent<VirtualHerdMapComponent>().FoodCompatibleWithStoredSpecies(food.def)
                    && barn.FoodInventory.GetCountCanAccept(food) > 0
                    && pawn.CanReserve(food, 1, -1, null, true));
        }

        private static Building_VirtualBarn FindBarn(Map map, Thing food, Pawn pawn, bool forced)
        {
            if (map == null)
                return null;
            return map.listerBuildings.AllBuildingsColonistOfClass<Building_VirtualBarn>()
                .Where(barn => barn != null && !barn.Destroyed && !barn.IsForbidden(pawn)
                    && (barn.FoodFilter == null || barn.FoodFilter.Allows(food)))
                .Where(barn => map.GetComponent<VirtualHerdMapComponent>()?.FoodCompatibleWithStoredSpecies(food.def) == true)
                .Where(barn => barn.MapHeld == map && barn.GetDirectlyHeldThings().GetCountCanAccept(food) > 0)
                .OrderBy(barn => barn.FoodInventory.TotalStackCount)
                .FirstOrDefault(barn => map.GetComponent<VirtualHerdMapComponent>()?.NeedsFoodHauling == true
                    && pawn.CanReserveAndReach(barn, PathEndMode.Touch, Danger.Some, 1, -1, null, forced));
        }
    }
}

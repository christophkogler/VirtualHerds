using RimWorld;
using Verse;

namespace VirtualHerds
{
    public sealed class PlaceWorker_VirtualBarn : PlaceWorker
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            foreach (var cell in GenAdj.OccupiedRect(loc, rot, checkingDef.Size))
            {
                if (!cell.InBounds(map))
                    return "The barn must fit inside the map.";
                foreach (var existing in cell.GetThingList(map))
                {
                    if (existing is Building_VirtualBarn)
                        return "Select the existing barn and use Upgrade barn to preserve its herd and feed.";
                    if (existing != thingToIgnore && existing != thing && (existing is Blueprint || existing is Frame) && existing.def.entityDefToBuild is ThingDef target
                        && target.thingClass == typeof(Building_VirtualBarn))
                        return "Another barn is already planned here.";
                }
            }
            return true;
        }
    }
}

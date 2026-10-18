using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace VirtualHerds
{
    public sealed class WorkGiver_UpgradeVirtualBarn : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial);
        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn) => pawn.Map.listerBuildings.AllBuildingsColonistOfClass<Building_VirtualBarn>();
        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            var barn = t as Building_VirtualBarn;
            if (barn == null || !barn.HasUpgrade || barn.Faction != pawn.Faction || barn.IsForbidden(pawn)
                || !pawn.CanReserveAndReach(barn, PathEndMode.Touch, Danger.Some, 1, -1, null, forced)
                || !barn.UpgradeClearance(barn.UpgradeTarget).Accepted)
                return false;
            return barn.UpgradeMaterialsReady || FindMaterial(pawn, barn, forced) != null;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            var barn = t as Building_VirtualBarn;
            if (!HasJobOnThing(pawn, t, forced))
                return null;
            var job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("VirtualHerds_UpgradeBarn"), barn);
            if (!barn.UpgradeMaterialsReady)
            {
                var material = FindMaterial(pawn, barn, forced);
                if (material == null)
                    return null;
                job.targetB = material;
                job.count = System.Math.Min(barn.MissingUpgradeMaterial(material.def), material.stackCount);
            }
            return job;
        }

        private static Thing FindMaterial(Pawn pawn, Building_VirtualBarn barn, bool forced)
        {
            return GenClosest.ClosestThingReachable(pawn.Position, pawn.Map, ThingRequest.ForGroup(ThingRequestGroup.HaulableEver),
                PathEndMode.ClosestTouch, TraverseParms.For(pawn), VirtualHerds.Pure.BalanceTuning.Current["haulRadius"],
                t => barn.MissingUpgradeMaterial(t.def) > 0 && !t.IsForbidden(pawn) && pawn.CanReserve(t, 1, -1, null, forced));
        }
    }
}

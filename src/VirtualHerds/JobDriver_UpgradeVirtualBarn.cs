using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace VirtualHerds
{
    public sealed class JobDriver_UpgradeVirtualBarn : JobDriver
    {
        private Building_VirtualBarn Barn => job.targetA.Thing as Building_VirtualBarn;
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed)
                && (!job.targetB.HasThing || pawn.Reserve(job.targetB, job, 1, -1, null, errorOnFailed));
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(() => Barn == null || !Barn.HasUpgrade || !Barn.UpgradeClearance(Barn.UpgradeTarget).Accepted);
            if (job.targetB.HasThing)
            {
                this.FailOnDestroyedNullOrForbidden(TargetIndex.B);
                yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch);
                yield return Toils_Haul.StartCarryThing(TargetIndex.B, false, false);
                yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
                var deliver = ToilMaker.MakeToil("DeliverBarnUpgradeMaterial");
                deliver.initAction = () => Barn.DeliverUpgradeMaterial(pawn.carryTracker.CarriedThing);
                deliver.defaultCompleteMode = ToilCompleteMode.Instant;
                yield return deliver;
                yield break;
            }
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            var work = ToilMaker.MakeToil("BuildBarnUpgrade");
            work.tickAction = () =>
            {
                if (!Barn.UpgradeMaterialsReady)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                pawn.skills?.Learn(SkillDefOf.Construction, VirtualHerds.Pure.BalanceTuning.Current["upgradeXp"]);
                if (Barn.WorkOnUpgrade(pawn.GetStatValue(StatDefOf.ConstructionSpeed)))
                    EndJobWith(JobCondition.Succeeded);
            };
            work.defaultCompleteMode = ToilCompleteMode.Never;
            work.WithProgressBar(TargetIndex.A, () => Barn?.UpgradeProgress ?? 1f);
            yield return work;
        }
    }
}

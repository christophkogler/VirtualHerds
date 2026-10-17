using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using VirtualHerds.Pure;

namespace VirtualHerds
{
    public static class VirtualHerdUtility
    {
        public static bool IsEligible(Pawn pawn, out string reason)
        {
            reason = null;
            if (pawn == null || pawn.RaceProps?.Animal != true) { reason = "not an animal"; return false; }
            if (!pawn.Spawned || pawn.Dead || pawn.Destroyed) { reason = "not spawned"; return false; }
            if (!AutoSlaughterManager.CanAutoSlaughterNow(pawn)) { reason = "not available to auto-slaughter"; return false; }
            if (pawn.kindDef == null || pawn.kindDef.race != pawn.def || pawn.kindDef.RaceProps?.Animal != true) { reason = "no releasable pawn kind"; return false; }
            if (pawn.relations != null && pawn.relations.GetDirectRelationsCount(PawnRelationDefOf.Bond) > 0) { reason = "bonded"; return false; }
            return true;
        }

        public static bool IsEligible(Pawn pawn)
        {
            string reason;
            return IsEligible(pawn, out reason);
        }

        public static HerdSex SexOf(Pawn pawn)
        {
            return FromVerseGender(pawn.gender);
        }

        public static HerdSex FromVerseGender(Gender gender)
        {
            switch (gender)
            {
                case Gender.Male: return HerdSex.Male;
                case Gender.Female: return HerdSex.Female;
                default: return HerdSex.None;
            }
        }

        public static Gender ToVerseGender(HerdSex sex)
        {
            switch (sex)
            {
                case HerdSex.Male: return Gender.Male;
                case HerdSex.Female: return Gender.Female;
                default: return Gender.None;
            }
        }

        public static bool IsPregnant(Pawn pawn, out float progress)
        {
            progress = 0f;
            var pregnancy = pawn?.health?.hediffSet?.GetFirstHediffOfDef(HediffDefOf.Pregnant) as Hediff_Pregnant;
            if (pregnancy == null)
                return false;
            progress = Math.Max(0f, Math.Min(0.99999f, pregnancy.GestationProgress));
            return true;
        }

        public static VirtualHerdCohort MakeCohort(Pawn pawn)
        {
            var def = pawn.def;
            var stage = Math.Max(0, Math.Min(def.race.lifeStageAges.Count - 1, pawn.ageTracker.CurLifeStageIndex));
            var ageBin = AgeBin(pawn);
            float gestation;
            var pregnant = IsPregnant(pawn, out gestation);
            return new VirtualHerdCohort
            {
                defName = def.defName,
                pawnKindDefName = pawn.kindDef.defName,
                sex = SexOf(pawn),
                lifeStageIndex = stage,
                ageProgressBin = ageBin,
                gestationBin = pregnant ? GestationBin(gestation) : -1,
                count = 1
            };
        }

        public static int AgeBin(Pawn pawn)
        {
            var stage = pawn.ageTracker.CurLifeStageRace;
            if (stage == null)
                return 0;
            var next = NextStageMinAge(pawn.def, pawn.ageTracker.CurLifeStageIndex);
            var duration = Math.Max(0.1f, next - stage.minAge);
            var progress = (pawn.ageTracker.AgeBiologicalYearsFloat - stage.minAge) / duration;
            return Math.Max(0, Math.Min(15, (int)(Math.Max(0f, Math.Min(0.99999f, progress)) * 16f)));
        }

        public static float NextStageMinAge(ThingDef def, int stageIndex)
        {
            if (def?.race?.lifeStageAges == null || def.race.lifeStageAges.Count == 0)
                return 1f;
            if (stageIndex + 1 < def.race.lifeStageAges.Count)
                return def.race.lifeStageAges[stageIndex + 1].minAge;
            // Extend the final age bucket beyond life expectancy so deterministic old-age
            // mortality can actually begin instead of pinning every animal just below it.
            return Math.Max(def.race.lifeExpectancy * 1.25f, def.race.lifeStageAges[stageIndex].minAge + 1f);
        }

        public static int GestationBin(float progress)
        {
            return Math.Max(0, Math.Min(15, (int)(progress * 16f)));
        }

        public static bool IsNutritionFoodDef(ThingDef foodDef)
        {
            if (foodDef?.ingestible == null)
                return false;
            var flags = foodDef.ingestible.foodType;
            return flags.HasFlag(FoodTypeFlags.Plant) || flags.HasFlag(FoodTypeFlags.Meat)
                || flags.HasFlag(FoodTypeFlags.AnimalProduct) || flags.HasFlag(FoodTypeFlags.Meal)
                || flags.HasFlag(FoodTypeFlags.Kibble);
        }

        public static bool IsCompatibleFood(Thing thing)
        {
            return IsNutritionFoodDef(thing?.def);
        }

        public static bool IsCompatibleFood(ThingDef speciesDef, ThingDef foodDef)
        {
            return speciesDef?.race != null && IsNutritionFoodDef(foodDef)
                && (speciesDef.race.foodType & foodDef.ingestible.foodType) != 0;
        }

        public static void EndOrdinaryJobForCommit(Pawn pawn)
        {
            if (pawn?.jobs == null || pawn.CurJob == null)
                return;
            pawn.Map?.reservationManager?.ReleaseAllClaimedBy(pawn);
            pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
        }

        public static bool IsAdult(ThingDef def, int stageIndex)
        {
            return def?.race?.lifeStageAges != null && stageIndex >= 0 && stageIndex < def.race.lifeStageAges.Count
                && def.race.lifeStageAges[stageIndex].def.reproductive;
        }

        public static PawnKindDef PawnKindFor(string kindDefName, ThingDef def)
        {
            var kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(kindDefName);
            return kind != null && kind.race == def && kind.RaceProps?.Animal == true ? kind : null;
        }

        public static DevelopmentalStage DevelopmentalStagesFor(ThingDef def, int stageIndex)
        {
            if (def?.race?.lifeStageAges == null || def.race.lifeStageAges.Count == 0)
                return DevelopmentalStage.Adult;
            var stage = def.race.lifeStageAges[Math.Max(0, Math.Min(def.race.lifeStageAges.Count - 1, stageIndex))];
            return stage.def.developmentalStage;
        }

        public static float NutritionPerDay(ThingDef def, int lifeStageIndex)
        {
            if (def?.race == null)
                return 0.01f;
            var stageFactor = 1f;
            if (def.race.lifeStageAges != null && def.race.lifeStageAges.Count > 0)
            {
                var index = Math.Max(0, Math.Min(def.race.lifeStageAges.Count - 1, lifeStageIndex));
                stageFactor = def.race.lifeStageAges[index].def.hungerRateFactor;
            }
            // Need_Food's unmodified Fed-category fall rate is baseHungerRate *
            // lifeStageFactor * 1.6 nutrition per 60-day RimWorld year.
            return Math.Max(0.01f, def.race.baseHungerRate * stageFactor * 1.6f);
        }

        public static CompProperties_Milkable Milkable(ThingDef def)
        {
            return def?.comps?.OfType<CompProperties_Milkable>().FirstOrDefault();
        }

        public static CompProperties_Shearable Shearable(ThingDef def)
        {
            return def?.comps?.OfType<CompProperties_Shearable>().FirstOrDefault();
        }

        public static CompProperties_EggLayer EggLayer(ThingDef def)
        {
            return def?.comps?.OfType<CompProperties_EggLayer>().FirstOrDefault();
        }

        public static string FormatLimit(int value)
        {
            return value < 0 ? "∞" : value.ToString();
        }

    }
}

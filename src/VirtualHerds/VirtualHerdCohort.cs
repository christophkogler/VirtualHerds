using System;
using Verse;
using VirtualHerds.Pure;

namespace VirtualHerds
{
    public sealed class VirtualHerdCohort : IExposable
    {
        public HerdBoostSettings boosts = new HerdBoostSettings();
        public string defName;
        public string pawnKindDefName;
        public HerdSex sex;
        public int lifeStageIndex;
        public int ageProgressBin;
        public int gestationBin = -1;
        public int count;
        public float ageAccumulator;
        public float gestationAccumulator;
        public float conceptionRemainder;
        public float birthRemainder;
        public float femaleBirthRemainder;
        public float mortalityRemainder;
        public float milkAccumulator;
        public float woolAccumulator;
        public float eggAccumulator;

        public ThingDef Def => DefDatabase<ThingDef>.GetNamedSilentFail(defName);
        public HerdSex Sex => sex;
        public bool Pregnant => gestationBin >= 0;

        public void ExposeData()
        {
            Scribe_Deep.Look(ref boosts, "boosts");
            Scribe_Values.Look(ref defName, "defName");
            Scribe_Values.Look(ref pawnKindDefName, "pawnKindDefName");
            Scribe_Values.Look(ref sex, "sex");
            Scribe_Values.Look(ref lifeStageIndex, "lifeStageIndex");
            Scribe_Values.Look(ref ageProgressBin, "ageProgressBin");
            Scribe_Values.Look(ref gestationBin, "gestationBin", -1);
            Scribe_Values.Look(ref count, "count");
            Scribe_Values.Look(ref ageAccumulator, "ageAccumulator");
            Scribe_Values.Look(ref gestationAccumulator, "gestationAccumulator");
            Scribe_Values.Look(ref conceptionRemainder, "conceptionRemainder");
            Scribe_Values.Look(ref birthRemainder, "birthRemainder");
            Scribe_Values.Look(ref femaleBirthRemainder, "femaleBirthRemainder");
            Scribe_Values.Look(ref mortalityRemainder, "mortalityRemainder");
            Scribe_Values.Look(ref milkAccumulator, "milkAccumulator");
            Scribe_Values.Look(ref woolAccumulator, "woolAccumulator");
            Scribe_Values.Look(ref eggAccumulator, "eggAccumulator");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                boosts = boosts ?? new HerdBoostSettings();
                count = count < 0 ? 0 : count;
                ageProgressBin = ageProgressBin < 0 ? 0 : ageProgressBin > 15 ? 15 : ageProgressBin;
                gestationBin = gestationBin < 0 ? -1 : gestationBin > 16 ? 16 : gestationBin;
                ageAccumulator = Nonnegative(ageAccumulator);
                gestationAccumulator = Nonnegative(gestationAccumulator);
                conceptionRemainder = Nonnegative(conceptionRemainder);
                birthRemainder = Nonnegative(birthRemainder);
                femaleBirthRemainder = Nonnegative(femaleBirthRemainder);
                mortalityRemainder = Nonnegative(mortalityRemainder);
                milkAccumulator = Math.Max(0f, milkAccumulator);
                woolAccumulator = Math.Max(0f, woolAccumulator);
                eggAccumulator = Math.Max(0f, eggAccumulator);
            }
        }

        private static float Nonnegative(float value)
        {
            return Math.Max(0f, value);
        }
    }

    public sealed class VirtualHerdLimitEntry : IExposable
    {
        public string defName;
        public int total = -1;
        public int adultMale = -1;
        public int youngMale = -1;
        public int adultFemale = -1;
        public int youngFemale = -1;
        public int pregnant = -1;

        public HerdLimits ToLimits()
        {
            return new HerdLimits
            {
                Total = total,
                AdultMale = adultMale,
                YoungMale = youngMale,
                AdultFemale = adultFemale,
                YoungFemale = youngFemale,
                Pregnant = pregnant
            };
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref defName, "defName");
            Scribe_Values.Look(ref total, "total", -1);
            Scribe_Values.Look(ref adultMale, "adultMale", -1);
            Scribe_Values.Look(ref youngMale, "youngMale", -1);
            Scribe_Values.Look(ref adultFemale, "adultFemale", -1);
            Scribe_Values.Look(ref youngFemale, "youngFemale", -1);
            Scribe_Values.Look(ref pregnant, "pregnant", -1);
        }
    }

    public sealed class VirtualHerdOutput : IExposable
    {
        public string defName;
        public int count;

        public void ExposeData()
        {
            Scribe_Values.Look(ref defName, "defName");
            Scribe_Values.Look(ref count, "count");
        }
    }

    public sealed class VirtualHerdSpeciesState : IExposable
    {
        public string defName;
        public float nutritionCredit;
        public bool needsFoodHauling;

        public void ExposeData()
        {
            Scribe_Values.Look(ref defName, "defName");
            Scribe_Values.Look(ref nutritionCredit, "nutritionCredit");
            Scribe_Values.Look(ref needsFoodHauling, "needsFoodHauling");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                nutritionCredit = Math.Max(0f, nutritionCredit);
        }
    }
}

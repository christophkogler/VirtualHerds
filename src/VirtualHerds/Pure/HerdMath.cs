using System;

namespace VirtualHerds.Pure
{
    public enum HerdSex { Male, Female, None }
    public enum HerdGender { None, Male, Female }

    public static class HerdSexMapping
    {
        public static HerdSex FromGender(HerdGender gender)
        {
            switch (gender)
            {
                case HerdGender.Male: return HerdSex.Male;
                case HerdGender.Female: return HerdSex.Female;
                default: return HerdSex.None;
            }
        }

        public static HerdGender ToGender(HerdSex sex)
        {
            switch (sex)
            {
                case HerdSex.Male: return HerdGender.Male;
                case HerdSex.Female: return HerdGender.Female;
                default: return HerdGender.None;
            }
        }
    }

    public enum HerdCategory { AdultMale, YoungMale, AdultFemale, YoungFemale, Pregnant }

    public struct HerdLimits
    {
        public int Total;
        public int AdultMale;
        public int YoungMale;
        public int AdultFemale;
        public int YoungFemale;
        public int Pregnant;

        public static HerdLimits Infinite => new HerdLimits
        {
            Total = -1, AdultMale = -1, YoungMale = -1, AdultFemale = -1, YoungFemale = -1, Pregnant = -1
        };
    }

    public struct HerdCounts
    {
        public int Total;
        public int AdultMale;
        public int YoungMale;
        public int AdultFemale;
        public int YoungFemale;
        public int Pregnant;

        public int For(HerdCategory category)
        {
            switch (category)
            {
                case HerdCategory.AdultMale: return AdultMale;
                case HerdCategory.YoungMale: return YoungMale;
                case HerdCategory.AdultFemale: return AdultFemale;
                case HerdCategory.YoungFemale: return YoungFemale;
                case HerdCategory.Pregnant: return Pregnant;
                default: return 0;
            }
        }

        public void Add(HerdCategory category, int amount)
        {
            if (amount <= 0) return;
            Total += amount;
            switch (category)
            {
                case HerdCategory.AdultMale: AdultMale += amount; break;
                case HerdCategory.YoungMale: YoungMale += amount; break;
                case HerdCategory.AdultFemale: AdultFemale += amount; break;
                case HerdCategory.YoungFemale: YoungFemale += amount; break;
                case HerdCategory.Pregnant: Pregnant += amount; break;
            }
        }
    }

    public static class HerdMath
    {
        public static HerdCategory CategoryFor(HerdSex sex, bool adult, bool pregnant)
        {
            if (pregnant) return HerdCategory.Pregnant;
            if (sex == HerdSex.Female) return adult ? HerdCategory.AdultFemale : HerdCategory.YoungFemale;
            return adult ? HerdCategory.AdultMale : HerdCategory.YoungMale;
        }

        public static int LimitFor(HerdLimits limits, HerdCategory category)
        {
            switch (category)
            {
                case HerdCategory.AdultMale: return limits.AdultMale;
                case HerdCategory.YoungMale: return limits.YoungMale;
                case HerdCategory.AdultFemale: return limits.AdultFemale;
                case HerdCategory.YoungFemale: return limits.YoungFemale;
                case HerdCategory.Pregnant: return limits.Pregnant;
                default: return limits.Total;
            }
        }

        public static int Remaining(int limit, int stored)
        {
            return limit < 0 ? int.MaxValue : Math.Max(0, limit - stored);
        }

        public static bool CanAbsorb(HerdLimits limits, HerdCounts stored, HerdCategory category)
        {
            return Remaining(LimitFor(limits, category), stored.For(category)) > 0
                && Remaining(limits.Total, stored.Total) > 0;
        }

        public static float ScaleByFeed(float requested, float availableNutrition, float requiredNutrition)
        {
            if (requested <= 0f || requiredNutrition <= 0f) return 0f;
            return requested * Math.Min(1f, Math.Max(0f, availableNutrition / requiredNutrition));
        }

        public static int ConsumeWhole(ref float remainder, float expected)
        {
            if (expected <= 0f) return 0;
            remainder += expected;
            var whole = (int)Math.Floor(remainder);
            remainder -= whole;
            return whole;
        }

        public static float[] AllocateFairNutrition(float[] demands, float available)
        {
            var result = new float[demands.Length];
            if (available <= 0f) return result;
            var totalDemand = 0f;
            foreach (var demand in demands) totalDemand += Math.Max(0f, demand);
            if (totalDemand <= 0f) return result;
            var ratio = Math.Min(1f, available / totalDemand);
            for (var i = 0; i < demands.Length; i++) result[i] = Math.Max(0f, demands[i]) * ratio;
            return result;
        }
    }
}

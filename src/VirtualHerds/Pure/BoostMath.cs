using System;

namespace VirtualHerds.Pure
{
    public static class BoostMath
    {
        private static readonly float[] Caps = { 2f, 3f, 6f, 12f, 25f, 100f };
        private static readonly float[] FeedPremium = { 2f, 1.5f, 1f, 0.5f, 0.2f, 0f };
        private static readonly float[] Energy = { 0f, 50f, 150f, 400f, 1000f, 2500f };
        private static readonly float[] Idle = { 0f, 100f, 500f, 1500f, 5000f, 15000f };
        public static int Tier(int tier) => Math.Max(0, Math.Min(5, tier));
        public static float Cap(int tier) => Caps[Tier(tier)];
        public static float IdleWatts(int tier) => Idle[Tier(tier)];
        public static float Sanitize(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 1f : Math.Max(1f, Math.Min(100f, value));
        public static float Combine(float global, float species, float cohort, int tier)
            => Math.Min(Cap(tier), 1f + Sanitize(global) - 1f + Sanitize(species) - 1f + Sanitize(cohort) - 1f);
        public static float Severity(float output, float breeding, float gestation)
        {
            var extra = Sanitize(output) + Sanitize(breeding) + Sanitize(gestation) - 3f;
            return extra + extra * extra * 0.1f;
        }
        public static float NutritionFactor(int tier, float output, float breeding, float gestation, bool digitized)
            => digitized && Tier(tier) == 5 ? 0f : 1f + FeedPremium[Tier(tier)] * Severity(output, breeding, gestation);
        public static float LoadWatts(int tier, float baselineNutritionPerDay, float output, float breeding, float gestation, bool digitized)
        {
            var digital = digitized && Tier(tier) == 5;
            return Math.Max(0f, baselineNutritionPerDay) * Energy[Tier(tier)] * (1f + Severity(output, breeding, gestation)) * (digital ? 10f : 1f);
        }
    }
}

using System;

namespace VirtualHerds.Pure
{
    public static class BoostMath
    {
        // A save/request validation ceiling, distinct from the configurable effective tier cap.
        public const float RequestCeiling = 1000f;
        public static int Tier(int tier) => Math.Max(0, Math.Min(5, tier));
        public static float Cap(int tier) => BalanceTuning.Current["cap" + Tier(tier)];
        public static float IdleWatts(int tier) => Tier(tier) == 0 ? 0f : BalanceTuning.Current["idle" + Tier(tier)] * BalanceTuning.Current["idlePower"];
        public static float EnergyCoefficient(int tier) => Tier(tier) == 0 ? 0f : BalanceTuning.Current["energy" + Tier(tier)];
        public static float DigitalMultiplier(int tier, bool digitized) => digitized && Tier(tier) == 5 ? BalanceTuning.Current["digitalPower"] : 1f;
        public static float Sanitize(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 1f : Math.Max(1f, Math.Min(RequestCeiling, value));
        public static float Combine(float global, float species, float cohort, int tier)
            => Math.Min(Cap(tier), 1f + Sanitize(global) - 1f + Sanitize(species) - 1f + Sanitize(cohort) - 1f);
        public static float Severity(float output, float breeding, float gestation)
        {
            var extra = Sanitize(output) + Sanitize(breeding) + Sanitize(gestation) - 3f;
            return extra * BalanceTuning.Current["severityLinear"] + extra * extra * BalanceTuning.Current["severityQuadratic"];
        }
        public static float NutritionFactor(int tier, float output, float breeding, float gestation, bool digitized)
            => (1f + BalanceTuning.Current["feed" + Tier(tier)] * Severity(output, breeding, gestation))
                * BalanceTuning.Current["nutrition"] * (digitized && Tier(tier) == 5 ? BalanceTuning.Current["digitalFeed"] : 1f);
        public static float LoadWatts(int tier, float baselineNutritionPerDay, float output, float breeding, float gestation, bool digitized)
            => Math.Max(0f, baselineNutritionPerDay) * EnergyCoefficient(tier) * BalanceTuning.Current["activePower"]
                * (1f + Severity(output, breeding, gestation)) * DigitalMultiplier(tier, digitized);
        public static float MortalityFraction(float age, float expectancy)
            => expectancy <= 0 ? 0f : Math.Max(0f, Math.Min(1f, (age / expectancy - BalanceTuning.Current["mortalityOnset"]) * BalanceTuning.Current["mortality"]));
        public static float BaselineNutrition(float hunger, float stageFactor) => Math.Max(0.01f, hunger * stageFactor * BalanceTuning.Current["baselineHunger"]);
        public static float ProductScale(string product) => BalanceTuning.Current["production"] * BalanceTuning.Current[product];
    }

    public sealed class PowerCostBreakdown
    {
        public int AnimalCount;
        public float BaselineNutrition, TierCoefficient, ActiveScale, IdleScale, ControllerIdleBase, SecondaryIdleWatts;
        public float BoostMultiplier = 1f, DigitalMultiplier = 1f;
        public float AverageNutrition => AnimalCount <= 0 ? 0f : BaselineNutrition / AnimalCount;
        public float UnboostedActiveWatts => BaselineNutrition * TierCoefficient * ActiveScale;
        public float BoostedActiveWatts => UnboostedActiveWatts * BoostMultiplier;
        public float ControllerIdleWatts => ControllerIdleBase * IdleScale;
        public float ControllerWatts => (ControllerIdleWatts + BoostedActiveWatts) * DigitalMultiplier;
        public float TotalWatts => ControllerWatts + SecondaryIdleWatts;
    }
}

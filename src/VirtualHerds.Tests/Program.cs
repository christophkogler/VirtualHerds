using VirtualHerds.Pure;

static class Program
{
    private static void Main()
    {
        Assert(HerdSexMapping.FromGender(HerdGender.Male) == HerdSex.Male, "male gender mapping");
        Assert(HerdSexMapping.FromGender(HerdGender.Female) == HerdSex.Female, "female gender mapping");
        Assert(HerdSexMapping.FromGender(HerdGender.None) == HerdSex.None, "none gender mapping");
        Assert(HerdSexMapping.ToGender(HerdSex.Male) == HerdGender.Male, "male reverse mapping");
        Assert(HerdSexMapping.ToGender(HerdSex.Female) == HerdGender.Female, "female reverse mapping");
        Assert(HerdSexMapping.ToGender(HerdSex.None) == HerdGender.None, "none reverse mapping");
        Assert(HerdMath.CategoryFor(HerdSex.Female, true, true) == HerdCategory.Pregnant, "pregnancy category is disjoint from adult female");
        Assert(HerdMath.CategoryFor(HerdSex.Female, false, false) == HerdCategory.YoungFemale, "young female category");
        var limits = new HerdLimits { Total = 5, AdultMale = 2, YoungMale = -1, AdultFemale = 2, YoungFemale = -1, Pregnant = 1 };
        var stored = new HerdCounts { Total = 3, AdultMale = 2, AdultFemale = 1 };
        Assert(HerdMath.CanAbsorb(limits, stored, HerdCategory.AdultFemale), "category and total capacity");
        Assert(!HerdMath.CanAbsorb(limits, stored, HerdCategory.AdultMale), "adult male category cap");
        Assert(HerdMath.Remaining(5, 5) == 0, "total boundary");
        Assert(HerdMath.Remaining(-1, 100) == int.MaxValue, "infinite boundary");
        var integerCounts = new HerdCounts();
        integerCounts.Add(HerdCategory.AdultFemale, 2);
        Assert(integerCounts.Total == 2 && integerCounts.AdultFemale == 2, "integer count conservation");
        var remainder = 0f;
        Assert(HerdMath.ConsumeWhole(ref remainder, 0.4f) == 0 && remainder > 0.39f, "fractional remainder retained");
        Assert(HerdMath.ConsumeWhole(ref remainder, 0.6f) == 1 && remainder < 0.01f, "remainder conserves one whole unit");
        Assert(HerdMath.ConsumeWhole(ref remainder, 0.25f) == 0 && HerdMath.ConsumeWhole(ref remainder, 0.25f) == 0 && HerdMath.ConsumeWhole(ref remainder, 0.5f) == 1, "deterministic remainder sequence");
        Assert(HerdMath.ScaleByFeed(10f, 0f, 5f) == 0f, "feed shortage pauses output");
        Assert(HerdMath.ScaleByFeed(10f, 2.5f, 5f) == 5f, "feed scales output");
        var allocation = HerdMath.AllocateFairNutrition(new[] { 10f, 20f }, 15f);
        Assert(Math.Abs(allocation[0] - 5f) < 0.001f && Math.Abs(allocation[1] - 10f) < 0.001f, "fair nutrition allocation");
        var noFood = HerdMath.AllocateFairNutrition(new[] { 10f, 20f }, 0f);
        Assert(noFood[0] == 0f && noFood[1] == 0f, "zero nutrition allocation");
        CheckBoostCosts();
        CheckRuntimeTuning();
        CheckPowerMigration();
        System.Console.WriteLine("Virtual Herds pure self-tests passed.");
    }

    private static void CheckBoostCosts()
    {
        var expectedCaps = new[] { 2f, 3f, 6f, 12f, 25f, 100f };
        for (var tier = 0; tier < 6; tier++)
        {
            Assert(BoostMath.Cap(tier) == expectedCaps[tier], "research tier cap " + tier);
            Assert(BoostMath.Combine(1f, 1f, 1f, tier) == 1f, "neutral defaults " + tier);
            Assert(BoostMath.Combine(100f, 100f, 100f, tier) == expectedCaps[tier], "stacked scopes obey cap " + tier);
            Assert(BoostMath.NutritionFactor(tier, 1f, 1f, 1f, false) == 1f, "unboosted herd needs baseline feed " + tier);
            Assert(BoostMath.LoadWatts(tier, 0f, 100f, 100f, 100f, false) == 0f, "empty herd adds no operating load " + tier);
            Assert(BoostMath.NutritionFactor(tier, 2f, 1f, 1f, true) > 0f || tier == 5, "digitization cannot bypass tier " + tier);
        }
        Assert(BoostMath.Combine(2f, 3f, 4f, 5) == 7f, "scope extras add rather than multiply");
        Assert(BoostMath.Combine(float.NaN, float.PositiveInfinity, -100f, 5) == 1f, "invalid boost values are neutral");
        Assert(BoostMath.NutritionFactor(0, 2f, 1f, 1f, false) > 3f, "neolithic boost has high feed premium");
        Assert(BoostMath.LoadWatts(0, 1000f, 2f, 2f, 2f, false) == 0f && BoostMath.IdleWatts(0) == 0f, "neolithic has no power draw");
        for (var tier = 1; tier < 6; tier++)
        {
            Assert(BoostMath.NutritionFactor(tier, 2f, 2f, 2f, false) < BoostMath.NutritionFactor(tier - 1, 2f, 2f, 2f, false), "successive tiers substitute energy for feed " + tier);
            if (tier > 1)
            {
                foreach (float demand in new[] { 0f, 1f, 125f, 1000f })
                    foreach (float boost in new[] { 1f, 2f, 3f })
                    {
                        float previous = BoostMath.IdleWatts(tier - 1) + BoostMath.LoadWatts(tier - 1, demand, boost, boost, boost, false);
                        float current = BoostMath.IdleWatts(tier) + BoostMath.LoadWatts(tier, demand, boost, boost, boost, false);
                        Assert(current <= previous, "same herd and boosts never cost more at a higher powered tier " + tier);
                        if (demand > 0) Assert(current < previous, "higher powered tier is more efficient per animal " + tier);
                    }
                float cap = BoostMath.Cap(tier), previousCap = BoostMath.Cap(tier - 1);
                Assert(BoostMath.LoadWatts(tier, 125, cap, cap, cap, false) > BoostMath.LoadWatts(tier - 1, 125, previousCap, previousCap, previousCap, false), "expanded boost range permits larger maximum load " + tier);
            }
        }
        Assert(BoostMath.NutritionFactor(5, 100f, 100f, 100f, false) == 1f, "archotech boosts retain only baseline nutrition");
        Assert(BoostMath.NutritionFactor(5, 100f, 100f, 100f, true) == 0f, "digitized herds use no feed at maximum boosts");
        Assert(BoostMath.NutritionFactor(5, 1f, 1f, 1f, true) == 0f, "digitized baseline uses no feed");
        var ordinary = BoostMath.LoadWatts(5, 100f, 12f, 3f, 6f, false);
        var digital = BoostMath.LoadWatts(5, 100f, 12f, 3f, 6f, true);
        Assert(Math.Abs(digital / ordinary - 10f) < 0.0001f, "digitization pays exactly ten times electrical load");
        Assert(BoostMath.LoadWatts(5, 20f, 2f, 3f, 4f, false) == 2f * BoostMath.LoadWatts(5, 10f, 2f, 3f, 4f, false), "operating costs scale with herd demand");
        Assert(BoostMath.LoadWatts(5, 10f, 3f, 1f, 1f, false) > BoostMath.LoadWatts(5, 10f, 2f, 1f, 1f, false), "higher requested boosts cost more power");
    }

    private static void CheckRuntimeTuning()
    {
        var defaults = new BalanceTuning();
        Assert(BalanceTuning.Options.Select(o => o.Key).Distinct().Count() == BalanceTuning.Options.Count, "unique settings persistence keys");
        foreach (var option in BalanceTuning.Options)
        {
            Assert(option.Default >= option.Min && option.Default <= option.Max, "default in range: " + option.Key);
            var invalid = new BalanceTuning(new Dictionary<string, float> { [option.Key] = float.NaN });
            Assert(invalid[option.Key] == option.Default, "NaN reset: " + option.Key);
            invalid[option.Key] = float.PositiveInfinity;
            Assert(invalid[option.Key] == option.Default, "infinite reset: " + option.Key);
            invalid[option.Key] = -100000;
            Assert(invalid[option.Key] == option.Min, "lower bound: " + option.Key);
            invalid[option.Key] = float.MaxValue;
            Assert(invalid[option.Key] == option.Max, "upper bound: " + option.Key);
        }
        var tuning = defaults.Clone();
        BalanceTuning.Current = tuning;
        Assert(BoostMath.BaselineNutrition(2, 0.5f) == 1.6f, "default hunger conversion");
        tuning["baselineHunger"] = 3.2f;
        Assert(BoostMath.BaselineNutrition(2, 0.5f) == 3.2f, "baseline hunger conversion independently tunable");
        Assert(BoostMath.BaselineNutrition(0, 1) == 0.01f, "nutrition numerical floor retained");
        tuning["baselineHunger"] = 1.6f;
        tuning["nutrition"] = 0.5f;
        Assert(BoostMath.NutritionFactor(1, 1, 1, 1, false) == 0.5f, "feed scale affects baseline feeding");
        Assert(BoostMath.LoadWatts(1, 10, 1, 1, 1, false) == 500f, "feed tuning independent of power");
        tuning["activePower"] = 2;
        tuning["energy1"] = 80;
        tuning["idlePower"] = 3;
        Assert(BoostMath.LoadWatts(1, 10, 1, 1, 1, false) == 1600f, "tier and global active power");
        Assert(BoostMath.IdleWatts(1) == 300f && BoostMath.IdleWatts(0) == 0, "idle scale preserves unpowered tier");
        tuning["cap1"] = 200;
        Assert(BoostMath.Combine(100, 100, 100, 1) == 200, "configurable caps above former ceiling");
        tuning["cap1"] = 2;
        Assert(BoostMath.Sanitize(100) == 100 && BoostMath.Combine(100, 1, 1, 1) == 2, "effective cap does not erase saved requests");
        tuning["severityLinear"] = 2; tuning["severityQuadratic"] = 0.5f;
        Assert(BoostMath.Severity(2, 3, 1) == 10.5f, "custom linear and quadratic coefficients");
        tuning["digitalPower"] = 4; tuning["digitalFeed"] = 0.25f;
        Assert(BoostMath.DigitalMultiplier(5, true) == 4 && BoostMath.DigitalMultiplier(4, true) == 1, "custom digitization remains tier gated");
        Assert(BoostMath.NutritionFactor(5, 1, 1, 1, true) == 0.125f, "digitization feed fraction and feed scale");
        tuning["production"] = 3; tuning["milk"] = 2;
        Assert(BoostMath.ProductScale("milk") == 6 && BoostMath.ProductScale("wool") == 3, "global and product-specific yields combine");
        tuning["mortality"] = 0; Assert(BoostMath.MortalityFraction(30, 10) == 0, "mortality disabled");
        tuning["mortality"] = 0.5f; tuning["mortalityOnset"] = 1.5f;
        Assert(BoostMath.MortalityFraction(10, 10) == 0 && BoostMath.MortalityFraction(20, 10) == 0.25f, "mortality onset and coefficient");
        Assert(BoostMath.MortalityFraction(1000, 10) == 1, "mortality bounded");
        var restored = new BalanceTuning(tuning.Export());
        Assert(restored["production"] == 3 && restored["cap1"] == 2, "settings exported and restored");
        tuning.Reset("Power");
        Assert(tuning["activePower"] == 1 && tuning["production"] == 3, "section reset leaves other sections intact");
        tuning.Reset();
        Assert(BalanceTuning.Options.All(o => tuning[o.Key] == o.Default), "full defaults restored");
        Assert(BalanceTuning.ScaledCount(350, 0.5f) == 175 && BalanceTuning.ScaledCount(3, 0.1f) == 1 && BalanceTuning.ScaledCount(20, 0) == 0, "material scaling rounds up and supports free construction");

        // Two cohorts with unequal count/diet/boosts: nutrition-weighted cost average reconciles exactly.
        float a = 20, b = 5;
        float sa = 1 + BoostMath.Severity(2, 1, 1), sb = 1 + BoostMath.Severity(1, 3, 1);
        var power = new PowerCostBreakdown { AnimalCount = 15, BaselineNutrition = a + b, TierCoefficient = BoostMath.EnergyCoefficient(5), ActiveScale = 1, IdleScale = 1, ControllerIdleBase = BoostMath.IdleWatts(5), BoostMultiplier = (a * sa + b * sb) / (a + b), DigitalMultiplier = 10, SecondaryIdleWatts = 100 };
        float expected = (BoostMath.IdleWatts(5) + BoostMath.LoadWatts(5, a, 2, 1, 1, false) + BoostMath.LoadWatts(5, b, 1, 3, 1, false)) * 10;
        Assert(Math.Abs(power.ControllerWatts - expected) < 0.1f && power.TotalWatts == power.ControllerWatts + 100, "breakdown weighted average matches actual cohort costs and includes secondary idle");
        Assert(new PowerCostBreakdown().AverageNutrition == 0, "empty herd breakdown has no division by zero");
        float hugeRemainder = 0;
        Assert(HerdMath.ConsumeWhole(ref hugeRemainder, 1e30f) == int.MaxValue && hugeRemainder == 0, "extreme configured output cannot overflow into negatives");
        Assert(HerdMath.AddCounts(int.MaxValue, 10) == int.MaxValue, "count buffers saturate rather than wrap");
        BalanceTuning.Current = new BalanceTuning();
    }

    private static void CheckPowerMigration()
    {
        var saved = new BalanceTuning().Export();
        float[] idle = { 0, 100, 500, 1500, 5000, 15000 }, energy = { 0, 50, 150, 400, 1000, 2500 };
        for (int tier = 1; tier <= 5; tier++) { saved["idle" + tier] = idle[tier]; saved["energy" + tier] = energy[tier]; }
        saved["production"] = 3;
        var migrated = BalanceTuning.Load(saved, 0);
        var defaults = new BalanceTuning();
        for (int tier = 1; tier <= 5; tier++)
        {
            Assert(migrated["idle" + tier] == defaults["idle" + tier] && migrated["energy" + tier] == defaults["energy" + tier], "legacy defaults migrate " + tier);
        }
        Assert(migrated["production"] == 3, "unrelated tuning survives power migration");
        saved["energy4"] = 777; saved["idle3"] = 900;
        migrated = BalanceTuning.Load(saved, 0);
        Assert(migrated["energy4"] == 777 && migrated["idle3"] == 900, "custom power values preserved independently");
        var modern = BalanceTuning.Load(saved, BalanceTuning.PowerDefaultsVersion);
        Assert(modern["energy5"] == 2500 && modern["idle5"] == 15000, "versioned settings are never migrated again even when matching legacy numbers");
        Assert(BalanceTuning.Load(null, 0)["energy4"] == 20, "missing settings use efficient defaults");
        BalanceTuning.Current = new BalanceTuning();
        Assert(BoostMath.IdleWatts(3) + BoostMath.LoadWatts(3, 125, 1, 1, 1, false) == 3850, "Spacer unboosted example");
        Assert(BoostMath.IdleWatts(4) + BoostMath.LoadWatts(4, 125, 1, 1, 1, false) == 2600, "Ultra unboosted example");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new System.InvalidOperationException(message);
    }
}

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
            Assert(BoostMath.LoadWatts(tier, 10f, 2f, 2f, 2f, false) > BoostMath.LoadWatts(tier - 1, 10f, 2f, 2f, 2f, false), "energy cost rises with tier " + tier);
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

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new System.InvalidOperationException(message);
    }
}

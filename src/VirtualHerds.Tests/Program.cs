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
        System.Console.WriteLine("Virtual Herds pure self-tests passed.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new System.InvalidOperationException(message);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace VirtualHerds.Pure
{
    public sealed class TuningOption
    {
        public readonly string Key, Group, Label, Description;
        public readonly float Default, Min, Max;
        public readonly bool Whole;
        public TuningOption(string key, string group, string label, string description, float value, float min, float max, bool whole = false)
        { Key = key; Group = group; Label = label; Description = description; Default = value; Min = min; Max = max; Whole = whole; }
        public float Validate(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return Default;
            value = Math.Max(Min, Math.Min(Max, value));
            return Whole ? (float)Math.Round(value) : value;
        }
    }

    // Shared by the settings UI, persistence and the simulation; no game dependencies.
    public sealed class BalanceTuning
    {
        public const int PowerDefaultsVersion = 1;
        public static readonly string[] TierNames = { "Neolithic", "Medieval", "Industrial", "Spacer", "Ultra", "Archotech" };
        public static readonly IReadOnlyList<TuningOption> Options = BuildOptions();
        private static readonly Dictionary<string, TuningOption> ByKey = Options.ToDictionary(o => o.Key);
        public static BalanceTuning Current { get; set; } = new BalanceTuning();
        private readonly Dictionary<string, float> values = new Dictionary<string, float>();
        public BalanceTuning(IDictionary<string, float> saved = null)
        {
            foreach (var option in Options)
                values[option.Key] = saved != null && saved.TryGetValue(option.Key, out var value) ? option.Validate(value) : option.Default;
        }
        public static BalanceTuning Load(IDictionary<string, float> saved, int powerDefaultsVersion)
        {
            var result = new BalanceTuning(saved);
            if (saved == null || powerDefaultsVersion >= PowerDefaultsVersion) return result;
            var oldIdle = new[] { 0f, 100f, 500f, 1500f, 5000f, 15000f };
            var oldEnergy = new[] { 0f, 50f, 150f, 400f, 1000f, 2500f };
            for (int tier = 1; tier <= 5; tier++)
            {
                string idle = "idle" + tier, energy = "energy" + tier;
                // Upgrade only persisted legacy defaults; leave independently customized values alone.
                if (saved.TryGetValue(idle, out var watts) && watts == oldIdle[tier]) result[idle] = ByKey[idle].Default;
                if (saved.TryGetValue(energy, out var coefficient) && coefficient == oldEnergy[tier]) result[energy] = ByKey[energy].Default;
            }
            return result;
        }
        public float this[string key]
        {
            get => values[key];
            set => values[key] = ByKey[key].Validate(value);
        }
        public Dictionary<string, float> Export() => new Dictionary<string, float>(values);
        public BalanceTuning Clone() => new BalanceTuning(values);
        public void Reset(string group = null)
        { foreach (var o in Options) if (group == null || o.Group == group) values[o.Key] = o.Default; }
        public static int ScaledCount(int baseline, float scale)
            => baseline <= 0 || scale <= 0f ? 0 : Math.Max(1, (int)Math.Ceiling(Math.Min(int.MaxValue - 1d, baseline * (double)scale)));

        private static IReadOnlyList<TuningOption> BuildOptions()
        {
            var result = new List<TuningOption>();
            Action<string, string, string, string, float, float, float> add = (key, group, label, hint, value, min, max)
                => result.Add(new TuningOption(key, group, label, hint, value, min, max));
            add("production", "Simulation", "All product output", "Multiplier for milk, wool and eggs. Independent of the herd boost request and its cost.", 1, 0, 100);
            add("milk", "Simulation", "Milk yield", "Additional milk-only output multiplier.", 1, 0, 100);
            add("wool", "Simulation", "Wool yield", "Additional wool-only output multiplier.", 1, 0, 100);
            add("eggs", "Simulation", "Egg yield", "Additional egg-only output multiplier; does not change egg fertility rules.", 1, 0, 100);
            add("breeding", "Simulation", "Conception rate", "Multiplier for new pregnancies. Zero pauses conception without deleting progress.", 1, 0, 100);
            add("gestation", "Simulation", "Pregnancy speed", "Multiplier for progress of existing and future pregnancies. Zero pauses gestation.", 1, 0, 100);
            add("aging", "Simulation", "Aging speed", "Multiplier for biological aging in storage. Zero freezes aging; mortality is separate.", 1, 0, 100);
            add("mortality", "Simulation", "Old-age mortality coefficient", "Hourly death fraction = (age / life expectancy - onset) × this value, clamped to 0–1.", 0.02f, 0, 1);
            add("mortalityOnset", "Simulation", "Old-age mortality onset", "Life-expectancy fraction at which mortality starts; 1 means normal life expectancy.", 1, 0, 10);
            add("litter", "Simulation", "Litter size multiplier", "Scales the species' average live births. Fractional births accumulate; zero produces no offspring.", 1, 0, 100);
            add("femaleFraction", "Simulation", "Female birth fraction", "Deterministic female share of live births, from 0 (all male) to 1 (all female).", 0.5f, 0, 1);
            add("maleCoverage", "Simulation", "Breeding coverage per male", "Scales male contribution to conception coverage; 1 retains the original male/female ratio.", 1, 0, 100);
            add("baselineHunger", "Feeding", "Baseline hunger conversion", "Nutrition/day per unit of species hunger and life-stage factor. Also changes the baseline operating power load.", 1.6f, 0.01f, 100);
            add("nutrition", "Feeding", "Nutrition consumption", "Scales feed demand only, not operating power. Zero removes food requirements.", 1, 0, 100);
            add("reserveScale", "Feeding", "All-map reserve target scale", "Multiplies each map's chosen herd-days reserve, including existing saves.", 1, 0, 100);
            add("defaultReserve", "Feeding", "Default reserve days", "Initial reserve for newly created maps only. Existing maps keep their own reserve setting.", 5, 0, 365);
            add("refillThreshold", "Feeding", "Reserve refill threshold", "Start hauling below this fraction of the reserve target; continue until the target is met.", 0.8f, 0, 1);
            add("idlePower", "Power", "Idle power scale", "Multiplies the idle load of the controller and every secondary powered barn.", 1, 0, 100);
            add("activePower", "Power", "Active power scale", "Multiplies the controller's nutrition-based operating load; independent of the feed scale.", 1, 0, 100);
            add("severityLinear", "Power", "Boost cost linear coefficient", "Cost severity S = linear × E + quadratic × E²; E is the sum of the three boost extras.", 1, 0, 10);
            add("severityQuadratic", "Power", "Boost cost quadratic coefficient", "Curvature of extra feed and operating power costs at high boosts.", 0.1f, 0, 10);
            add("digitalPower", "Power", "Digitization power multiplier", "Multiplies the Archotech controller's full energy load while digitized.", 10, 0, 100);
            add("digitalFeed", "Power", "Digitization feed fraction", "Fraction of normal feed demand retained while digitized. Default 0 makes digitization food-free.", 0, 0, 1);
            var caps = new[] { 2f, 3f, 6f, 12f, 25f, 100f };
            var feed = new[] { 2f, 1.5f, 1f, 0.5f, 0.2f, 0f };
            var idle = new[] { 0f, 100f, 100f, 100f, 100f, 100f };
            var energy = new[] { 0f, 50f, 40f, 30f, 20f, 10f };
            for (var tier = 0; tier < 6; tier++)
            {
                string name = TierNames[tier];
                add("cap" + tier, "Tiers", name + " boost cap", "Maximum combined request per boosted stat; lowering it preserves higher saved requests.", caps[tier], 1, 1000);
                add("feed" + tier, "Tiers", name + " feed premium", "Extra nutrition coefficient: normal feed × (1 + premium × S).", feed[tier], 0, 100);
                if (tier > 0)
                {
                    add("idle" + tier, "Tiers", name + " idle watts", "Base watts per powered barn before the global idle/digitization multipliers.", idle[tier], 0, 1000000);
                    add("energy" + tier, "Tiers", name + " operating coefficient", "Watts per baseline nutrition/day × (1 + S), before global active/digitization multipliers.", energy[tier], 0, 100000);
                }
                add("materials" + tier, "Economy", name + " construction materials", "Scales each original material count, rounded up. Deferred while this barn type has active construction/upgrades.", 1, 0, 100);
                add("work" + tier, "Economy", name + " construction work", "Scales original Work to build. Deferred together with materials to protect active construction.", 1, 0.01f, 100);
                add("research" + tier, "Economy", name + " research cost", "Scales original research points. Existing absolute research progress is retained.", 1, 0.01f, 100);
            }
            add("research6", "Economy", "Full digitization research cost", "Scales original research points. Existing absolute research progress is retained.", 1, 0.01f, 100);
            result.Add(new TuningOption("reconcileTicks", "Advanced", "Reconcile check interval (ticks)", "How often power/topology and dirty intake/limits are checked. Does not change simulation speed.", 60, 1, 2500, true));
            result.Add(new TuningOption("fallbackHours", "Advanced", "Full reconciliation interval (hours)", "Fallback intake/limit reconciliation for changes without a notification.", 4, 1, 168, true));
            add("haulRadius", "Advanced", "Hauling search radius (cells)", "Maximum search distance for the right-click haul order and barn upgrade materials.", 9999, 1, 9999);
            result.Add(new TuningOption("releaseRadius", "Advanced", "Nearby release radius (cells)", "Search nearby first when releasing animals; a full-map safe-cell fallback remains.", 8, 1, 100, true));
            result.Add(new TuningOption("outputStackBudget", "Advanced", "Output stacks per herd hour", "Limits physical output spawning per map per hour; excess products remain saved in the pending buffer.", 256, 1, 10000, true));
            add("upgradeXp", "Advanced", "Upgrade construction XP per tick", "Construction skill experience while actively working on a barn upgrade.", 0.08f, 0, 10);
            return result.AsReadOnly();
        }
    }
}

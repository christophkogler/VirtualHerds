using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using VirtualHerds.Pure;

namespace VirtualHerds
{
    // Called only on the game thread, after all definition patches have finished.
    public static class RuntimeDefinitionTuning
    {
        private sealed class BarnBaseline
        {
            public ThingDef Def;
            public int Tier;
            public List<ThingDefCountClass> Costs;
            public int Stuff;
            public float Work;
            public float AppliedMaterials = 1f;
            public float AppliedWork = 1f;
        }

        private static readonly List<BarnBaseline> barns = new List<BarnBaseline>();
        private static readonly Dictionary<ResearchProjectDef, float> research = new Dictionary<ResearchProjectDef, float>();
        private static readonly HashSet<ThingDef> pending = new HashSet<ThingDef>();
        private static readonly string[] researchNames =
        {
            "VirtualHerds_Pastoralism", "VirtualHerds_ManagedHusbandry", "VirtualHerds_IntensiveHusbandry",
            "VirtualHerds_AcceleratedBioculture", "VirtualHerds_SyntheticEcosystems", "VirtualHerds_VirtualHerds",
            "VirtualHerds_Digitization"
        };
        private static bool initialized;

        public static string Status => !initialized ? "Waiting for game definitions."
            : pending.Count == 0 ? "All settings applied."
            : Current.Game == null ? "Economy settings waiting for a colony to load."
            : pending.Count + " barn type(s) pending completion or cancellation of construction/upgrades.";

        public static void Initialize()
        {
            if (initialized) return;
            foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (!def.defName.StartsWith("VirtualHerds_", StringComparison.Ordinal)
                    || def.thingClass != typeof(Building_VirtualBarn)) continue;
                var tier = def.GetModExtension<BarnTierExtension>()?.tier ?? 0;
                if (tier < 0 || tier > 5) continue;
                barns.Add(new BarnBaseline
                {
                    Def = def, Tier = tier,
                    Costs = CloneCosts(def.costList), Stuff = def.costStuffCount,
                    Work = def.statBases?.FirstOrDefault(s => s.stat == StatDefOf.WorkToMake)?.value
                        ?? def.GetStatValueAbstract(StatDefOf.WorkToMake)
                });
            }
            foreach (var name in researchNames)
            {
                var def = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(name);
                if (def != null) research.Add(def, def.baseCost);
            }
            initialized = true;
            Apply();
        }

        public static void Apply()
        {
            if (!initialized) return;
            foreach (var pair in research)
            {
                var tier = Array.IndexOf(researchNames, pair.Key.defName);
                // Preserve unfinished progress, and never revoke already completed research.
                bool finished = Current.Game != null && pair.Key.IsFinished;
                pair.Key.baseCost = pair.Value * BalanceTuning.Current["research" + tier];
                if (finished && !pair.Key.IsFinished) Find.ResearchManager.FinishProject(pair.Key, false);
            }
            ApplyBarns(false);
        }

        public static void RetryPending()
        {
            if (initialized && pending.Count > 0) ApplyBarns(true);
        }

        public static Dictionary<string, float> ExportAppliedScales()
        {
            var result = new Dictionary<string, float>();
            foreach (var baseline in barns)
            {
                result[baseline.Def.defName + ".materials"] = baseline.AppliedMaterials;
                result[baseline.Def.defName + ".work"] = baseline.AppliedWork;
            }
            return result;
        }

        // Each save records the definitions under which its construction was started.
        // Restore before jobs resume, including defaults for saves predating this feature.
        public static void RestoreForSave(IDictionary<string, float> saved)
        {
            if (!initialized) Initialize();
            pending.Clear();
            foreach (var baseline in barns)
            {
                float materials = SavedScale(saved, baseline.Def.defName + ".materials", 0f);
                float work = SavedScale(saved, baseline.Def.defName + ".work", 0.01f);
                WriteBarn(baseline, materials, work);
                if (materials != BalanceTuning.Current["materials" + baseline.Tier]
                    || work != BalanceTuning.Current["work" + baseline.Tier])
                    pending.Add(baseline.Def);
            }
            ClearStatCaches();
        }

        public static List<string> CompletedResearch()
            => research.Keys.Where(r => Current.Game != null && r.IsFinished).Select(r => r.defName).ToList();

        public static void RestoreFinishedResearch(List<string> completed)
        {
            if (Current.Game == null) return;
            foreach (var pair in research)
            {
                // Pre-settings saves had the original costs; newer saves record completion explicitly.
                bool wasFinished = completed == null ? Find.ResearchManager.GetProgress(pair.Key) >= pair.Value
                    : completed.Contains(pair.Key.defName);
                if (wasFinished && !pair.Key.IsFinished) Find.ResearchManager.FinishProject(pair.Key, false);
            }
        }

        private static float SavedScale(IDictionary<string, float> saved, string key, float minimum)
        {
            if (saved == null || !saved.TryGetValue(key, out var value)
                || float.IsNaN(value) || float.IsInfinity(value)) return 1f;
            return Math.Max(minimum, Math.Min(100f, value));
        }

        private static void ApplyBarns(bool pendingOnly)
        {
            var blocked = ConstructionTargets();
            bool changed = false;
            foreach (var baseline in barns)
            {
                if (pendingOnly && !pending.Contains(baseline.Def)) continue;
                float materials = BalanceTuning.Current["materials" + baseline.Tier];
                float work = Math.Max(0.01f, BalanceTuning.Current["work" + baseline.Tier]);
                if (materials == baseline.AppliedMaterials && work == baseline.AppliedWork)
                {
                    pending.Remove(baseline.Def);
                    continue;
                }
                // Defer the pair, so materials already delivered and work already performed
                // retain the exact same meaning until every active target is gone.
                if (Current.Game == null || blocked.Contains(baseline.Def))
                {
                    pending.Add(baseline.Def);
                    continue;
                }
                WriteBarn(baseline, materials, work);
                pending.Remove(baseline.Def);
                changed = true;
            }
            // Costs also feed derived stats (including market value), not just WorkToMake.
            if (changed) ClearStatCaches();
        }

        private static void WriteBarn(BarnBaseline baseline, float materials, float work)
        {
            baseline.Def.costList = baseline.Costs == null ? null : baseline.Costs
                .Select(c => new ThingDefCountClass(c.thingDef, BalanceTuning.ScaledCount(c.count, materials)))
                .Where(c => c.count > 0).ToList();
            baseline.Def.costStuffCount = BalanceTuning.ScaledCount(baseline.Stuff, materials);
            if (baseline.Def.statBases == null) baseline.Def.statBases = new List<StatModifier>();
            var workStat = baseline.Def.statBases.FirstOrDefault(s => s.stat == StatDefOf.WorkToMake);
            if (workStat == null)
            {
                workStat = new StatModifier { stat = StatDefOf.WorkToMake };
                baseline.Def.statBases.Add(workStat);
            }
            workStat.value = baseline.Work * work;
            baseline.AppliedMaterials = materials;
            baseline.AppliedWork = work;
        }

        private static void ClearStatCaches()
        {
            foreach (var stat in DefDatabase<StatDef>.AllDefsListForReading)
                stat.Worker.TryClearCache();
        }

        private static HashSet<ThingDef> ConstructionTargets()
        {
            var result = new HashSet<ThingDef>();
            if (Current.Game == null) return result;
            foreach (var map in Find.Maps)
                foreach (var thing in map.listerThings.AllThings)
                {
                    if ((thing is Blueprint || thing is Frame) && thing.def.entityDefToBuild is ThingDef target)
                        result.Add(target);
                    if (thing is Building_VirtualBarn barn && barn.UpgradeTarget != null)
                        result.Add(barn.UpgradeTarget);
                }
            return result;
        }

        private static List<ThingDefCountClass> CloneCosts(List<ThingDefCountClass> costs)
            => costs?.Select(c => new ThingDefCountClass(c.thingDef, c.count)).ToList();
    }
}

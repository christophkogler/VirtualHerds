using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using VirtualHerds.Pure;

namespace VirtualHerds
{
    public sealed class VirtualHerdMapComponent : MapComponent
    {
        private const int HourTicks = 2500;
        private const int ReconcileCheckTicks = 60;
        private const int AgeBins = 16;
        private const int GestationBins = 16;
        private const float FeedReserveRefillThreshold = 0.8f;
        private List<VirtualHerdCohort> cohorts = new List<VirtualHerdCohort>();
        private List<VirtualHerdLimitEntry> limits = new List<VirtualHerdLimitEntry>();
        private List<VirtualHerdOutput> pendingOutputs = new List<VirtualHerdOutput>();
        private List<VirtualHerdSpeciesState> speciesStates = new List<VirtualHerdSpeciesState>();
        private float reserveDays = 5f;
        private int lastPawnCount = -1;
        private bool dirty = true;
        private int lastSimulationTick = -1;

        public VirtualHerdMapComponent(Map map) : base(map) { }
        public IReadOnlyList<VirtualHerdCohort> Cohorts => cohorts;
        public float ReserveDays => reserveDays;
        public bool HasBarns => Barns().Count > 0;
        public int StoredCount => cohorts.Sum(cohort => Math.Max(0, cohort.count));

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref cohorts, "cohorts", LookMode.Deep);
            Scribe_Collections.Look(ref limits, "limits", LookMode.Deep);
            Scribe_Collections.Look(ref pendingOutputs, "pendingOutputs", LookMode.Deep);
            Scribe_Collections.Look(ref speciesStates, "speciesStates", LookMode.Deep);
            Scribe_Values.Look(ref reserveDays, "reserveDays", 5f);
            if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
            cohorts = cohorts ?? new List<VirtualHerdCohort>();
            limits = limits ?? new List<VirtualHerdLimitEntry>();
            pendingOutputs = pendingOutputs ?? new List<VirtualHerdOutput>();
            speciesStates = speciesStates ?? new List<VirtualHerdSpeciesState>();
            reserveDays = Mathf.Clamp(reserveDays, 0f, 365f);
            cohorts.RemoveAll(cohort => cohort == null || cohort.count <= 0 || cohort.Def == null);
            limits.RemoveAll(entry => entry == null || string.IsNullOrEmpty(entry.defName));
            pendingOutputs.RemoveAll(output => output == null || output.count <= 0 || DefDatabase<ThingDef>.GetNamedSilentFail(output.defName) == null);
            speciesStates.RemoveAll(state => state == null || string.IsNullOrEmpty(state.defName));
            dirty = true;
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (Find.TickManager == null) return;
            var tick = Find.TickManager.TicksGame;
            var reconcileCheckDue = tick % ReconcileCheckTicks == 0;
            var simulationDue = tick % HourTicks == 0 && tick != lastSimulationTick;
            var fallbackReconcileDue = tick % (HourTicks * 4) == 0;
            if (!reconcileCheckDue && !simulationDue && !fallbackReconcileDue) return;
            if (!HasBarns)
            {
                if (reconcileCheckDue && cohorts.Count > 0) ReleaseAll(map.Center);
                return;
            }
            if (reconcileCheckDue)
            {
                var pawnCount = map.mapPawns?.AllPawnsSpawned?.Count ?? 0;
                if (pawnCount != lastPawnCount) { lastPawnCount = pawnCount; dirty = true; }
            }
            var dirtyReconcileDue = dirty && reconcileCheckDue;
            if (dirtyReconcileDue || fallbackReconcileDue)
            {
                Reconcile();
                dirty = false;
            }
            if (!simulationDue) return;
            lastSimulationTick = tick;
            SimulateHour();
        }

        public override void MapRemoved()
        {
            if (cohorts.Count > 0) Log.Warning("[Virtual Herds] Map removed with stored animals; discarding stored cohorts because no map remains for safe release.");
            cohorts.Clear();
            pendingOutputs.Clear();
            speciesStates.Clear();
            base.MapRemoved();
        }

        public VirtualHerdLimitEntry LimitFor(ThingDef def)
        {
            if (def == null) return null;
            var entry = limits.FirstOrDefault(item => item.defName == def.defName);
            if (entry == null) { entry = new VirtualHerdLimitEntry { defName = def.defName }; limits.Add(entry); }
            return entry;
        }

        public void SetReserveDays(float value)
        {
            value = Mathf.Clamp(value, 0f, 365f);
            if (Mathf.Abs(reserveDays - value) > 0.0001f) { reserveDays = value; dirty = true; }
        }

        public void MarkDirty() { dirty = true; }
        public void NotifyPawnTopologyChanged() { dirty = true; }

        public HerdCounts StoredCounts(ThingDef def)
        {
            var counts = new HerdCounts();
            foreach (var cohort in cohorts.Where(item => item.Def == def)) counts.Add(CategoryFor(cohort), cohort.count);
            return counts;
        }

        public HerdCounts MaterializedCounts(ThingDef def)
        {
            var counts = new HerdCounts();
            if (map.mapPawns == null) return counts;
            foreach (var pawn in map.mapPawns.AllPawnsSpawned.Where(pawn => pawn.def == def && VirtualHerdUtility.IsEligible(pawn)))
            {
                float progress;
                counts.Add(HerdMath.CategoryFor(VirtualHerdUtility.SexOf(pawn), pawn.ageTracker.CurLifeStage.reproductive, VirtualHerdUtility.IsPregnant(pawn, out progress)), 1);
            }
            return counts;
        }

        public int ExemptCount(ThingDef def)
        {
            return map.mapPawns == null ? 0 : map.mapPawns.AllPawnsSpawned.Count(pawn => pawn.def == def && pawn.RaceProps?.Animal == true && pawn.Faction == Faction.OfPlayer && !VirtualHerdUtility.IsEligible(pawn));
        }

        public float FoodNutrition => Barns().Sum(barn => barn.FoodInventory.InnerListForReading.Where(food => VirtualHerdUtility.IsNutritionFoodDef(food.def))
            .Where(food => barn.FoodFilter == null || barn.FoodFilter.Allows(food)).Sum(food => food.GetStatValue(StatDefOf.Nutrition) * food.stackCount));
        public float RequiredReserveNutrition => cohorts.Sum(cohort => VirtualHerdUtility.NutritionPerDay(cohort.Def, cohort.lifeStageIndex) * cohort.count) * reserveDays;
        public bool NeedsFoodHauling
        {
            get
            {
                var needsFood = false;
                foreach (var group in cohorts.Where(cohort => cohort.Def != null).GroupBy(cohort => cohort.defName))
                {
                    var def = group.First().Def;
                    var state = SpeciesState(group.Key);
                    var dailyNeed = group.Sum(cohort => VirtualHerdUtility.NutritionPerDay(def, cohort.lifeStageIndex) * cohort.count);
                    var required = dailyNeed * reserveDays;
                    var available = state.nutritionCredit + Barns().Sum(barn => barn.FoodInventory.InnerListForReading
                        .Where(food => VirtualHerdUtility.IsCompatibleFood(def, food.def) && (barn.FoodFilter == null || barn.FoodFilter.Allows(food)))
                        .Sum(food => food.GetStatValue(StatDefOf.Nutrition) * food.stackCount));
                    if (reserveDays <= 0f || required <= 0f)
                        state.needsFoodHauling = false;
                    else if (state.needsFoodHauling)
                        state.needsFoodHauling = available + 0.01f < required;
                    else
                        state.needsFoodHauling = available + 0.01f < required * FeedReserveRefillThreshold;
                    needsFood |= state.needsFoodHauling;
                }
                return needsFood;
            }
        }
        public bool FoodCompatibleWithStoredSpecies(ThingDef foodDef) => cohorts.Any(cohort => VirtualHerdUtility.IsCompatibleFood(cohort.Def, foodDef));

        public void NotifyBarnRemoved(Building_VirtualBarn removed)
        {
            var remaining = Barns().Where(barn => barn != removed && !barn.Destroyed).ToList();
            if (remaining.Count == 0 && cohorts.Count > 0) ReleaseAll(removed?.Position ?? map.Center);
        }

        public void ConfirmDiscardStoredAnimals()
        {
            if (StoredCount <= 0) { Messages.Message("Virtual Herds has no stored animals on this map.", MessageTypeDefOf.NeutralEvent, false); return; }
            var count = StoredCount;
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("Discard " + count + " stored virtual animals? This cannot be undone and is intended for confirmed map abandonment.", () =>
            {
                cohorts.Clear(); pendingOutputs.Clear(); speciesStates.Clear();
                Messages.Message("Discarded " + count + " stored virtual animals.", MessageTypeDefOf.NegativeEvent, false);
            }, true));
        }

        private List<Building_VirtualBarn> Barns()
        {
            return map?.listerBuildings?.AllBuildingsColonistOfClass<Building_VirtualBarn>()?.Where(barn => barn != null && !barn.Destroyed).ToList() ?? new List<Building_VirtualBarn>();
        }

        private void Reconcile()
        {
            var defs = new HashSet<ThingDef>(cohorts.Where(cohort => cohort.Def != null).Select(cohort => cohort.Def));
            if (map.mapPawns != null) foreach (var pawn in map.mapPawns.AllPawnsSpawned.Where(pawn => pawn.RaceProps?.Animal == true && pawn.Faction == Faction.OfPlayer)) defs.Add(pawn.def);
            foreach (var def in defs.Where(item => item != null).OrderBy(item => item.defName).ToList())
            {
                if (VirtualHerdsMod.Instance == null || !VirtualHerdsMod.Instance.IsSpeciesEnabled(def)) ReleaseForSpecies(def, int.MaxValue, null);
                else EnforceLimits(def);
            }
            var candidates = map.mapPawns?.AllPawnsSpawned?.Where(pawn => VirtualHerdsMod.Instance != null && VirtualHerdsMod.Instance.IsSpeciesEnabled(pawn.def) && VirtualHerdUtility.IsEligible(pawn))
                .OrderByDescending(pawn => pawn.ageTracker.AgeBiologicalTicks).ThenBy(pawn => pawn.def.defName).ThenBy(pawn => pawn.gender).ThenBy(pawn => pawn.thingIDNumber).ToList();
            if (candidates == null) return;
            foreach (var pawn in candidates) TryIntake(pawn);
        }

        private void TryIntake(Pawn pawn)
        {
            if (!VirtualHerdUtility.IsEligible(pawn)) return;
            var cohort = VirtualHerdUtility.MakeCohort(pawn);
            if (VirtualHerdUtility.PawnKindFor(cohort.pawnKindDefName, cohort.Def) == null) return;
            if (!HerdMath.CanAbsorb(LimitFor(cohort.Def).ToLimits(), StoredCounts(cohort.Def), CategoryFor(cohort))) return;
            VirtualHerdUtility.EndOrdinaryJobForCommit(pawn);
            if (!VirtualHerdUtility.IsEligible(pawn)) return;
            try
            {
                var existing = cohorts.FirstOrDefault(item => SameKey(item, cohort) && item.pawnKindDefName == cohort.pawnKindDefName);
                if (existing == null) cohorts.Add(cohort); else existing.count += 1;
                try
                {
                    pawn.Destroy(DestroyMode.Vanish);
                }
                catch
                {
                    if (existing == null) cohorts.Remove(cohort); else existing.count -= 1;
                    throw;
                }
            }
            catch (Exception ex) { Log.Warning("[Virtual Herds] Intake failed without destroying pawn: " + ex.Message); dirty = true; }
        }

        private void EnforceLimits(ThingDef def)
        {
            var limit = LimitFor(def).ToLimits();
            foreach (var category in Enum.GetValues(typeof(HerdCategory)).Cast<HerdCategory>())
            {
                var cap = HerdMath.LimitFor(limit, category);
                if (cap < 0) continue;
                var excess = StoredCounts(def).For(category) - cap;
                if (excess > 0) ReleaseForSpecies(def, excess, category);
            }
            var totalExcess = StoredCounts(def).Total - limit.Total;
            if (limit.Total >= 0 && totalExcess > 0) ReleaseForSpecies(def, totalExcess, null);
        }

        private void ReleaseForSpecies(ThingDef def, int amount, HerdCategory? category)
        {
            if (def == null || amount <= 0) return;
            var remaining = amount;
            var origin = Barns().FirstOrDefault()?.Position ?? map.Center;
            foreach (var cohort in cohorts.Where(item => item.Def == def && (!category.HasValue || CategoryFor(item) == category.Value)).OrderByDescending(AgeScore).ThenBy(item => item.sex).ThenBy(item => item.gestationBin).ToList())
            {
                if (remaining <= 0) break;
                var request = Math.Min(remaining, cohort.count);
                remaining -= TryRelease(cohort, request, origin);
            }
            cohorts.RemoveAll(cohort => cohort.count <= 0);
        }

        private void ReleaseAll(IntVec3 origin)
        {
            foreach (var cohort in cohorts.ToList()) TryRelease(cohort, cohort.count, origin);
            cohorts.RemoveAll(cohort => cohort.count <= 0);
        }

        private int TryRelease(VirtualHerdCohort cohort, int count, IntVec3 origin)
        {
            if (cohort?.Def == null || count <= 0 || (cohort.Pregnant && cohort.Sex != HerdSex.Female)) return 0;
            var kind = VirtualHerdUtility.PawnKindFor(cohort.pawnKindDefName, cohort.Def);
            if (kind == null || !cohort.Def.race.Animal) return 0;
            var released = 0;
            var originalCount = cohort.count;
            for (; released < count; released++)
            {
                if (!TryFindSpawnCell(origin, cohort.Def, out var cell)) break;
                Pawn pawn = null;
                try
                {
                    var age = ApproximateAge(cohort);
                    var request = new PawnGenerationRequest(kind, Faction.OfPlayer, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true,
                        canGeneratePawnRelations: false, allowPregnant: cohort.Pregnant && cohort.Sex == HerdSex.Female, fixedBiologicalAge: age, fixedChronologicalAge: age,
                        fixedGender: VirtualHerdUtility.ToVerseGender(cohort.Sex), developmentalStages: VirtualHerdUtility.DevelopmentalStagesFor(cohort.Def, cohort.lifeStageIndex), forceNoGear: true);
                    pawn = PawnGenerator.GeneratePawn(request);
                    if (pawn == null) break;
                    pawn.Name = null;
                    if (cohort.Pregnant && cohort.Sex == HerdSex.Female) SetPregnancyProgress(pawn, cohort);
                    GenSpawn.Spawn(pawn, cell, map);
                    cohort.count -= 1;
                }
                catch (Exception ex)
                {
                    if (pawn != null && !pawn.Spawned) pawn.Destroy(DestroyMode.Vanish);
                    Log.Warning("[Virtual Herds] Release stopped after " + released + " pawns: " + ex.Message);
                    break;
                }
            }
            if (released > 0 && originalCount > 0)
            {
                var retainedFraction = cohort.count / (float)originalCount;
                cohort.conceptionRemainder *= retainedFraction;
                cohort.birthRemainder *= retainedFraction;
                cohort.femaleBirthRemainder *= retainedFraction;
                cohort.mortalityRemainder *= retainedFraction;
                cohort.milkAccumulator *= retainedFraction;
                cohort.woolAccumulator *= retainedFraction;
                cohort.eggAccumulator *= retainedFraction;
            }
            return released;
        }

        private bool TryFindSpawnCell(IntVec3 origin, ThingDef def, out IntVec3 result)
        {
            if (CellFinder.TryFindRandomCellNear(origin, map, 8, cell => GenSpawn.CanSpawnAt(def, cell, map), out result)) return true;
            for (var x = 0; x < map.Size.x; x++) for (var z = 0; z < map.Size.z; z++)
            {
                var cell = new IntVec3(x, 0, z);
                if (GenSpawn.CanSpawnAt(def, cell, map)) { result = cell; return true; }
            }
            result = IntVec3.Invalid;
            return false;
        }

        private static void SetPregnancyProgress(Pawn pawn, VirtualHerdCohort cohort)
        {
            if (pawn == null || cohort.Sex != HerdSex.Female || !cohort.Pregnant) return;
            try
            {
                var pregnancy = pawn.health?.hediffSet?.GetFirstHediffOfDef(HediffDefOf.Pregnant) as Hediff_Pregnant;
                if (pregnancy == null) { pregnancy = HediffMaker.MakeHediff(HediffDefOf.Pregnant, pawn) as Hediff_Pregnant; if (pregnancy != null) pawn.health.AddHediff(pregnancy); }
                AccessTools.PropertySetter(typeof(Hediff_Pregnant), "GestationProgress")?.Invoke(pregnancy, new object[] { Mathf.Clamp01((cohort.gestationBin + 0.5f) / GestationBins) });
            }
            catch (Exception ex) { Log.Warning("[Virtual Herds] Pregnancy restoration skipped: " + ex.Message); }
        }

        private static float ApproximateAge(VirtualHerdCohort cohort)
        {
            var def = cohort.Def;
            if (def?.race?.lifeStageAges == null || def.race.lifeStageAges.Count == 0) return 1f;
            var index = Math.Max(0, Math.Min(def.race.lifeStageAges.Count - 1, cohort.lifeStageIndex));
            var stage = def.race.lifeStageAges[index];
            var next = VirtualHerdUtility.NextStageMinAge(def, index);
            return Math.Max(0.01f, stage.minAge + (next - stage.minAge) * ((cohort.ageProgressBin + 0.5f) / AgeBins));
        }

        private void SimulateHour()
        {
            var feedRatios = ConsumeNutritionBySpecies();
            foreach (var cohort in cohorts.ToList())
            {
                if (cohort.Def == null || cohort.count <= 0) continue;
                AdvanceAge(cohort); ApplyOldAgeMortality(cohort);
                var ratio = feedRatios.TryGetValue(cohort.defName, out var value) ? value : 0f;
                AdvancePregnancy(cohort, ratio); Produce(cohort, ratio);
            }
            StartPregnancies(feedRatios);
            foreach (var def in cohorts.Select(cohort => cohort.Def).Where(def => def != null).Distinct().ToList()) EnforceLimits(def);
            cohorts.RemoveAll(cohort => cohort.count <= 0 || cohort.Def == null);
            MergeEquivalentCohorts(); FlushPendingOutputs();
        }

        private Dictionary<string, float> ConsumeNutritionBySpecies()
        {
            var result = new Dictionary<string, float>();
            var species = cohorts.Where(cohort => cohort.Def != null).GroupBy(cohort => cohort.defName).Select(group => group.First()).OrderBy(cohort => cohort.defName).ToList();
            var demands = new Dictionary<string, float>();
            var totalDemand = new Dictionary<string, float>();
            foreach (var cohort in species)
            {
                var demand = cohorts.Where(item => item.defName == cohort.defName).Sum(item => VirtualHerdUtility.NutritionPerDay(item.Def, item.lifeStageIndex) * item.count) / 24f;
                totalDemand[cohort.defName] = demand;
                var state = SpeciesState(cohort.defName);
                var fromCredit = Math.Min(state.nutritionCredit, demand);
                state.nutritionCredit -= fromCredit;
                demands[cohort.defName] = Math.Max(0f, demand - fromCredit);
                result[cohort.defName] = demand <= 0f ? 1f : fromCredit / demand;
            }
            var foods = Barns().SelectMany(barn => barn.FoodInventory.InnerListForReading.Select(food => new { barn, food }))
                .Where(pair => pair.food != null && (pair.barn.FoodFilter == null || pair.barn.FoodFilter.Allows(pair.food)) && pair.food.GetStatValue(StatDefOf.Nutrition) > 0f)
                .Where(pair => species.Any(cohort => VirtualHerdUtility.IsCompatibleFood(cohort.Def, pair.food.def)))
                .OrderBy(pair => pair.food.TryGetComp<CompRottable>()?.TicksUntilRotAtCurrentTemp ?? int.MaxValue)
                .ThenBy(pair => pair.food.def.defName).ThenBy(pair => pair.food.thingIDNumber).ToList();
            foreach (var pair in foods)
            {
                var nutrient = pair.food.GetStatValue(StatDefOf.Nutrition);
                while (pair.barn.FoodInventory.Contains(pair.food) && pair.food.stackCount > 0)
                {
                    var eligible = species.Where(cohort => demands[cohort.defName] > 0f && VirtualHerdUtility.IsCompatibleFood(cohort.Def, pair.food.def))
                        .OrderByDescending(cohort => demands[cohort.defName] / Math.Max(0.0001f, totalDemand[cohort.defName])).ThenBy(cohort => cohort.defName).ToList();
                    if (eligible.Count == 0) break;
                    var selected = eligible[0];
                    var taken = pair.barn.FoodInventory.Take(pair.food, 1);
                    if (taken == null) break;
                    var used = Math.Min(nutrient, demands[selected.defName]);
                    demands[selected.defName] -= used;
                    result[selected.defName] += used / Math.Max(0.0001f, totalDemand[selected.defName]);
                    SpeciesState(selected.defName).nutritionCredit += nutrient - used;
                    taken.Destroy(DestroyMode.Vanish);
                }
            }
            foreach (var name in result.Keys.ToList()) result[name] = Mathf.Clamp01(result[name]);
            return result;
        }

        private VirtualHerdSpeciesState SpeciesState(string defName)
        {
            var state = speciesStates.FirstOrDefault(item => item.defName == defName);
            if (state == null) { state = new VirtualHerdSpeciesState { defName = defName }; speciesStates.Add(state); }
            return state;
        }

        private void AdvanceAge(VirtualHerdCohort cohort)
        {
            var def = cohort.Def; var index = Math.Max(0, Math.Min(def.race.lifeStageAges.Count - 1, cohort.lifeStageIndex));
            var durationDays = Math.Max(0.1f, (VirtualHerdUtility.NextStageMinAge(def, index) - def.race.lifeStageAges[index].minAge) * 60f);
            cohort.ageAccumulator += 1f / (durationDays * 24f);
            while (cohort.ageAccumulator >= 1f / AgeBins)
            {
                cohort.ageAccumulator -= 1f / AgeBins;
                if (cohort.ageProgressBin < AgeBins - 1) cohort.ageProgressBin++;
                else if (index + 1 < def.race.lifeStageAges.Count) { index++; cohort.lifeStageIndex = index; cohort.ageProgressBin = 0; }
            }
        }

        private static void ApplyOldAgeMortality(VirtualHerdCohort cohort)
        {
            var def = cohort.Def;
            if (def?.race == null || def.race.lifeExpectancy <= 0f) return;
            var index = Math.Max(0, Math.Min(def.race.lifeStageAges.Count - 1, cohort.lifeStageIndex));
            var age = def.race.lifeStageAges[index].minAge + (VirtualHerdUtility.NextStageMinAge(def, index) - def.race.lifeStageAges[index].minAge) * ((cohort.ageProgressBin + 0.5f) / AgeBins);
            if (age <= def.race.lifeExpectancy) return;
            var deaths = HerdMath.ConsumeWhole(ref cohort.mortalityRemainder, cohort.count * Mathf.Clamp01((age / def.race.lifeExpectancy - 1f) * 0.02f));
            cohort.count = Math.Max(0, cohort.count - Math.Min(cohort.count, deaths));
        }

        private void AdvancePregnancy(VirtualHerdCohort cohort, float feedRatio)
        {
            if (!cohort.Pregnant || cohort.Sex != HerdSex.Female || feedRatio <= 0f) return;
            var days = Math.Max(0.1f, cohort.Def.race.gestationPeriodDays);
            cohort.gestationAccumulator += feedRatio / (days * 24f);
            while (cohort.gestationAccumulator >= 1f / GestationBins) { cohort.gestationAccumulator -= 1f / GestationBins; cohort.gestationBin++; }
            if (cohort.gestationBin < GestationBins) return;
            var count = cohort.count;
            var mother = cohorts.FirstOrDefault(item => item != cohort && item.defName == cohort.defName && item.Sex == HerdSex.Female && item.lifeStageIndex == cohort.lifeStageIndex && item.ageProgressBin == cohort.ageProgressBin && !item.Pregnant);
            if (mother == null)
            {
                mother = new VirtualHerdCohort { defName = cohort.defName, pawnKindDefName = cohort.pawnKindDefName, sex = HerdSex.Female, lifeStageIndex = cohort.lifeStageIndex, ageProgressBin = cohort.ageProgressBin, gestationBin = -1 };
                cohorts.Add(mother);
            }
            mother.count += count;
            mother.birthRemainder += cohort.birthRemainder;
            var births = HerdMath.ConsumeWhole(ref mother.birthRemainder, count * LitterAverage(cohort.Def));
            AddOffspring(cohort.Def, cohort.pawnKindDefName, births, ref mother.femaleBirthRemainder);
            cohort.count = 0;
        }

        private void StartPregnancies(Dictionary<string, float> feedRatios)
        {
            foreach (var def in cohorts.Select(cohort => cohort.Def).Where(def => def != null).Distinct().ToList())
            {
                if (!feedRatios.TryGetValue(def.defName, out var ratio) || ratio <= 0f || def.race.gestationPeriodDays <= 0f || VirtualHerdUtility.EggLayer(def) != null) continue;
                var males = cohorts.Where(item => item.Def == def && !item.Pregnant && item.Sex == HerdSex.Male && VirtualHerdUtility.IsAdult(def, item.lifeStageIndex)).Sum(item => item.count);
                if (males <= 0) continue;
                foreach (var female in cohorts.Where(item => item.Def == def && !item.Pregnant && item.Sex == HerdSex.Female && VirtualHerdUtility.IsAdult(def, item.lifeStageIndex)).ToList())
                {
                    var rate = ratio / (Math.Max(1f, def.race.gestationPeriodDays) * 24f) * Math.Min(1f, males / (float)Math.Max(1, female.count));
                    var amount = Math.Min(female.count, HerdMath.ConsumeWhole(ref female.conceptionRemainder, female.count * rate));
                    if (amount <= 0) continue;
                    female.count -= amount;
                    var pregnant = cohorts.FirstOrDefault(item => item.Def == def && item.Sex == HerdSex.Female && item.lifeStageIndex == female.lifeStageIndex && item.ageProgressBin == female.ageProgressBin && item.gestationBin == 0);
                    if (pregnant == null)
                    {
                        pregnant = new VirtualHerdCohort { defName = def.defName, pawnKindDefName = female.pawnKindDefName, sex = HerdSex.Female, lifeStageIndex = female.lifeStageIndex, ageProgressBin = female.ageProgressBin, gestationBin = 0 };
                        cohorts.Add(pregnant);
                    }
                    pregnant.count += amount;
                }
            }
        }

        private static float LitterAverage(ThingDef def) => def?.race?.litterSizeCurve == null ? 1f : Math.Max(1f, def.race.litterSizeCurve.Evaluate(0.5f));

        private void AddOffspring(ThingDef def, string kindName, int count, ref float femaleRemainder)
        {
            if (count <= 0) return;
            if (!def.race.hasGenders) { AddCohort(def, kindName, HerdSex.None, 0, 0, -1, count); return; }
            var females = Math.Min(count, HerdMath.ConsumeWhole(ref femaleRemainder, count * 0.5f));
            AddCohort(def, kindName, HerdSex.Female, 0, 0, -1, females);
            AddCohort(def, kindName, HerdSex.Male, 0, 0, -1, count - females);
        }

        private void AddCohort(ThingDef def, string kindName, HerdSex sex, int lifeStage, int ageBin, int gestation, int count)
        {
            if (count <= 0) return;
            var cohort = cohorts.FirstOrDefault(item => item.Def == def && item.pawnKindDefName == kindName && item.sex == sex && item.lifeStageIndex == lifeStage && item.ageProgressBin == ageBin && item.gestationBin == gestation);
            if (cohort == null) { cohort = new VirtualHerdCohort { defName = def.defName, pawnKindDefName = kindName, sex = sex, lifeStageIndex = lifeStage, ageProgressBin = ageBin, gestationBin = gestation }; cohorts.Add(cohort); }
            cohort.count += count;
        }

        private void Produce(VirtualHerdCohort cohort, float feedRatio)
        {
            if (!VirtualHerdUtility.IsAdult(cohort.Def, cohort.lifeStageIndex) || feedRatio <= 0f) return;
            var milk = VirtualHerdUtility.Milkable(cohort.Def);
            if (milk != null && (!milk.milkFemaleOnly || cohort.Sex == HerdSex.Female)) AccumulateOutput(ref cohort.milkAccumulator, milk.milkDef, cohort.count * milk.milkAmount * feedRatio / Math.Max(1f, milk.milkIntervalDays * 24f));
            var wool = VirtualHerdUtility.Shearable(cohort.Def);
            if (wool != null) AccumulateOutput(ref cohort.woolAccumulator, wool.woolDef, cohort.count * wool.woolAmount * feedRatio / Math.Max(1f, wool.shearIntervalDays * 24f));
            var eggs = VirtualHerdUtility.EggLayer(cohort.Def);
            if (eggs != null && (!eggs.eggLayFemaleOnly || cohort.Sex == HerdSex.Female))
            {
                var egg = cohort.Sex == HerdSex.Female && HasStoredReproductiveMale(cohort.Def) ? eggs.eggFertilizedDef ?? eggs.eggUnfertilizedDef : eggs.eggUnfertilizedDef;
                AccumulateOutput(ref cohort.eggAccumulator, egg, cohort.count * ((eggs.eggCountRange.min + eggs.eggCountRange.max) / 2f) * feedRatio / Math.Max(1f, eggs.eggLayIntervalDays * 24f));
            }
        }

        private bool HasStoredReproductiveMale(ThingDef def) => cohorts.Any(cohort => cohort.Def == def && cohort.Sex == HerdSex.Male && !cohort.Pregnant && VirtualHerdUtility.IsAdult(def, cohort.lifeStageIndex) && cohort.count > 0);

        private void AccumulateOutput(ref float accumulator, ThingDef def, float amount)
        {
            if (def == null || amount <= 0f) return;
            accumulator += amount;
            var whole = (int)Math.Floor(accumulator);
            if (whole <= 0) return;
            accumulator -= whole;
            var output = pendingOutputs.FirstOrDefault(item => item.defName == def.defName);
            if (output == null) { output = new VirtualHerdOutput { defName = def.defName }; pendingOutputs.Add(output); }
            output.count += whole;
        }

        private void FlushPendingOutputs()
        {
            foreach (var output in pendingOutputs.ToList())
            {
                var def = DefDatabase<ThingDef>.GetNamedSilentFail(output.defName);
                if (def == null) continue;
                var remaining = output.count;
                foreach (var barn in Barns())
                {
                    while (remaining > 0)
                    {
                        var amount = Math.Min(remaining, def.stackLimit);
                        var thing = ThingMaker.MakeThing(def); thing.stackCount = amount;
                        if (!GenPlace.TryPlaceThing(thing, barn.Position, map, ThingPlaceMode.Near)) { thing.Destroy(DestroyMode.Vanish); break; }
                        remaining -= amount;
                    }
                    if (remaining <= 0) break;
                }
                output.count = remaining;
            }
            pendingOutputs.RemoveAll(output => output.count <= 0);
        }

        private void MergeEquivalentCohorts()
        {
            var merged = new Dictionary<string, VirtualHerdCohort>();
            foreach (var cohort in cohorts.ToList())
            {
                var key = cohort.defName + ":" + cohort.pawnKindDefName + ":" + cohort.sex + ":" + cohort.lifeStageIndex + ":" + cohort.ageProgressBin + ":" + cohort.gestationBin;
                if (!merged.TryGetValue(key, out var existing)) { merged[key] = cohort; continue; }
                var left = existing.count; var right = cohort.count; var total = left + right; existing.count = total;
                existing.ageAccumulator = total <= 0 ? 0f : (existing.ageAccumulator * left + cohort.ageAccumulator * right) / total;
                existing.gestationAccumulator = total <= 0 ? 0f : (existing.gestationAccumulator * left + cohort.gestationAccumulator * right) / total;
                existing.conceptionRemainder += cohort.conceptionRemainder; existing.birthRemainder += cohort.birthRemainder; existing.femaleBirthRemainder += cohort.femaleBirthRemainder; existing.mortalityRemainder += cohort.mortalityRemainder;
                existing.milkAccumulator += cohort.milkAccumulator; existing.woolAccumulator += cohort.woolAccumulator; existing.eggAccumulator += cohort.eggAccumulator;
                cohorts.Remove(cohort);
            }
        }

        private static HerdCategory CategoryFor(VirtualHerdCohort cohort) => HerdMath.CategoryFor(cohort.Sex, VirtualHerdUtility.IsAdult(cohort.Def, cohort.lifeStageIndex), cohort.Pregnant);
        private static float AgeScore(VirtualHerdCohort cohort) => cohort.lifeStageIndex * AgeBins + cohort.ageProgressBin + cohort.ageAccumulator;
        private static bool SameKey(VirtualHerdCohort left, VirtualHerdCohort right) => left.defName == right.defName && left.pawnKindDefName == right.pawnKindDefName && left.sex == right.sex && left.lifeStageIndex == right.lifeStageIndex && left.ageProgressBin == right.ageProgressBin && left.gestationBin == right.gestationBin;
    }
}

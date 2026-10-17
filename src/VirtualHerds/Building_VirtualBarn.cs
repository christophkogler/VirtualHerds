using System.Collections.Generic;
using System.Linq;
using System;
using RimWorld;
using Verse;

namespace VirtualHerds
{
    public sealed class Building_VirtualBarn : Building, IThingHolder
    {
        private ThingOwner<Thing> foodInventory;
        private ThingFilter foodFilter;
        private ThingDef upgradeTarget;
        private float upgradeWork;
        private BarnUpgradeMaterials upgradeMaterials;
        public int Tier => def.GetModExtension<BarnTierExtension>()?.tier ?? 0;
        public ThingDef UpgradeTarget => upgradeTarget;
        public float UpgradeProgress => upgradeTarget == null ? 0f : upgradeWork / Math.Max(1f, upgradeTarget.GetStatValueAbstract(StatDefOf.WorkToMake));
        private ThingOwner UpgradeInventory => (upgradeMaterials ?? (upgradeMaterials = new BarnUpgradeMaterials(this))).GetDirectlyHeldThings();
        public bool HasUpgrade => upgradeTarget != null;

        public ThingOwner<Thing> FoodInventory => foodInventory ?? (foodInventory = new ThingOwner<Thing>(this));
        public ThingFilter FoodFilter => foodFilter;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            EnsureFoodFilter();
            map.GetComponent<VirtualHerdMapComponent>()?.SetBoostsChanged();
        }

        protected override void Tick()
        {
            base.Tick();
            FoodInventory.DoTick();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref foodInventory, "foodInventory", this);
            Scribe_Deep.Look(ref foodFilter, "foodFilter");
            Scribe_Defs.Look(ref upgradeTarget, "upgradeTarget");
            Scribe_Values.Look(ref upgradeWork, "upgradeWork");
            Scribe_Deep.Look(ref upgradeMaterials, "upgradeMaterials", this);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                EnsureFoodFilter();
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos())
                yield return gizmo;
            yield return new Command_Action
            {
                defaultLabel = "Manage virtual herd",
                defaultDesc = "Set map-wide per-species storage limits, inspect counts, and configure the pooled feed reserve.",
                icon = TexCommand.GatherSpotActive,
                action = () => Find.WindowStack.Add(new Dialog_VirtualHerdManager(Map))
            };
            yield return new Command_Action
            {
                defaultLabel = "Herd boosts",
                defaultDesc = "Configure global, species, and cohort boosts and their resource costs.",
                icon = TexCommand.DesirePower,
                action = () => Find.WindowStack.Add(new Dialog_HerdBoosts(Map))
            };
            foreach (var upgradeGizmo in UpgradeGizmos())
                yield return upgradeGizmo;
            yield return new Command_Action
            {
                defaultLabel = "Configure feed",
                defaultDesc = "Choose which foods haulers may place in this barn's feed inventory.",
                icon = TexCommand.DesirePower,
                action = () => Find.WindowStack.Add(new Dialog_VirtualHerdFoodFilter(this))
            };
            yield return new Command_Action
            {
                defaultLabel = "Discard stored herd",
                defaultDesc = "Permanently discard this map's stored virtual animals after confirmation. Real animals are not affected.",
                icon = TexCommand.ReleaseAnimals,
                action = () => Map.GetComponent<VirtualHerdMapComponent>()?.ConfirmDiscardStoredAnimals()
            };
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            var currentMap = Map;
            var currentPosition = Position;
            if (!Destroyed && currentMap != null)
            {
                currentMap.GetComponent<VirtualHerdMapComponent>()?.NotifyBarnRemoved(this);
                FoodInventory.TryDropAll(currentPosition, currentMap, ThingPlaceMode.Near);
                UpgradeInventory.TryDropAll(currentPosition, currentMap, ThingPlaceMode.Near);
            }
            base.Destroy(mode);
        }

        public new IThingHolder ParentHolder => Spawned ? Map : null;

        public ThingOwner GetDirectlyHeldThings()
        {
            return FoodInventory;
        }

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, FoodInventory);
            if (upgradeMaterials != null)
                outChildren.Add(upgradeMaterials);
        }

        public int MissingUpgradeMaterial(ThingDef material)
        {
            var cost = upgradeTarget?.costList?.FirstOrDefault(c => c.thingDef == material);
            return cost == null ? 0 : Math.Max(0, cost.count - UpgradeInventory.TotalStackCountOfDef(material));
        }

        public bool UpgradeMaterialsReady => HasUpgrade && (upgradeTarget.costList == null || upgradeTarget.costList.All(c => MissingUpgradeMaterial(c.thingDef) == 0));

        public int DeliverUpgradeMaterial(Thing material)
        {
            if (!Spawned || !HasUpgrade || material == null)
                return 0;
            int wanted = Math.Min(material.stackCount, MissingUpgradeMaterial(material.def));
            return wanted == 0 ? 0 : UpgradeInventory.TryAddOrTransfer(material, wanted);
        }

        public AcceptanceReport UpgradeClearance(ThingDef target)
        {
            if (!Spawned || target == null)
                return "Barn is not on a map.";
            foreach (var cell in GenAdj.OccupiedRect(Position, Rotation, target.size))
            {
                if (!cell.InBounds(Map) || (target.terrainAffordanceNeeded != null && !cell.GetTerrain(Map).affordances.Contains(target.terrainAffordanceNeeded)))
                    return "The upgraded barn needs suitable terrain across its full footprint.";
                foreach (var thing in cell.GetThingList(Map))
                {
                    if (thing == this || thing is Pawn || thing.def.category == ThingCategory.Item || thing.def.category == ThingCategory.Filth
                        || thing.def.building?.isPowerConduit == true)
                        continue;
                    if (thing is Blueprint || thing is Frame || thing.def.category == ThingCategory.Building || thing.def.category == ThingCategory.Plant)
                        return "Clear buildings, plants, and construction plans from the upgraded barn's footprint first.";
                }
            }
            return true;
        }

        public bool WorkOnUpgrade(float work)
        {
            if (!UpgradeMaterialsReady || !UpgradeClearance(upgradeTarget).Accepted)
                return false;
            upgradeWork += work;
            if (upgradeWork < upgradeTarget.GetStatValueAbstract(StatDefOf.WorkToMake))
                return false;
            CompleteUpgrade();
            return true;
        }

        private IEnumerable<Gizmo> UpgradeGizmos()
        {
            if (HasUpgrade)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Cancel barn upgrade",
                    defaultDesc = "Cancel construction and return all delivered materials. Construction work is lost.",
                    icon = TexCommand.ClearPrioritizedWork,
                    action = CancelUpgrade
                };
                yield break;
            }
            var nextName = def.GetModExtension<BarnTierExtension>()?.nextBarnDefName;
            var next = nextName == null ? null : DefDatabase<ThingDef>.GetNamedSilentFail(nextName);
            if (next == null)
                yield break;
            string costs = next.costList == null ? "" : string.Join(", ", next.costList.Select(c => c.count + " " + c.thingDef.label));
            var command = new Command_Action
            {
                defaultLabel = "Upgrade barn: " + next.label,
                defaultDesc = "Queue construction in place (" + costs + "). Builders haul the full new barn cost and work at the existing barn; herds keep operating until the completed swap.",
                icon = TexCommand.Install,
                action = () => { if (UpgradeClearance(next).Accepted) { upgradeTarget = next; upgradeWork = 0; } }
            };
            if (next.researchPrerequisites != null && next.researchPrerequisites.Any(r => !r.IsFinished))
                command.Disable("Research required: " + string.Join(", ", next.researchPrerequisites.Where(r => !r.IsFinished).Select(r => r.label)));
            else
            {
                var clearance = UpgradeClearance(next);
                if (!clearance.Accepted)
                    command.Disable(clearance.Reason);
            }
            yield return command;
        }

        private void CancelUpgrade()
        {
            if (!UpgradeInventory.TryDropAll(Position, Map, ThingPlaceMode.Near))
            {
                Messages.Message("Some upgrade materials cannot be returned here. Clear nearby space and cancel again.", MessageTypeDefOf.RejectInput, false);
                return;
            }
            upgradeTarget = null;
            upgradeWork = 0;
        }

        private void CompleteUpgrade()
        {
            var map = Map;
            var position = Position;
            var rotation = Rotation;
            var replacement = (Building_VirtualBarn)ThingMaker.MakeThing(upgradeTarget);
            replacement.SetFaction(Faction);
            var component = map.GetComponent<VirtualHerdMapComponent>();
            component?.BeginBarnReplacement();
            try
            {
                // Despawn is deliberately used: Destroy would release the last barn's herd.
                DeSpawn();
                try
                {
                    GenSpawn.Spawn(replacement, position, map, rotation, WipeMode.VanishOrMoveAside);
                }
                catch
                {
                    if (replacement.Spawned)
                        replacement.DeSpawn();
                    GenSpawn.Spawn(this, position, map, rotation, WipeMode.Vanish);
                    throw;
                }
                FoodInventory.TryTransferAllToContainer(replacement.FoodInventory);
                replacement.foodFilter = foodFilter;
                UpgradeInventory.ClearAndDestroyContents();
                upgradeTarget = null;
                Destroy(DestroyMode.Vanish);
            }
            finally
            {
                component?.EndBarnReplacement();
            }
        }

        public override string GetInspectString()
        {
            var text = base.GetInspectString();
            if (HasUpgrade)
            {
                string missing = string.Join(", ", upgradeTarget.costList.Where(c => MissingUpgradeMaterial(c.thingDef) > 0).Select(c => MissingUpgradeMaterial(c.thingDef) + " " + c.thingDef.label));
                text += "\nUpgrade: " + upgradeTarget.LabelCap + " (" + UpgradeProgress.ToStringPercent() + ")";
                text += "\n" + (missing.Length == 0 ? "Materials ready; construction pending" : "Needs: " + missing);
                var clearance = UpgradeClearance(upgradeTarget);
                if (!clearance.Accepted)
                    text += "\n" + clearance.Reason;
            }
            return text.Trim();
        }

        private void EnsureFoodFilter()
        {
            if (foodFilter != null)
                return;
            foodFilter = new ThingFilter();
            foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def.ingestible == null)
                    continue;
                if (VirtualHerdUtility.IsNutritionFoodDef(def))
                    foodFilter.SetAllow(def, true);
            }
        }
    }
}

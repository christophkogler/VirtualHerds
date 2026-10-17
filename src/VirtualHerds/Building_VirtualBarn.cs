using System.Collections.Generic;
using RimWorld;
using Verse;

namespace VirtualHerds
{
    public sealed class Building_VirtualBarn : Building, IThingHolder
    {
        private ThingOwner<Thing> foodInventory;
        private ThingFilter foodFilter;

        public ThingOwner<Thing> FoodInventory => foodInventory ?? (foodInventory = new ThingOwner<Thing>(this));
        public ThingFilter FoodFilter => foodFilter;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            EnsureFoodFilter();
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

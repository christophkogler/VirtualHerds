using System.Collections.Generic;
using Verse;

namespace VirtualHerds
{
    public sealed class BarnUpgradeMaterials : IThingHolder, IExposable
    {
        private Building_VirtualBarn barn;
        private ThingOwner<Thing> contents;
        public BarnUpgradeMaterials(Building_VirtualBarn barn)
        {
            this.barn = barn;
            contents = new ThingOwner<Thing>(this);
        }
        public IThingHolder ParentHolder => barn;
        public ThingOwner GetDirectlyHeldThings() => contents;
        public void GetChildHolders(List<IThingHolder> children) => ThingOwnerUtility.AppendThingHoldersFromThings(children, contents);
        public void ExposeData() => Scribe_Deep.Look(ref contents, "contents", this);
    }
}

using UnityEngine;
using RimWorld;
using Verse;

namespace VirtualHerds
{
    public sealed class Dialog_VirtualHerdFoodFilter : Window
    {
        private readonly Building_VirtualBarn barn;
        private readonly ThingFilter parentFilter = new ThingFilter();
        private readonly ThingFilterUI.UIState uiState = new ThingFilterUI.UIState();

        public Dialog_VirtualHerdFoodFilter(Building_VirtualBarn barn)
        {
            this.barn = barn;
            doCloseX = true;
            closeOnCancel = true;
            absorbInputAroundWindow = true;
            foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
                if (VirtualHerdUtility.IsNutritionFoodDef(def))
                    parentFilter.SetAllow(def, true);
        }

        public override Vector2 InitialSize => new Vector2(620f, 720f);

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 34f), "Barn nutrition filter");
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(inRect.x, inRect.y + 36f, inRect.width, 42f),
                "Choose food this barn may receive. Only food compatible with at least one stored species will be hauled.");
            float buttonWidth = (inRect.width - 24f) / 4f;
            if (Widgets.ButtonText(new Rect(inRect.x, inRect.y + 84f, buttonWidth, 32f), "All food"))
                SetPreset(0);
            if (Widgets.ButtonText(new Rect(inRect.x + buttonWidth + 8f, inRect.y + 84f, buttonWidth, 32f), "Hay + kibble"))
                SetPreset(1);
            if (Widgets.ButtonText(new Rect(inRect.x + (buttonWidth + 8f) * 2f, inRect.y + 84f, buttonWidth, 32f), "Herd diet"))
                SetPreset(2);
            if (Widgets.ButtonText(new Rect(inRect.x + (buttonWidth + 8f) * 3f, inRect.y + 84f, buttonWidth, 32f), "Clear"))
                SetPreset(3);
            TooltipHandler.TipRegion(new Rect(inRect.x, inRect.y + 84f, inRect.width, 32f),
                "Presets replace this barn's selection. Herd diet allows foods compatible with species currently stored on this map. Fine-tune individual foods below.");
            ThingFilterUI.DoThingFilterConfigWindow(new Rect(inRect.x, inRect.y + 126f, inRect.width, inRect.height - 168f),
                uiState, barn.FoodFilter, parentFilter, forceHideHitPointsConfig: true, forceHideQualityConfig: true, map: barn.Map);
            if (Widgets.ButtonText(new Rect(inRect.xMax - 120f, inRect.yMax - 34f, 120f, 32f), "Close"))
                Close();
        }

        private void SetPreset(int preset)
        {
            barn.FoodFilter.SetDisallowAll();
            foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (!VirtualHerdUtility.IsNutritionFoodDef(def))
                    continue;
                bool allow = preset == 0
                    || (preset == 1 && (def == ThingDefOf.Hay || def == ThingDefOf.Kibble))
                    || (preset == 2 && barn.Map.GetComponent<VirtualHerdMapComponent>().FoodCompatibleWithStoredSpecies(def));
                if (allow)
                    barn.FoodFilter.SetAllow(def, true);
            }
        }
    }
}

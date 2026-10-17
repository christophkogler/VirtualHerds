using UnityEngine;
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
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 34f), "Virtual herd feed");
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(inRect.x, inRect.y + 36f, inRect.width, 42f),
                "Choose food this barn may receive. Only food compatible with at least one stored species will be hauled.");
            ThingFilterUI.DoThingFilterConfigWindow(new Rect(inRect.x, inRect.y + 82f, inRect.width, inRect.height - 124f),
                uiState, barn.FoodFilter, parentFilter, forceHideHitPointsConfig: true, forceHideQualityConfig: true, map: barn.Map);
            if (Widgets.ButtonText(new Rect(inRect.xMax - 120f, inRect.yMax - 34f, 120f, 32f), "Close"))
                Close();
        }
    }
}

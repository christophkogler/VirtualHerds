using UnityEngine;
using Verse;

namespace VirtualHerds
{
    public sealed class Dialog_HerdPower : Window
    {
        private readonly VirtualHerdMapComponent component;
        public Dialog_HerdPower(VirtualHerdMapComponent component)
        { this.component = component; doCloseX = true; closeOnCancel = true; absorbInputAroundWindow = true; forcePause = true; }
        public override Vector2 InitialSize => new Vector2(760f, 650f);
        public override void DoWindowContents(Rect rect)
        {
            var p = component.PowerBreakdown;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, rect.width, 34), "Herd energy cost");
            Text.Font = GameFont.Small;
            var listing = new Listing_Standard();
            listing.Begin(new Rect(0, 44, rect.width, rect.height - 90));
            listing.Label("Controller: " + (component.Controller?.LabelCap.ToString() ?? "none") + (component.PowerReady ? " — operating" : " — power outage"));
            listing.GapLine();
            listing.Label("Animals stored: " + p.AnimalCount.ToString("N0"));
            listing.Label("Average baseline nutrition: " + p.AverageNutrition.ToString("0.####") + " /animal/day");
            listing.Label("Combined baseline: " + p.BaselineNutrition.ToString("0.###") + " nutrition/day (count × average)");
            listing.Label("Tier operating coefficient: " + p.TierCoefficient.ToString("N2") + " W per nutrition/day");
            listing.Label("Global active power scale: ×" + p.ActiveScale.ToString("0.###"));
            listing.Label("Unboosted active load: " + p.UnboostedActiveWatts.ToString("N2") + " W");
            listing.Label("Average boost cost multiplier: ×" + p.BoostMultiplier.ToString("0.####"));
            listing.Label("Active load after boosts: " + p.BoostedActiveWatts.ToString("N2") + " W");
            listing.GapLine();
            listing.Label("Controller idle: " + p.ControllerIdleBase.ToString("N2") + " W × " + p.IdleScale.ToString("0.###") + " = " + p.ControllerIdleWatts.ToString("N2") + " W");
            listing.Label("Digitization multiplier: ×" + p.DigitalMultiplier.ToString("0.###"));
            listing.Label("Controller total: (idle + boosted active) × digitization = " + p.ControllerWatts.ToString("N2") + " W");
            listing.Label("Other barns' idle load: " + p.SecondaryIdleWatts.ToString("N2") + " W");
            listing.Label("All barns combined: " + p.TotalWatts.ToString("N2") + " W");
            listing.GapLine();
            listing.Label("Boost costs are weighted by baseline nutrition across cohorts: 1 + linear × E + quadratic × E², where E sums the three effective boost extras. This average reconciles with the actual load. Feed-consumption scales do not reduce operating power.");
            listing.End();
            if (Widgets.ButtonText(new Rect(rect.width - 120, rect.height - 34, 120, 30), "Close")) Close();
        }
    }
}

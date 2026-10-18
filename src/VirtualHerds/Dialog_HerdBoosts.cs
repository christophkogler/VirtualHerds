using System;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using VirtualHerds.Pure;

namespace VirtualHerds
{
    /// <summary>Map-wide factory controls; cohorts remain pooled across all barns.</summary>
    public sealed class Dialog_HerdBoosts : Window
    {
        private readonly Map map;
        private int scope;
        private ThingDef species;
        private VirtualHerdCohort selected;
        private Vector2 scroll;

        public Dialog_HerdBoosts(Map map)
        {
            this.map = map;
            doCloseX = true;
            closeOnCancel = true;
            absorbInputAroundWindow = true;
            forcePause = true;
        }

        public override Vector2 InitialSize => new Vector2(1020f, 720f);

        public override void DoWindowContents(Rect rect)
        {
            var component = map.GetComponent<VirtualHerdMapComponent>();
            var cohorts = component.Cohorts.Where(c => c.Def != null && c.count > 0).ToList();
            var speciesList = cohorts.Select(c => c.Def).Distinct().OrderBy(d => d.label).ToList();
            if (species == null || !speciesList.Contains(species)) species = speciesList.FirstOrDefault();
            if (selected == null || !cohorts.Contains(selected) || selected.Def != species)
                selected = cohorts.FirstOrDefault(c => c.Def == species);

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, rect.width, 32f), "Factory farming controls");
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0f, 36f, rect.width, 44f),
                "All barns share the map's herds. The highest tier barn controls technology and powers all cohorts. " +
                "Boost extras from all three scopes add together, capped at " + component.MaxBoost.ToString("0.#") + "x per stat.");
            Widgets.Label(new Rect(0f, 84f, rect.width, 24f),
                "Controller: " + (component.Controller == null ? "none" : component.Controller.LabelCap.ToString()) +
                "   |   Required power: " + component.RequiredPowerWatts.ToString("N0") + " W");
            Widgets.Label(new Rect(0f, 110f, rect.width, 24f), component.PowerReady ? "Operating" :
                "POWER OUTAGE: production, breeding, gestation and feeding paused; aging continues.");
            Widgets.Label(new Rect(0f, 136f, rect.width, 24f),
                "Pooled nutrition demand: " + cohorts.Sum(c => component.DailyNutritionFor(c)).ToString("0.##") +
                " /day" + (component.PowerReady ? " at full operation" : " (currently paused)") +
                "   |   Stored: " + component.FoodNutrition.ToString("0.##"));

            if (Widgets.ButtonText(new Rect(rect.width - 170f, 106f, 170f, 28f), "Power breakdown"))
                Find.WindowStack.Add(new Dialog_HerdPower(component));
            var digitized = component.Digitized;
            if (component.CanDigitize || digitized)
            {
                Widgets.CheckboxLabeled(new Rect(0f, 166f, rect.width, 26f),
                    "Digitized: feed fraction " + BalanceTuning.Current["digitalFeed"].ToString("P0") + "; controller power ×" + BalanceTuning.Current["digitalPower"].ToString("0.##"), ref digitized);
                if (digitized != component.Digitized) component.SetDigitized(digitized);
            }
            else
                Widgets.Label(new Rect(0f, 166f, rect.width, 26f), "Digitization locked: requires an Archotech controller and Full digitization research.");

            string[] scopes = { "All herds", "Species", "Individual cohort" };
            for (var i = 0; i < scopes.Length; i++)
                if (Widgets.ButtonText(new Rect(i * 210f, 204f, 200f, 30f), (scope == i ? "• " : "") + scopes[i]))
                { scope = i; scroll = Vector2.zero; }

            if (Widgets.ButtonText(new Rect(rect.width - 170f, 204f, 170f, 30f), "Mod settings"))
                Find.WindowStack.Add(new Dialog_HerdSettings());
            if (scope > 0 && species != null && Widgets.ButtonText(new Rect(0f, 244f, rect.width, 30f), "Species: " + species.LabelCap))
                Find.WindowStack.Add(new FloatMenu(speciesList.Select(d => new FloatMenuOption(d.LabelCap, () =>
                { species = d; selected = null; scroll = Vector2.zero; })).ToList()));
            if (scope > 0 && species == null)
            {
                Widgets.Label(new Rect(0f, 254f, rect.width, 50f), "Store animals to configure a species or individual cohort. All-herd settings also apply to future intake and births.");
                return;
            }

            var editorX = scope == 2 ? 400f : 0f;
            if (scope == 2)
            {
                var rows = cohorts.Where(c => c.Def == species).ToList();
                var listRect = new Rect(0f, 286f, 384f, rect.height - 336f);
                var view = new Rect(0f, 0f, listRect.width - 18f, Math.Max(listRect.height, rows.Count * 66f));
                Widgets.BeginScrollView(listRect, ref scroll, view);
                for (var i = 0; i < rows.Count; i++)
                {
                    var row = rows[i];
                    if (Widgets.ButtonText(new Rect(0f, i * 66f, view.width, 62f),
                        (selected == row ? "• " : "") + CohortLabel(row))) selected = row;
                }
                Widgets.EndScrollView();
            }
            var settings = scope == 0 ? component.GlobalBoosts : scope == 1 ? component.BoostsFor(species) : selected?.boosts;
            if (settings == null) return;
            var width = rect.width - editorX;
            var y = 288f;
            Widgets.Label(new Rect(editorX, y, width, 42f), scope == 0 ? "Requests for every existing and future herd" :
                scope == 1 ? "Requests for every cohort of this species" : "Requests for the selected cohort only (newborns use defaults)");
            y += 48f;
            var changed = Slider(editorX, ref y, width, "Product output", ref settings.output, component.MaxBoost);
            changed |= Slider(editorX, ref y, width, "Breeding / conception", ref settings.breeding, component.MaxBoost);
            changed |= Slider(editorX, ref y, width, "Pregnancy speed", ref settings.gestation, component.MaxBoost);
            if (Widgets.ButtonText(new Rect(editorX, y, 190f, 28f), "Reset this scope to 1x"))
            { settings.output = settings.breeding = settings.gestation = 1f; changed = true; }
            if (changed) component.SetBoostsChanged();
            y += 36f;
            if (scope == 2 && selected != null)
            {
                var effective = component.EffectiveBoosts(selected);
                Widgets.Label(new Rect(editorX, y, width, 48f), "Combined effective: output " + effective.output.ToString("0.##") +
                    "x, breeding " + effective.breeding.ToString("0.##") + "x, pregnancy " + effective.gestation.ToString("0.##") + "x");
                y += 48f;
                Widgets.Label(new Rect(editorX, y, width, 40f), "Cohort nutrition: " + component.DailyNutritionFor(selected).ToString("0.##") + " /day at full operation.");
            }
            else
                Widgets.Label(new Rect(editorX, y, width, 58f), "These are requested multipliers. Species and cohort extras also apply; select an individual cohort to see its combined effective boosts. Feeding shortages reduce output and reproduction.");

            if (Widgets.ButtonText(new Rect(rect.width - 120f, rect.height - 34f, 120f, 30f), "Close")) Close();
        }

        private static bool Slider(float x, ref float y, float width, string label, ref float value, float max)
        {
            var previous = value;
            value = float.IsNaN(value) || float.IsInfinity(value) ? 1f : Mathf.Clamp(value, 1f, BoostMath.RequestCeiling);
            Widgets.Label(new Rect(x, y, width, 24f), label + ": requested " + value.ToString("0.##") + "x");
            var visibleValue = Mathf.Min(value, max);
            var edited = (float)Math.Round(Widgets.HorizontalSlider(new Rect(x, y + 25f, width - 8f, 22f), visibleValue, 1f, max), 2);
            if (Math.Abs(edited - visibleValue) > 0.005f) value = edited;
            y += 56f;
            return Math.Abs(value - previous) > 0.0001f || float.IsNaN(previous) || float.IsInfinity(previous);
        }

        private static string CohortLabel(VirtualHerdCohort cohort)
        {
            var stages = cohort.Def.race.lifeStageAges;
            var index = Math.Max(0, Math.Min(stages.Count - 1, cohort.lifeStageIndex));
            var minimum = stages[index].minAge;
            var age = minimum + (VirtualHerdUtility.NextStageMinAge(cohort.Def, index) - minimum) * cohort.ageProgressBin / 16f;
            return cohort.Sex + ", " + stages[index].def.label + ", ~" + age.ToString("0.##") + " years\n" +
                cohort.count + " animals; " + (cohort.Pregnant ? "pregnant " + (cohort.gestationBin / 16f).ToString("P0") : "not pregnant");
        }
    }
}

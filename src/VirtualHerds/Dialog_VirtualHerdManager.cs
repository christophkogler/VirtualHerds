using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using VirtualHerds.Pure;

namespace VirtualHerds
{
    public sealed class Dialog_VirtualHerdManager : Window
    {
        private readonly Map map;
        private Vector2 scrollPosition;
        private readonly Dictionary<string, string[]> buffers = new Dictionary<string, string[]>();

        public Dialog_VirtualHerdManager(Map map)
        {
            this.map = map;
            doCloseX = true;
            closeOnCancel = true;
            absorbInputAroundWindow = true;
            forcePause = true;
            draggable = false;
        }

        public override Vector2 InitialSize => new Vector2(1020f, 720f);

        public override void DoWindowContents(Rect inRect)
        {
            var component = map.GetComponent<VirtualHerdMapComponent>();
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f), "Factory Herds");
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(inRect.x, inRect.y + 32f, inRect.width, 44f),
                "Non-bonded animals available under RimWorld's auto-slaughter rules can be stored. Intake resets health, surgery, genes, lineage, names, training, and individual state. Stored animals are cohorts, not hidden pawns.");

            var reserveRect = new Rect(inRect.x, inRect.y + 82f, inRect.width, 52f);
            var listing = new Listing_Standard();
            listing.Begin(reserveRect);
            var reserve = component.ReserveDays;
            listing.Label("Pooled food reserve (herd-days; refills below 80%): " + reserve.ToString("0.0"));
            reserve = listing.Slider(reserve, 0f, 30f);
            if (Mathf.Abs(reserve - component.ReserveDays) > 0.0001f)
                component.SetReserveDays(reserve);
            listing.End();

            DrawNutritionReserve(component, new Rect(inRect.x, inRect.y + 140f, inRect.width, 18f));

            var listRect = new Rect(inRect.x, inRect.y + 168f, inRect.width, inRect.height - 216f);
            var defs = SpeciesOnMap(component);
            var viewRect = new Rect(0f, 0f, listRect.width - 16f, Math.Max(listRect.height, defs.Count * 116f + 45f));
            Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);
            var y = 0f;
            DrawHeader(viewRect.width, ref y);
            foreach (var def in defs)
                DrawSpeciesRow(component, def, viewRect.width, ref y);
            if (defs.Count == 0)
                Widgets.Label(new Rect(0f, y + 12f, viewRect.width, 28f), "No eligible or stored livestock is present on this map.");
            Widgets.EndScrollView();

            Widgets.Label(new Rect(inRect.x, inRect.yMax - 29f, inRect.width - 280f, 24f),
                "-1 means no limit. Lowering a limit releases the oldest stored cohorts first.");
            if (Widgets.ButtonText(new Rect(inRect.xMax - 255f, inRect.yMax - 36f, 125f, 32f), "Herd boosts"))
                Find.WindowStack.Add(new Dialog_HerdBoosts(map));
            if (Widgets.ButtonText(new Rect(inRect.xMax - 120f, inRect.yMax - 36f, 120f, 32f), "Close"))
                Close();
        }

        private static void DrawNutritionReserve(VirtualHerdMapComponent component, Rect rect)
        {
            const int segments = 5;
            var stored = component.FoodNutrition;
            var target = component.RequiredReserveNutrition;
            var dailyNeed = component.ReserveDays > 0f ? target / component.ReserveDays : 0f;
            var days = dailyNeed > 0f ? stored / dailyNeed : 0f;
            var tip = "Nutrition in storage: " + stored.ToString("0.##") + " / " + target.ToString("0.##") + " nutrition (target)\n" +
                      "About " + days.ToString("0.0") + " days of nutrition in storage; each segment represents 1 day.";
            Widgets.DrawBoxSolid(rect, new Color(0.12f, 0.12f, 0.12f));
            var gap = 2f;
            var segmentWidth = (rect.width - gap * (segments - 1)) / segments;
            for (var i = 0; i < segments; i++)
            {
                var segment = new Rect(rect.x + i * (segmentWidth + gap), rect.y, segmentWidth, rect.height);
                Widgets.DrawBoxSolid(segment, new Color(0.25f, 0.25f, 0.25f));
                var fill = Mathf.Clamp01(days - i);
                if (fill > 0f)
                    Widgets.DrawBoxSolid(new Rect(segment.x, segment.y, segment.width * fill, segment.height), new Color(0.35f, 0.75f, 0.3f));
            }
            TooltipHandler.TipRegion(rect, tip);
        }

        private List<ThingDef> SpeciesOnMap(VirtualHerdMapComponent component)
        {
            var defs = new HashSet<ThingDef>(component.Cohorts.Where(cohort => cohort.Def != null).Select(cohort => cohort.Def));
            if (map.mapPawns != null)
                foreach (var pawn in map.mapPawns.AllPawnsSpawned.Where(pawn => pawn.RaceProps?.Animal == true && pawn.Faction == Faction.OfPlayer))
                    defs.Add(pawn.def);
            return defs.OrderBy(def => def.label).ThenBy(def => def.defName).ToList();
        }

        private static void DrawHeader(float width, ref float y)
        {
            Widgets.DrawMenuSection(new Rect(0f, y, width, 34f));
            Widgets.Label(new Rect(8f, y + 7f, width - 16f, 22f), "Species / counts                         Total   Adult male   Young male   Adult female   Young female   Pregnant");
            y += 38f;
        }

        private void DrawSpeciesRow(VirtualHerdMapComponent component, ThingDef def, float width, ref float y)
        {
            var entry = component.LimitFor(def);
            var stored = component.StoredCounts(def);
            var materialized = component.MaterializedCounts(def);
            var exempt = component.ExemptCount(def);
            var dailyNutrition = component.Cohorts.Where(cohort => cohort.Def == def)
                .Sum(cohort => component.DailyNutritionFor(cohort));
            var dailyProduction = DailyProduction(component, def);
            Widgets.DrawAltRect(new Rect(0f, y, width, 108f));
            Widgets.Label(new Rect(8f, y + 6f, 170f, 22f), def.LabelCap);
            Widgets.Label(new Rect(8f, y + 31f, 266f, 24f),
                "Stored " + stored.Total.ToString("0.##") + "   Real " + materialized.Total.ToString("0.##") + "   Exempt " + exempt);
            Widgets.Label(new Rect(8f, y + 59f, 266f, 20f), "Feed: " + dailyNutrition.ToString("0.##") + " /day" + (component.PowerReady ? "" : " (paused)"));
            Widgets.Label(new Rect(8f, y + 82f, width - 16f, 20f), (component.PowerReady ? "Products/day at full feed: " : "Power outage; production paused. Potential/day: ") + dailyProduction);

            var values = new[] { entry.total, entry.adultMale, entry.youngMale, entry.adultFemale, entry.youngFemale, entry.pregnant };
            var counts = new[] { stored.Total, stored.AdultMale, stored.YoungMale, stored.AdultFemale, stored.YoungFemale, stored.Pregnant };
            var labels = new[] { "Total", "AM", "YM", "AF", "YF", "Preg" };
            var buffersForDef = GetBuffers(def, values);
            var changed = false;
            for (var i = 0; i < values.Length; i++)
            {
                var x = 282f + i * 108f;
                Widgets.Label(new Rect(x, y + 8f, 100f, 18f), labels[i] + " stored " + counts[i].ToString("0.##"));
                var value = values[i];
                Widgets.TextFieldNumeric(new Rect(x, y + 30f, 86f, 24f), ref value, ref buffersForDef[i], -1, 999999);
                values[i] = Math.Max(-1, value);
                if (Widgets.ButtonImage(new Rect(x + 88f, y + 30f, 20f, 24f), TexButton.Infinity))
                {
                    values[i] = -1;
                    buffersForDef[i] = "-1";
                }
                if (values[i] != (i == 0 ? entry.total : i == 1 ? entry.adultMale : i == 2 ? entry.youngMale : i == 3 ? entry.adultFemale : i == 4 ? entry.youngFemale : entry.pregnant))
                    changed = true;
            }
            entry.total = values[0];
            entry.adultMale = values[1];
            entry.youngMale = values[2];
            entry.adultFemale = values[3];
            entry.youngFemale = values[4];
            entry.pregnant = values[5];
            if (changed)
                component.MarkDirty();
            y += 114f;
        }

        private static string DailyProduction(VirtualHerdMapComponent component, ThingDef def)
        {
            var output = new Dictionary<ThingDef, float>();
            foreach (var cohort in component.Cohorts.Where(item => item.Def == def && item.count > 0 && VirtualHerdUtility.IsAdult(def, item.lifeStageIndex)))
            {
                var boost = component.EffectiveBoosts(cohort).output;
                var milk = VirtualHerdUtility.Milkable(def);
                if (milk != null && (!milk.milkFemaleOnly || cohort.Sex == HerdSex.Female))
                    AddDailyOutput(output, milk.milkDef, boost * cohort.count * milk.milkAmount / Math.Max(1f / 24f, milk.milkIntervalDays));

                var wool = VirtualHerdUtility.Shearable(def);
                if (wool != null)
                    AddDailyOutput(output, wool.woolDef, boost * cohort.count * wool.woolAmount / Math.Max(1f / 24f, wool.shearIntervalDays));

                var eggs = VirtualHerdUtility.EggLayer(def);
                if (eggs != null && (!eggs.eggLayFemaleOnly || cohort.Sex == HerdSex.Female))
                {
                    var averageEggs = (eggs.eggCountRange.min + eggs.eggCountRange.max) / 2f;
                    var hasMale = component.Cohorts.Any(item => item.Def == def && item.count > 0
                        && item.Sex == HerdSex.Male && VirtualHerdUtility.IsAdult(def, item.lifeStageIndex));
                    var eggDef = cohort.Sex == HerdSex.Female && hasMale
                        ? eggs.eggFertilizedDef ?? eggs.eggUnfertilizedDef
                        : eggs.eggUnfertilizedDef;
                    AddDailyOutput(output, eggDef,
                        boost * cohort.count * averageEggs / Math.Max(1f / 24f, eggs.eggLayIntervalDays));
                }
            }

            return output.Count == 0
                ? "none"
                : string.Join(", ", output.OrderBy(pair => pair.Key.label)
                    .Select(pair => pair.Value.ToString("0.##") + " " + pair.Key.LabelCap));
        }

        private static void AddDailyOutput(Dictionary<ThingDef, float> output, ThingDef product, float amount)
        {
            if (product == null || amount <= 0f)
                return;
            output[product] = output.TryGetValue(product, out var current) ? current + amount : amount;
        }

        private string[] GetBuffers(ThingDef def, int[] values)
        {
            if (!buffers.TryGetValue(def.defName, out var result))
            {
                result = values.Select(value => value.ToString()).ToArray();
                buffers[def.defName] = result;
            }
            return result;
        }
    }
}

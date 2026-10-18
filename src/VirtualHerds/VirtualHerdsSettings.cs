using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Verse;
using VirtualHerds.Pure;

namespace VirtualHerds
{
    public sealed class VirtualHerdsSettings : ModSettings
    {
        public List<string> DisabledAnimalDefs = new List<string>();
        public BalanceTuning Tuning { get; private set; } = new BalanceTuning();
        private Dictionary<string, float> savedValues;
        private int powerDefaultsVersion = BalanceTuning.PowerDefaultsVersion;
        private BalanceTuning draft;
        private readonly Dictionary<string, string> buffers = new Dictionary<string, string>();
        private readonly string[] groups = { "Simulation", "Feeding", "Power", "Tiers", "Economy", "Advanced", "Species" };
        private string group = "Simulation", search = "";
        private Vector2 scrollPosition;

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref DisabledAnimalDefs, "disabledAnimalDefs", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.Saving) savedValues = Tuning.Export();
            Scribe_Collections.Look(ref savedValues, "balanceValues", LookMode.Value, LookMode.Value);
            Scribe_Values.Look(ref powerDefaultsVersion, "powerDefaultsVersion", 0);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                DisabledAnimalDefs = DisabledAnimalDefs ?? new List<string>();
                Tuning = BalanceTuning.Load(savedValues, powerDefaultsVersion);
                powerDefaultsVersion = BalanceTuning.PowerDefaultsVersion;
                draft = null;
            }
        }

        private void Apply()
        {
            Tuning = draft.Clone();
            buffers.Clear();
            BalanceTuning.Current = Tuning;
            RuntimeDefinitionTuning.Apply();
            NotifyMaps();
            VirtualHerdsMod.Instance?.WriteSettings();
        }

        private static void NotifyMaps()
        {
            if (Current.Game == null) return;
            foreach (var map in Find.Maps) map.GetComponent<VirtualHerdMapComponent>()?.SetBoostsChanged();
        }

        public void DoWindowContents(Rect rect)
        {
            if (draft == null) draft = Tuning.Clone();
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 42f),
                "Runtime balance for all maps and saves. Edit values, then Apply & save. Defaults reproduce the original balance. Hover a setting for details.");
            float tabWidth = rect.width / groups.Length;
            for (int i = 0; i < groups.Length; i++)
                if (Widgets.ButtonText(new Rect(rect.x + i * tabWidth, rect.y + 44f, tabWidth - 4f, 28f), (group == groups[i] ? "• " : "") + groups[i]))
                { group = groups[i]; scrollPosition = Vector2.zero; }
            Widgets.Label(new Rect(rect.x, rect.y + 82f, 66f, 26f), "Search:");
            string nextSearch = Widgets.TextField(new Rect(rect.x + 68f, rect.y + 78f, rect.width - 68f, 28f), search);
            if (nextSearch != search) { search = nextSearch; scrollPosition = Vector2.zero; }
            var listRect = new Rect(rect.x, rect.y + 116f, rect.width, rect.height - 220f);
            if (group == "Species") DrawSpecies(listRect);
            else DrawOptions(listRect);

            float buttonWidth = (rect.width - 24f) / 4f;
            float footer = rect.yMax - 94f;
            if (Widgets.ButtonText(new Rect(rect.x, footer, buttonWidth, 30f), "Apply & save")) Apply();
            if (Widgets.ButtonText(new Rect(rect.x + buttonWidth + 8f, footer, buttonWidth, 30f), "Discard edits"))
            { draft = Tuning.Clone(); buffers.Clear(); }
            if (Widgets.ButtonText(new Rect(rect.x + (buttonWidth + 8f) * 2f, footer, buttonWidth, 30f), "Reset section"))
            { if (group != "Species") draft.Reset(group); buffers.Clear(); }
            if (Widgets.ButtonText(new Rect(rect.x + (buttonWidth + 8f) * 3f, footer, buttonWidth, 30f), "Reset all balance"))
            { draft.Reset(); buffers.Clear(); }
            bool changed = BalanceTuning.Options.Any(o => draft[o.Key] != Tuning[o.Key]);
            Widgets.Label(new Rect(rect.x, footer + 35f, rect.width, 24f),
                (changed ? "Unapplied balance edits. " : "Balance applied. ") + RuntimeDefinitionTuning.Status);
            Widgets.Label(new Rect(rect.x, footer + 59f, rect.width, 40f),
                "Simulation changes take effect on the next herd hour; power and previews refresh on Apply. Species toggles apply directly. Resets affect balance edits only; Apply to save them.");
        }

        private void DrawOptions(Rect rect)
        {
            var options = BalanceTuning.Options.Where(o => string.IsNullOrWhiteSpace(search) ? o.Group == group :
                (o.Group + " " + o.Label + " " + o.Description).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            const float rowHeight = 94f;
            var view = new Rect(0, 0, rect.width - 18f, Math.Max(rect.height, options.Count * rowHeight));
            Widgets.BeginScrollView(rect, ref scrollPosition, view);
            float y = 0;
            foreach (var option in options)
            {
                if (y + rowHeight >= scrollPosition.y && y <= scrollPosition.y + rect.height)
                {
                    Widgets.DrawAltRect(new Rect(0, y, view.width, rowHeight - 4f));
                    Widgets.Label(new Rect(8f, y + 4f, view.width - 16f, 24f),
                        (string.IsNullOrWhiteSpace(search) ? "" : option.Group + " / ") + option.Label + "   [default " + option.Default.ToString("0.###") + "]");
                    var value = draft[option.Key];
                    // Logarithmic travel keeps small multipliers usable even with large permitted maxima.
                    float min = (float)Math.Log(1d + option.Min), max = (float)Math.Log(1d + option.Max);
                    float position = (float)Math.Log(1d + value);
                    float edited = Widgets.HorizontalSlider(new Rect(8f, y + 35f, view.width - 224f, 22f), position, min, max);
                    if (Math.Abs(edited - position) > 0.00001f)
                    {
                        value = (float)Math.Round(Math.Exp(edited) - 1d, option.Whole ? 0 : 3);
                        buffers.Remove(option.Key);
                    }
                    if (!buffers.TryGetValue(option.Key, out var buffer)) buffer = value.ToString("0.###", CultureInfo.InvariantCulture);
                    Widgets.TextFieldNumeric(new Rect(view.width - 204f, y + 31f, 106f, 28f), ref value, ref buffer, option.Min, option.Max);
                    // TextFieldNumeric may parse NaN/Infinity: the schema validates every write.
                    draft[option.Key] = value;
                    buffers[option.Key] = buffer;
                    if (Widgets.ButtonText(new Rect(view.width - 90f, y + 31f, 82f, 28f), "Reset"))
                    { draft[option.Key] = option.Default; buffers.Remove(option.Key); }
                    Widgets.Label(new Rect(8f, y + 63f, view.width - 16f, 22f),
                        "Range " + option.Min.ToString("0.###") + "–" + option.Max.ToString("0.###") + (option.Whole ? " (whole numbers)" : "") + "  |  " + option.Description);
                    TooltipHandler.TipRegion(new Rect(0, y, view.width, rowHeight - 4f), option.Description + "\nAllowed: " + option.Min + "–" + option.Max + ". Default: " + option.Default);
                }
                y += rowHeight;
            }
            if (options.Count == 0) Widgets.Label(new Rect(8, 8, view.width - 16, 28), "No matching balance settings.");
            Widgets.EndScrollView();
        }

        private void DrawSpecies(Rect rect)
        {
            var defs = DefDatabase<ThingDef>.AllDefsListForReading.Where(d => d.race?.Animal == true)
                .Where(d => string.IsNullOrWhiteSpace(search) || (d.label + " " + d.defName).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(d => d.label).ThenBy(d => d.defName).ToList();
            var view = new Rect(0, 0, rect.width - 18f, Math.Max(rect.height, 52f + defs.Count * 28f));
            Widgets.BeginScrollView(rect, ref scrollPosition, view);
            Widgets.Label(new Rect(0, 0, view.width, 48f), "New and modded species are enabled by default. Disabling one releases its stored animals at the next reconciliation.");
            float y = 52f;
            foreach (var def in defs)
            {
                bool enabled = !DisabledAnimalDefs.Contains(def.defName), previous = enabled;
                Widgets.CheckboxLabeled(new Rect(0, y, view.width, 24f), def.LabelCap + " (" + def.defName + ")", ref enabled);
                if (enabled != previous)
                {
                    if (enabled) DisabledAnimalDefs.Remove(def.defName); else DisabledAnimalDefs.Add(def.defName);
                    NotifyMaps();
                }
                y += 28;
            }
            Widgets.EndScrollView();
        }
    }
    public sealed class Dialog_HerdSettings : Window
    {
        public Dialog_HerdSettings()
        { doCloseX = true; closeOnCancel = true; forcePause = true; absorbInputAroundWindow = true; }
        public override Vector2 InitialSize => new Vector2(1050, 760);
        public override void DoWindowContents(Rect rect) => VirtualHerdsMod.Instance.Settings.DoWindowContents(rect);
        public override void PostClose()
        { base.PostClose(); VirtualHerdsMod.Instance?.WriteSettings(); }
    }

}

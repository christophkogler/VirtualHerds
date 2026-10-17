using System.Collections.Generic;
using System.Linq;
using System;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace VirtualHerds
{
    public sealed class VirtualHerdsMod : Mod
    {
        public const string PackageId = "christoph.virtualherds";
        public static VirtualHerdsMod Instance { get; private set; }
        public VirtualHerdsSettings Settings { get; private set; }

        public VirtualHerdsMod(ModContentPack content) : base(content)
        {
            Instance = this;
            Settings = GetSettings<VirtualHerdsSettings>();
            new Harmony(PackageId).PatchAll();
            Log.Message("[Virtual Herds] Loaded.");
        }

        public bool IsSpeciesEnabled(ThingDef def)
        {
            return def?.race?.Animal == true && !Settings.DisabledAnimalDefs.Contains(def.defName);
        }

        public override string SettingsCategory()
        {
            return "Virtual Herds";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Settings.DoWindowContents(inRect);
        }
    }

    public sealed class VirtualHerdsSettings : ModSettings
    {
        public List<string> DisabledAnimalDefs = new List<string>();
        private Vector2 scrollPosition;

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref DisabledAnimalDefs, "disabledAnimalDefs", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && DisabledAnimalDefs == null)
                DisabledAnimalDefs = new List<string>();
        }

        public void DoWindowContents(Rect inRect)
        {
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 38f),
                "New and modded animal definitions are enabled by default. Disable a species here to keep it out of Virtual Herds.");
            var listRect = new Rect(inRect.x, inRect.y + 44f, inRect.width, inRect.height - 44f);
            var defs = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(def => def.race?.Animal == true)
                .OrderBy(def => def.label)
                .ThenBy(def => def.defName)
                .ToList();
            var viewRect = new Rect(0f, 0f, listRect.width - 16f, Math.Max(listRect.height, defs.Count * 28f));
            Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);
            var y = 0f;
            foreach (var def in defs)
            {
                var enabled = !DisabledAnimalDefs.Contains(def.defName);
                var old = enabled;
                Widgets.CheckboxLabeled(new Rect(0f, y, viewRect.width, 24f), def.LabelCap + " (" + def.defName + ")", ref enabled);
                if (enabled != old)
                {
                    if (enabled)
                        DisabledAnimalDefs.Remove(def.defName);
                    else if (!DisabledAnimalDefs.Contains(def.defName))
                        DisabledAnimalDefs.Add(def.defName);
                }
                y += 28f;
            }
            Widgets.EndScrollView();
        }
    }
}

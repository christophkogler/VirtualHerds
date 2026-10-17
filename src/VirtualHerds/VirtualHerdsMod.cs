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
            VirtualHerds.Pure.BalanceTuning.Current = Settings.Tuning;
            LongEventHandler.ExecuteWhenFinished(RuntimeDefinitionTuning.Initialize);
            new Harmony(PackageId).PatchAll();
            Log.Message("[Virtual Herds] Loaded.");
        }

        public bool IsSpeciesEnabled(ThingDef def)
        {
            return def?.race?.Animal == true && !Settings.DisabledAnimalDefs.Contains(def.defName);
        }

        public override string SettingsCategory()
        {
            return "Factory Herds";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Settings.DoWindowContents(inRect);
        }
    }

}

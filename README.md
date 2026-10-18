# Factory Herds

Factory Herds (formerly Virtual Herds) is a RimWorld **1.7** factory farming mod. It keeps livestock in pooled, definition-driven cohorts and lets a late-game colony trade increasingly large amounts of power for production and reproductive speed. Package ID remains `christoph.virtualherds`; HerdCompression is incompatible.

## Research and facilities

Research the seven projects in the **Factory farming** tab. Each of the first six unlocks a distinct **6×6** barn. Build from Miscellaneous or select an existing barn's **Upgrade barn** command.

| Tech | Research | Barn | Per-stat boost cap | Idle power | Extra feed coefficient | W per baseline nutrition/day |
|---|---|---|---:|---:|---:|---:|
| Neolithic | Pastoralism | Pasture barn | 2× | 0 | 2 | 0 |
| Medieval | Managed husbandry | Managed herd barn | 3× | 100 | 1.5 | 50 |
| Industrial | Intensive husbandry | Industrial livestock facility | 6× | 100 | 1 | 40 |
| Spacer | Accelerated bioculture | Spacer bioculture facility | 12× | 100 | 0.5 | 30 |
| Ultra | Synthetic ecosystems | Ultratech herd foundry | 25× | 100 | 0.2 | 20 |
| Archotech | Virtual herds | Archotech herd nexus | 100× | 100 | 0 | 10 |

Every tier requires the full building resource cost and construction work, including upgrades. Later facilities require steel, plasteel, uranium, gold, industrial components, and advanced components; exact quantities appear in the Architect panel and upgrade tooltip. Medieval electrical machinery requires a power supply even though its research is medieval. Research does not provide a generator.

**Full digitization**, the second Archotech research, unlocks an optional toggle in Herd boosts: all nutrition consumption becomes zero and the controller's entire power requirement becomes **10×** its non-digitized value. Disable it to resume feeding. Losing the Archotech controller suspends digitization until another is available.

## Boosts and costs

Select any barn → **Herd boosts**, or open the controls from the herd manager. Set three independent multipliers: product output, breeding/conception rate, and pregnancy speed. Each can target all herds, every cohort of a species, or a selected cohort. Extras above 1× add across the scopes, then clamp to the current tier's cap. For example, global 2× + species 3× + cohort 4× requests 7×, rather than multiplying to 24×. Requests survive a tier loss and take effect again when the appropriate controller returns.

All barns still share **one herd pool per map**. The highest-tier barn (lowest thing ID breaks ties) is the controller for every species and cohort; other powered barns consume their tier's idle power. Cohorts are not assigned to individual buildings. Build more barns for extra hauling/output endpoints, not free boost stacking.

Let `E` be the sum of the three effective extras above 1×, and `S = E + 0.1 E²`. Daily feed is baseline cohort nutrition × `(1 + feed coefficient × S)`. Active power is controller idle + sum of baseline cohort nutrition/day × the tier's W coefficient × `(1 + S)`. Powered tiers share 100 W idle draw and become more efficient per animal: their operating coefficients fall from 50 to 40, 30, 20 and 10. At the same effective boosts, upgrading reduces power; the wider boost ranges create the large high-tier loads. Every boosted stat has a real ongoing cost and extreme settings become very expensive. Archotech has no extra nutrition premium, but still requires baseline food until digitized. A neolithic herd at output 2× and other stats 1× consumes **3.2× baseline feed** and no power.

Powered-controller outages pause production, conception, gestation, and feeding; animals remain stored and aging continues. There is no automatic fallback to a lower-tier barn to avoid a power bill. Feed shortages proportionally slow production and reproduction without starvation deaths. The manager shows demand at full operation; the boost dialog also identifies outages.

Cohort tuning follows mothers through conception, gestation, and delivery, and stays distinct when otherwise similar cohorts merge. Newborns and newly stored physical animals start with neutral cohort tuning and inherit global/species settings. Cohorts age normally; pregnancy acceleration does not accelerate aging.

Existing buildable barns now occupy 6×6 cells when a save is loaded. Clear the extra border around previously built 4×4 facilities before updating. The hidden legacy 2×2 barn retains its original footprint.

## Upgrading safely

The old barn operates while builders haul materials and construct its replacement. Construction work and delivered materials persist in saves. **Cancel barn upgrade** returns delivered materials near the building; if space is blocked, clear it and retry. Completion swaps to the next barn in one transaction and transfers food and the food filter without releasing or reabsorbing animals. The new footprint must remain clear throughout construction; power conduits are allowed underneath.

Ordinary placement over an existing barn is rejected with guidance to use its upgrade command. The original **2×2** barn remains hidden in the build menu so existing saves can load it; upgrading it to the pasture barn requires room for the new 6×6 footprint. Removing the last barn normally still releases stored animals. Destroying an upgrading barn drops food/materials and follows that same release rule.

## Herd simulation

The herd manager sets per-species storage limits: total, adult male/female, young male/female, and pregnant. `-1` is unlimited. Lowering a limit releases oldest cohorts first. Bonded animals and animals unavailable under auto-slaughter eligibility remain physical.

Intake intentionally resets individual health, surgery, genes, lineage, names, training, and identity. Released animals are fresh generic healthy pawns with approximate age/pregnancy. No hidden pawn objects are kept. Milk, wool, eggs, reproduction and old-age deaths use deterministic averages. Old-age deaths reduce stored counts **without corpses or butcher products**; release thrumbos and butcher them normally for horns. Disease and starvation deaths are not simulated.

Barns hold real food in saved inventories. Haulers maintain a configurable map-wide reserve (default five herd-days, refill below 80% of target). Select a barn → **Configure feed** to use the searchable food tree or the All food, Hay + kibble, Herd diet, and Clear presets. Filters are saved per barn and transferred on upgrade. Select an undrafted hauler and right-click a barn → **Haul food to barn** to order a delivery; the order respects the filter, stored species diets, reachability, and reserve demand. Compatible food is consumed spoil-first. Products appear near barns; blocked outputs stay pending. Digitized herds need no reserve or hauling, although existing food remains in inventory.

## Runtime mod settings and power breakdown

Open **Options → Mod settings → Factory Herds**, or **Herd boosts → Mod settings**. There are 70 numeric controls, grouped into Simulation, Feeding, Power, Tiers, Economy and Advanced, plus the existing species switches. Search finds matching balance controls across sections. Numeric fields allow exact values; sliders use logarithmic travel so small multipliers remain easy to set. Each control shows its default, range and explanation. Reset a single value, the current section, or all balance values; **Apply & save** commits edits, while **Discard edits** restores the applied values. Resets do not change species switches.

Settings are global to this installation and affect every loaded map and future saves. Power and previews refresh on Apply; production, reproduction, aging and feeding use the new settings at the next simulated herd hour. Species switches apply directly and disabling a species releases its stored animals on reconciliation. The default values use the balance described above. On the first load after the power-efficiency update, saved idle/operating values that exactly match the old defaults migrate to the new defaults; custom values are preserved. The migration is versioned so later edits are not repeatedly reset. Neolithic remains unpowered, and optional digitization still adds its explicit energy multiplier.

| Section | Controls |
|---|---|
| Simulation | Global product output; separate milk/wool/egg yields; conception and gestation speed; aging speed; mortality coefficient and life-expectancy onset; litter size; female birth fraction; breeding coverage per male |
| Feeding | Baseline hunger conversion; nutrition scale; all-map reserve target scale; default reserve days for new maps; refill threshold |
| Power | Idle and active scales; linear/quadratic boost cost coefficients; digitization energy multiplier and remaining feed fraction |
| Tiers | Separate boost cap and feed premium for each tier; idle watts and active watts per baseline nutrition/day for each powered tier |
| Economy | Material and work scales for each barn tier, including legacy tier 0; seven independent research cost scales |
| Advanced | Reconciliation intervals; haul search and animal release radii; output stack budget per map/hour; construction XP during upgrades |

The feed-consumption and product scales do not change the energy bill. Energy uses baseline species nutrition; the separate baseline hunger conversion changes that baseline for both feeding and power. Economy scales are always calculated from the original patched definitions, so repeatedly applying settings does not compound costs. Changes to a barn type's material/work costs wait while any map has construction plans, a frame, or an upgrade targeting that type. Cancel or finish those projects; pending changes retry on the next herd hour. Actual applied construction scales are saved with each map and restored before its jobs resume; old saves use the original costs. Unfinished research retains absolute progress, so reducing its cost can finish a project sooner; completed research remains completed. Closing the game with unapplied balance edits does not save those edits.

**Herd boosts → Power breakdown** shows animal count, mean baseline nutrition per animal, the combined baseline, tier energy coefficient, global active scale, unboosted active load, the nutrition-weighted average boost cost multiplier, controller idle load, digitization, secondary barns and the final total. The average reconciles with the actual assigned power load even when cohorts use different boosts. A power outage still displays the required load.

Settings have finite bounds; tier caps can reach 1,000× and lowering a cap preserves higher saved requests for later use. Zero production, conception, gestation or aging scales pause those processes without erasing their fractional progress. Zero nutrition removes the feed requirement; zero mortality disables old-age deaths. The unpowered Neolithic tier stays unpowered. Extreme output remains buffered if the physical stack budget is exhausted; numeric count buffers saturate at the signed 32-bit representation limit. Building geometry, age/gestation save buckets, the engine calendar, numerical tolerances and UI geometry are intentionally fixed.

## Building and deployment

Install the .NET 10 SDK and build against an installed RimWorld 1.7:

```bash
dotnet build src/VirtualHerds/VirtualHerds.csproj -c Release -p:RimWorldInstallDir=/path/to/RimWorld
```

The default install path is `$HOME/.local/share/Steam/steamapps/common/RimWorld`. The default output is `1.7/Assemblies`; game dependencies are not copied. `LoadFolders.xml` selects the matching assembly directory. An optional legacy `-p:RimWorldVersion=1.6` build remains available against cached/downloaded Krafs 1.6 references, but gameplay validation focuses on 1.7.

`./build-and-deploy.sh` explicitly builds and copies the common XML/art payload and selected assemblies to the installed mod directory. Override `RIMWORLD_MOD_DIR`, `RIMWORLD_INSTALL_DIR`, or `RIMWORLD_VERSION` as needed. Ordinary `dotnet build` does not deploy.

```bash
dotnet run --project src/VirtualHerds.Tests/VirtualHerds.Tests.csproj
```

The dependency-free tests cover count/remainder/feed allocation and progression costs, additive caps, invalid settings, scaling, Archotech baseline food, and digitization. See `docs/validation.md` for actual in-game verification and its limits. New barn textures and generation prompts are recorded in `docs/art-prompts.md`.

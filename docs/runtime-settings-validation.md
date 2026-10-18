# Runtime settings validation — 2026-10-17

Baseline: merged main `74a7000`; clean checkout, branch `deep-runtime-settings`. Scope: 70 runtime tuning values, retained species switches, energy breakdown, economy deferral/persistence, numerical/physical output bounds. No unrelated edits. RuntimeDefinitionTuning was implemented by an isolated file-owning worker; integration, the other files and the checks below were performed by the main agent.

## Automated checks

- Final 1.6 and 1.7 Release builds succeeded with zero warnings/errors. Earlier attempts emitted NU1900 before the successful escalated restore/build. Game version remains 1.7.4981 rev136.
- Dependency-free self-tests passed: original default formulas, every option's defaults/bounds/NaN/infinity, export/restore/reset, independent nutrition/power, per-tier caps above the old limit, preservation of higher requests, mortality, yield scales, digitization, material rounding, large count saturation, and mixed-cohort weighted power reconciliation.
- `git diff --check` passed. Both versioned assemblies rebuilt from these sources.

## Ordinary UI and settings persistence

`/tmp/vh-settings-baseline`: isolated Harmony, Core and five DLC, Factory Herds only, no probe. Loaded the earlier disposable feed-filter save. Opened Herd boosts → Mod settings. Visually checked the scrollable groups, numeric fields, sliders, defaults, range/hint rows and Apply/reset/discard controls. Entered exact global production 2 via the numeric field and clicked Apply & save. The isolated settings XML records production=2 and all 69 then-available keys (the final baseline-hunger control brings the schema to70). After full process restart, the same settings UI visibly restored 2. Screenshots: `1792280293635350613`, `1792280319145202543`, `1792280401652271009`. This run preceded the final research-completion snapshot and buffer-refresh changes; the final probe-free load is recorded below.

## Optional runtime fixture, separate from the ordinary mod

`/tmp/vh-settings-feature-final` adds unshipped `local.factoryherds.settingsprobe` AFTER Factory Herds. Source/project are retained in `/tmp/vh-settings-probe`. Debug actions directly seeded 10 female and 2 male virtual cows with different output requests and a shared species breeding request, plus managed/pasture barns. This is not animal-intake evidence. It does not modify the shipped mod.

- Ordinary Power breakdown button opened a readable full panel: 12 animals, 0.856 baseline nutrition/animal/day, 10.272 total, tier coefficient 50, global active scale1, unboosted513.60W, weighted boost multiplier3.1833, boosted1634.96W, idle100W, final1734.96W. Screenshot `1792280651866463047`. The fixture was unpowered, so this verifies required/assigned load, not delivered grid energy. Direct cohort-sum and actual negative CompPowerTrader.PowerOutput assertions both passed.
- Focused assertions called actual simulation methods on detached components: zero aging, doubled gestation, doubled product output including fractional buffers. On the live component, changed active power scaled the actual breakdown. Completed research stayed finished after its cost increased.
- Construction assertions passed: active target deferred material/work edits, snapshot restore preserved pending values, cancellation permitted new costs, repeated Apply did not compound. Temporary values were restored afterward. These assertions are method-level evidence; they do not claim a live multi-hour simulation.
- Player-path persistence: the first ordinary upgrade attempt was correctly blocked by unsuitable fixture terrain. Developer Set terrain (rect) made the footprint concrete; the normal Upgrade barn gizmo then queued pasture→managed. In the real settings UI, searched Medieval construction, set both material/work multipliers to2, clicked Apply. UI reported one deferred barn type and the existing project still needed400 wood/250 granite/150 steel. Screenshot `1792280909458595703`.
- Saved `settings-pending.rws` through the normal Save UI. XML has the queued target and actual applied scale1, while the separate mod settings file requests2. After FULL process restart loading that save, the project still needed400/250/150; read-only fixture log reported400wood/8000work and pending status. Screenshot `1792281051813128959`.
- Cancelled via ordinary Cancel barn upgrade gizmo, clicked Apply & save again. A newly queued upgrade required800wood/500granite/300steel, then was cancelled. Screenshot `1792281122737316785`. Pending work migration thus observed across restart; this test did not deliver construction materials or finish the full upgrade.
- Final fixture source/deployed hashes and active list verified exactly after the interactions; game/private display stopped. Earlier `/tmp/vh-settings-feature2` only reached startup and was stopped before feature work because the final research-preservation build superseded it.

## Limits

Not every option was exercised in-game; all options have shared schema validation tests, with representative simulation/cost/UI checks above. No 1.6 gameplay, multi-map economy migration, arbitrary modded-species matrix, long-run maximum-scale population/output, or full construction completion under tuned costs. No in-session reload (restart loading was used). Species toggles/reset/discard use retained/simple paths but were not exhaustively clicked. The physical stack budget is finite and excess output stays saved; representational count saturation is documented. Neither normal user config nor the installed live mod was changed.

Only known engine Wayland-not-initialized startup errors were observed in the inspected ordinary/fixture logs; no mod load/action/save/load exceptions were found. Retained manifests, screenshots, action history, saves and logs live under the named /tmp runs.

## Probe-free release check before the focused review addition

`/tmp/vh-settings-release`: final DLL, Harmony/Core/five DLC/Factory Herds, no probe. Copied ONLY the earlier isolated mod-settings XML (production2) into this new isolated profile, then loaded the pre-settings feed-filter save after process startup. Visually confirmed playable colony and production2 in the settings UI (`1792281219871925652`). No mod load/post-load exceptions; source/deployed/mod-list hash verification passed. Owned game and display stopped. This checks the final research-completion and construction snapshot loading paths on a save without their new fields.

## Focused review addition: baseline hunger conversion

Independent read-only review identified the remaining fixed hunger conversion1.6 as a scope gap. Added Feeding → Baseline hunger conversion (default1.6), giving70 numeric controls; the actual nutrition utility now uses the shared validated formula. It changes baseline nutrition for both feed and power, while the existing nutrition-consumption scale remains feed-only. Added pure default/custom/floor tests. Rebuilt1.6 and1.7 with zero warnings/errors; full pure suite passed again.

`/tmp/vh-settings-hunger-final` is a new final-payload snapshot with the updated unshipped settings probe and prior pending-upgrade save, fresh process. The actual live map's baseline nutrition doubled when the conversion doubled, then restored; all prior focused actual simulation/economy/weighted-power assertions passed again. Current log has only known engine Wayland messages and no mod exceptions. Exact source/deployed/mod-list verification passed and owned process/display stopped. The last hunger-control addition was verified by this focused probe-assisted runtime check and pure tests; the probe-free visual/UI/persistence observations above remain on the otherwise equivalent69-control candidate.

Independent review and focused recheck: fresh GPT-6 Luna Medium reviewer inspected implementation and final hunger-conversion correction; no material remaining findings. Reviewer did code inspection only and did not rerun checks. Verification evidence above was produced separately.

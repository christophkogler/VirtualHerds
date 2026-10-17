# Validation record

Test date: 2026-10-17. Installed game: RimWorld 1.7.4981 rev136, Linux Waylith/.NET10. Normal mod payload and profile were not modified. Tests used isolated profiles and private Xephyr displays.

## Builds and static checks

- Default 1.7 Release build against the installed Assembly-CSharp and UnityStubs: passed, zero warnings/errors.
- Optional 1.6 compilation against Krafs 1.6.4633 passed for the final shared sources. No 1.6 gameplay test;1.7 is the supported development focus.
- Dependency-free tests passed, including feed allocation/count conservation and boost caps, additive scopes, rising costs, Archotech baseline feed and 10× digitization.
- Runtime XML parses. All six new PNGs have actual transparent alpha and were visually inspected.
- Deployment script built 1.7 and copied the full runtime payload to `/tmp/vh-deploy-check`; no installed mod was overwritten.

## Ordinary 1.7 smoke test

`/tmp/vh17-baseline`: Harmony3.0.1, Core and all five installed DLC, Factory Herds. No probe mod. Reached a visually confirmed playable colony. Factory farming research tab present. No mod loading, missing-def or XML exceptions observed. Evidence: `screenshots/1792266584503269210.png`, `1792266598368871574.png` and retained logs.

## Feature fixtures

`/tmp/vh17-feature2` adds the temporary **unshipped** `local.factoryherds.probe` after Factory Herds. Probe source and its limitations are recorded in `/tmp/virtualherds-probe/README.txt`. Fixture cows were seeded directly, so this is not an intake test for those cows. The normal quicktest guinea pig was subsequently absorbed through ordinary intake.

- **Construction and save/load:** prepared a pasture barn, twelve cows, feed, and undelivered materials. Used the ordinary Upgrade barn gizmo. Colonists delivered the required 400 wood,250 granite blocks and 150 steel through the custom construction workgiver/job. Saved through the normal Save UI at 5150.165/8000 work. XML contains the materials, work, food and three independently edited boost scopes. Returned to menu and loaded through the normal Load UI; progress restored to 64.39674%, materials ready, settings and stored population restored. Normal construction resumed and completed.
- **Swap without animal flicker:** before/after swap:13 stored,0 materialized; player-animal SpawnSetup/Destroy counters remained1/1 (the single spawn was the initial guinea pig and its destruction was normal intake before the swap). No release/intake cycle occurred. Food and tuning remained present. Barn changed visually to managed barn at the same position. Screenshots: `1792266860698773401.png`, `1792266917549366942.png`, `1792267603610320865.png`; state logs at13:06:03 and13:06:46.
- **Player controls/cost preview:** used all three scope selectors and dragged sliders. Global output~1.99× raised demand from10.43 to33.13 nutrition/day at Neolithic. Species conception1.57× and female-cohort gestation 1.79× combined and persisted separately. Screenshots: `1792266975813532170.png`, `1792266990573104046.png`.
- **Outage:** newly upgraded powered barn on an unpowered map retained all animals and reported required 2026.835W/PowerReady false. Archotech fixture similarly reported outage while unconnected. No fallback to an unpowered tier.
- **Digitization UI:** Archotech fixture changed from 389,484W and 20.7 nutrition/day to 3,894,840W and zero nutrition/day after the normal checkbox toggle. Screenshot `1792267780158489351.png`.
- **Isolated runtime method assertions:** conception and delivery retain cohort tuning; adult female output buffers; paused production preserves pending output; old-age mortality reduces population without creating a corpse; Neolithic feed penalty, zero digitized nutrition and 10× energy. All passed. These reflection-based checks call individual methods on a detached component and do not establish a full live-hour result by themselves.

## Final-build process restart and powered herd

`/tmp/vh17-final` loaded the named pending-upgrade save after a full process restart, with the final 1.7 assembly and in-universe research descriptions. Work/materials/tuning restored, and the saved upgrade completed again. A new Archotech fixture had an actual vanometric cell and conduit network; the standard Unlimited Power development toggle supplied the enormous test load. This verifies network membership and the load assigned by the mod, not the balance or capacity of a normal generator installation.

The player digitization toggle showed Operating, zero nutrition/day, and 3,894,840 W. A live `SimulateHour` assertion left food unchanged at 378.4 nutrition, produced 24 milk (468 to 492), advanced gestation, and retained population 25. Actual `CompPowerTrader.PowerOutput` was -3,894,840 with `hasPowerNet=True`; the secondary managed barn remained at -100 W idle. Evidence: final-run `screenshots/1792268423395172704.png`, log entries at 13:21:14, retained action history and save fixture.

## Environment observations and limits

The engine logged Wayland-not-initialized messages on its working X11 display and SteamAPI initialization warnings. The game offered its ordinary Ignore option for unavailable Steam features when returning to the main menu; that option was used. No DRM workaround was introduced. Initial sandbox display access failed and the test launch succeeded with host display permission.

The full technology chain has been defined and compiled, but only the pasture→managed construction cycle was observed. High-tier balance is an initial tuning pass, not a long-running colony benchmark. Legacy 2×2 migration, blocked refund placement, all-tier construction, destruction during upgrade, modded animal definitions, and long-duration maximum boosts remain additional regression cases.

Release payload verification: `/tmp/vh17-release-manifest` passed the helper's hash/source/mod-list check. The earlier final-run source check flagged a subsequently rebuilt **inactive 1.6 DLL only**; the active1.7 DLL and runtime XML/art match the tested copies. Final1.7 DLL SHA-256: `054169234261428cb37fa9e930c64122e34535d466f2c66d266d4ae553cd8c88`. The isolated test processes/displays were stopped after checks.

Independent review: a fresh read-only reviewer inspected the final diff and core simulation, upgrade, placement, persistence, XML and loading paths. No confirmed implementation defects were found. The reviewer ran diff whitespace checks but no tests; the runtime evidence above was produced separately.

# Virtual Herds

Virtual Herds is a RimWorld 1.6 mod (`christoph.virtualherds`) for pooled livestock simulation. It is incompatible with `christoph.HerdCompression`.

Build from `src/VirtualHerds`:

```bash
dotnet build VirtualHerds.csproj
```

The assembly is written to `1.6/Assemblies`. The barn texture is `Textures/Things/Building/VirtualHerds_Barn.png`.

## v1 behavior

Build the unpowered, no-research, no-component, genuinely stuffable 2x2 virtual herd barn from the Miscellaneous tab. It accepts Metallic, Woody, and Stony stuff (`costStuffCount` 160); it has no power requirement. All barns on a map share one state. The manager sets per-species maximum stored counts for total, adult male, young male, adult female, young female, and pregnant cohorts. `-1` means infinity, pregnant is disjoint from adult female but counts toward total, and category limits are applied before total limits. The UI reports stored, materialized, and exempt animals; real materialized/exempt pawns do not consume virtual storage slots.

Eligible intake follows RimWorld's auto-slaughter availability rules and excludes animals with a direct bond relation, matching auto-slaughter when bonded animals are not allowed. Names, training, quest tags, and ordinary held state do not independently block intake. Intake is transactional and destroys the original pawn only after its cohort has been accepted. Cohorts retain only def, sex, life-stage, 16-bin age progress, 16-bin gestation progress, and fractional accumulators; no pawn objects are hidden. Intake and release intentionally normalize health, surgery, genes, lineage, and individual state. Released animals are freshly generated generic healthy, untrained, unnamed pawns with approximate age and pregnancy progress.

Each barn has a persisted `IThingHolder` food inventory and `ThingFilter`. A hauling work giver maintains a pooled reserve measured in herd-days, default five. Refill starts below 80% of the configured reserve and stops when the full target is reached. Actual compatible nutrition is consumed spoil-first and shortages scale production/reproduction without causing starvation deaths. Milk, wool, eggs, live pregnancy, gestation, litter size, and sex split use deterministic definition-driven averages. Product placement is attempted at all barn endpoints and blocked output remains pending.

## Deliberate v1 reductions and safe API boundaries

The implementation uses public RimWorld 1.6 APIs and narrow mod-owned notifications rather than broad value/needs/damage Harmony patches. Trade, slaughter, caravans, quests, cross-species caps, species allocation, immersive intake/release jobs, finite capacity, identity/health/genetic preservation, disease mortality, and starvation mortality remain intentionally deferred. Definition-driven old-age mortality is represented by a deterministic life-expectancy curve over age bins; exact vanilla per-pawn health/mortality state is not preserved.

The public RimWorld 1.6 settlement-abandon entrypoint is narrowly patched to show the stored-animal count and require confirmation before continuing into vanilla's normal abandonment checks. Each barn also exposes an explicit confirmation-gated discard action. Removing the last barn releases stored cohorts near that barn; removing one of several barns keeps the pooled state. If another mod bypasses the normal abandonment entrypoint, map cleanup still clears stored cohorts because no safe release map remains.

## Verification

`src/VirtualHerds.Tests` is a dependency-free executable containing deterministic integer-conservation, gender-mapping, limit, remainder, feed-ratio, and fair-allocation tests. Run it with:

```bash
dotnet run --project src/VirtualHerds.Tests/VirtualHerds.Tests.csproj
```

In-game checklist: start a test colony with non-bonded animals available under RimWorld's auto-slaughter rules, including animals with generated names and training; build two barns; verify manager counts and shared limits; haul compatible food into either barn; lower a category/total limit and verify oldest-first release; raise it and verify intake; block product cells and verify pending output later places; remove one barn, then the last barn; confirm the discard action; save/reload and verify cohorts, limits, food, and pending output persist.

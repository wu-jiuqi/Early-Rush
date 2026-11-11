# ArtV4 integrated model, seed 91027

Updated 2026-11-11. This filename retains the original street-adapter name; integration now covers all three chapters. This is an in-progress gameplay integration, not a completed release. See [current acceptance](V14-current-acceptance.md) for the result snapshot.

## Selection and configuration

Seed 91027 selects authored ArtV4 street, metro and office geometry through the game map factory. Legacy 91026 remains available separately. With no explicit ConfigV9, RunV9 applies a 600-second budget to 91027, street walkSpeed 3.8, and the existing 3.6 speed to metro/office. Explicit diagnostic configs are not silently overwritten.

The old V4StreetValidation.ReplayDefault and ReplayDiagnostic methods still explicitly request their historical 420/600 budgets and default speed. They do not, by themselves, reproduce the current street 3.8 profile. Use the candidate profile reports or the integrated replay entry points when comparing current results.

## Shared source geometry

Coordinates use x/40 and (layout.worldHeight-y)/40. Rooms/capsules and building footprints come from the same JSON as the independent sprite renderer. Integer cells are navigation candidates; actual collision conservatively contains the whole circle inside a room or corridor capsule and rejects circle/rectangle footprint intersection. Concave joins may be narrower than the HTML implementation; mathematical equivalence is not claimed.

Metro preserves two main sections of 11 and 13 points. routeBreaks contains index 11, so route metrics and navigation must not invent a walkable edge across the train gap. Portal from/to are source pixels (4352,11088) and (4352,9552). Arrival transfer is driven by the train simulation rather than ordinary movement through the wall. Office has one 24-point main section.

## Task integration

- Street retains the actual breakfast, two-wheel transport and five-board mechanics. Source positions and task areas feed the adapter; a visible source rectangle is not automatically a replacement for an existing circular trigger. Street signal placement still adapts the north/south crossing rule to a real vertical segment.
- Metro tasks are bound by ID, not source array order (which is M2/M1/M3). Boarding uses the train lifecycle and portal. Cloth spawn uses the M3-cloth source object foot point because M3 has no pickups array. Dirt samples derive from the three source patch rectangles.
- Office uses the three source file deliveries, three source chair pickups with their own rectangular targets, and three source coffee actors. Completion counts derive from authored entities, rather than waiting for the legacy five-chair default. Coffee actors use source a/b routes within the existing guard/spill/desk-pause lifecycle.
- Missing optional JSON zone/target objects and empty exit arrays are handled as absent. Required geometry remains validated.

## Source people, collision and rendering

Only these four PNG names are source humans: commuter-female, commuter-male, commuter-back and commuter-side. Other sprites/npcs entries such as cars and buses remain vehicles.

The original builder gave every solid object a source-pixel rectangle (x+0.18*w, y+h-30, 0.64*w, 30). RushV4NpcSources proves a unique blocker index for each human object, with 0.011px matching tolerance, and rejects missing or ambiguous matches. Factories exclude only those indices. No nearby furniture or manual wall rectangle is removed.

Matched counts are street 93, metro 65, office 81. Remaining structure counts are 644, 535 and 601. The renderer skips the replaced static commuter images. Runtime sourceArtId assigns each identity to one initialized AI actor inside the existing generic population allocation, not as an extra batch of 239 people.

Physical source spawn candidates use a bounded 8px-step search, at most 64px/1.6u, within the original corridor segment. Runtime public-navigation and overlap rules can still reject a candidate; then the source identity uses an already legal generic spawn. The final observed initializations therefore include larger relocations:

- Street: 760 actual people; 33/93 source identities beyond 1.6u of the original foot point.
- Metro: 900 actual people; 22/65 beyond 1.6u.
- Office: 285 ordinary people plus 3 independent coffee actors, 288 total; 29/81 beyond 1.6u. The ordinary configured cap is 700, leaving 415 unfilled slots.

These facts must not be described as full original-position replication or 700 actual office pedestrians. The dynamic ownership contract passed 3235 checks with explicit unfilled-capacity reporting.

The renderer uses individual PNG objects and shared ground geometry, preserves aspect ratios, and skips static pushables, puddles and M3-cloth where live simulation entities own presentation. Dynamic coffee actors and source people must not gain extra static copies. Sprite stills do not constitute a full animation set.

## Current replay evidence and limitations

Under street walkSpeed 3.8 and 600 seconds, real Tick mainline passed in 466.13 seconds and all three tasks plus the exit passed in 583.18 seconds. Reports: Builds/EarlyRush-V14/QA/v4-street/candidate-3p8-mainline.txt and candidate-3p8-alltasks.txt.

Earlier interior mainlines passed: metro 496.35 seconds, office 478.87 seconds. Those runs predate the final source-NPC version and require reruns. Latest completed all-task reports failed: metro at 464.90 seconds with two tasks and a stalled cloth push; office at 465.75 seconds with one task and a stalled chair push. Metro repair verification is pending. Neither interior chapter has current full-task acceptance.

These are automated input/simulation results, not human-play timing, performance measurements or final-release approval. No completed-release claim is made.

## Contract entry points

- V4LayoutContractValidation.Validate: source DTO, task IDs, coordinates, portal and three-chair contracts.
- ValidateRuntimeInitialization: actual interior RunV9 construction with ambient population disabled.
- ValidateCoffeePatrol: actual Tick patrol plus pause/resume, ambient population disabled.
- ValidateNpcSources: exact source/blocker ownership and bounded candidate positions.
- ValidateDynamicNpcOwnership: default configured populations, unique runtime source IDs, retained structures, physical initial clearance and relocation/unfilled statistics.

Each entry has a narrower scope than end-to-end gameplay. Preserve failure results and rerun the final build rather than treating process exit, a static screenshot or an older success as current full-task completion.

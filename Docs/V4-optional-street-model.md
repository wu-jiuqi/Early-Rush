# ArtV4 integrated model, seed 91027

Updated 2026-11-11. This filename retains the original street-adapter name; integration now covers all three chapters. This is an in-progress gameplay integration, not a completed release. See [current acceptance](V14-current-acceptance.md) for the result snapshot.

## Selection and configuration

The executable now defaults to the three authored ArtV4 chapters, seed 91027, without arguments. --legacy-v14 selects the old comparison version; Play-Art-V4.cmd remains compatible. With no explicit ConfigV9, RunV9 uses a 600-second budget and walking speeds 3.8 for street, 4.0 for metro, and 3.6 for office. Explicit diagnostic configs are not silently overwritten.

The old V4StreetValidation.ReplayDefault and ReplayDiagnostic methods still explicitly request their historical 420/600 budgets and default speed. They do not, by themselves, reproduce the current street 3.8 profile. Use the candidate profile reports or the integrated replay entry points when comparing current results.

## Shared source geometry

Coordinates use x/40 and (layout.worldHeight-y)/40. Rooms/capsules and building footprints come from the same JSON as the independent sprite renderer. Integer cells are navigation candidates; actual collision conservatively contains the whole circle inside a room or corridor capsule and rejects circle/rectangle footprint intersection. Concave joins may be narrower than the HTML implementation; mathematical equivalence is not claimed.

Metro preserves two main sections of 11 and 13 points. routeBreaks contains index 11, so route metrics and navigation must not invent a walkable edge across the train gap. Portal from/to are source pixels (4352,11088) and (4352,9552). Arrival transfer is driven by the train simulation rather than ordinary movement through the wall. Office has one 24-point main section.

## Task integration

- Street retains the actual breakfast, two-wheel transport and five-board mechanics. Source positions and task areas feed the adapter; a visible source rectangle is not automatically a replacement for an existing circular trigger. Street signal placement still adapts the north/south crossing rule to a real vertical segment.
- Metro tasks are bound by ID, not source array order (which is M2/M1/M3). Boarding uses the train lifecycle and portal. Cloth spawn uses the M3-cloth source object foot point because M3 has no pickups array. M3 restores exactly three marks per source patch, nine marks total, at normalized x=.23/.50/.77 and y=.50. Cleaning brush radius is 65px/40=1.625u. The cloth remains a real physically transported body; completion is not assigned directly. The 31-check source/initialization contract passed (Logs/v14-v4-clean-source-contract.log).
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

All runs below use machine input and a 600-second limit, not human balance acceptance:

- Street speed 3.8: mainline passed at 466.133 seconds; all three tasks and exit passed at 583.183 seconds. Reports remain Builds/EarlyRush-V14/QA/v4-street/candidate-3p8-mainline.txt and candidate-3p8-alltasks.txt.
- Metro speed 4.0: mainline passed at 446.483 seconds; all three tasks and exit passed at 594.383 seconds. This speed is now the default. Evidence: Logs/v14-v4-metro-mainline-candidate4.log and v14-v4-metro-tasks-candidate4.log. Older precise-approach/2-of-3 failures are superseded by these results.
- Office speed 3.6: earlier mainline passed at 478.867 seconds; all three tasks and exit on the final source-NPC version passed at 599.333 seconds. Evidence: Builds/EarlyRush-V14/QA/v4-interior/stage-2-tasks.txt. The earlier mainline remains labelled as an earlier run, not a final-code rerun.

Office full-score margin is only approximately 0.667 seconds; human playtest feedback is required. Metro margin is approximately 5.617 seconds. Machine success does not establish forgiving controls, human difficulty or polished balance.

## Final playable-demo build and actual Windows smoke

The final playable demo build succeeded: Logs/v14-v4-demo-final-build.log contains V14_BUILD_RESULT Succeeded. Game source was pushed as commit 847668c4c8ac0160b87c472c8793c3bc9bc8887f. Subsequent acceptance-document updates accompany the package separately; this does not claim those later edits are already in that commit.

Builds/EarlyRush-V14/QA/runtime-art-v4/runtime-report.json, UTC 2026-11-11T04:02:25Z, reports passed=true with no errors and nine start/pause/resume screenshots. All three actual Windows chapters passed movement, pause stability, resume and single-Tick clock checks.

The latest measured mean frame intervals were approximately 18.95/16.94/17.08ms. Each chapter ran only 50 movement frames, 20 paused frames and 50 resumed movement frames, including QA/render overhead. This establishes a short actual-run pass, not long-duration stability, a production performance benchmark or human balance acceptance.

The demo archive name is Builds/EarlyRush-ArtV4-Playable-Demo-Windows.zip, distinct from the old Integration-Candidate archive. Build and runtime completion are confirmed; archive assembly is handled separately by the integration process. Do not reuse the old candidate's size as evidence for the new package. The executable defaults to the new three-chapter 91027 game without arguments. --legacy-v14 selects the old comparison and Play-Art-V4.cmd remains compatible.

This is a **playable demo, not a final Release**. Three-chapter machine all-task results, final build and short actual Windows execution have passed. Office's approximately 0.667-second machine margin still needs user playtest feedback. Human balance, longer stability, full-route visual checks and failure/retry coverage are not certified. Source-NPC relocation, the unfilled office population cap and directional-still asset limitations remain disclosed above.

## Contract entry points

- V4LayoutContractValidation.Validate: source DTO, task IDs, coordinates, portal and three-chair contracts.
- ValidateRuntimeInitialization: actual interior RunV9 construction with ambient population disabled.
- ValidateCoffeePatrol: actual Tick patrol plus pause/resume, ambient population disabled.
- ValidateNpcSources: exact source/blocker ownership and bounded candidate positions.
- ValidateDynamicNpcOwnership: default configured populations, unique runtime source IDs, retained structures, physical initial clearance and relocation/unfilled statistics.

Each entry has a narrower scope than end-to-end gameplay. Preserve failure results and rerun the final build rather than treating process exit, a static screenshot or an older success as current full-task completion.

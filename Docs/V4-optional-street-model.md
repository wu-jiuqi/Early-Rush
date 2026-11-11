# Optional Art V4 street model (seed 91027)

Only `stage=0, seed=91027` selects the new JSON geometry. 91026/default route and old 500–650 unit/9-module assertions are unchanged. No RunV9 simulation or RushPlayV9 rendering code was modified by this adapter.

Coordinates exactly match ArtV4 review: x/40, (18432-y)/40. Main route comes from the JSON main corridor. Rooms/capsules and blocker footprints are loaded from the same data. Integer cells remain a navigation candidate index; actual IsWalkable uses conservative whole-circle containment inside a room or capsule, then exact circle-vs-rectangle footprint rejection. This prevents sub-grid thin obstacle penetration; concave unions may be slightly narrower for bodies than an exact eroded polygon union. No claim of mathematically identical HTML collision.

Existing mechanics remain authoritative. Breakfast uses source task position, repair uses source target-zone center. Two wheel initial positions use the source pair midpoint plus the existing ±1.5 unit offsets, NOT original independent positions (existing Setup is unchanged). Five flip-board contact positions retain existing ±2 unit spacing. Their workspaces and wheel back-push approach points are validated for radius 1.05. Signal is placed on an actual vertical segment because legacy crossing rule is north/south only; source JSON does not define a signal event. Source trigger rectangles do not replace legacy circular trigger conditions.

Not integrated: metro/office portal, dynamic source NPC identities, exact independent wheel positions, HTML simplified task actions, timings, source task-region rectangle triggers. Visual mapping is a separate step. Default 420s may be too short and is deliberately NOT silently extended. No release qualification is claimed.

Editor/CLI methods:
- V4StreetValidation.Validate: geometry/clearance/connectivity and thin footprints.
- V4StreetValidation.ReplayDefault: mainline + all-task real Tick with shipped 420s budget.
- V4StreetValidation.ReplayDiagnostic: same with explicitly diagnostic 600s budget.
Reports: Builds/EarlyRush-V14/QA/v4-street. Failures are recorded as failures; no forced completion/teleport is used.

## Optional game rendering
RushV4StreetArt.Build(parent, owned, decor, run) is restricted to stage0 seed91027. It uses exactly the review's 40px/unit coordinate transform and ground mesh algorithms; does not create cameras, input, reference actors or simulation state. Out-of-route area is a contrasting solid background plane; all original sprites keep aspect and bottom-center pivots. Sorting uses ArtV8.SortOrder so static objects interleave with existing live actors; sprites are returned in decor for normal foreground fade. Meshes, materials and sprites are tracked in owned for normal teardown. Unity frustum culling applies to renderers; this does not claim pooled instantiation or performance measurements.

role=pushable source wheel images are omitted, because the authoritative bodies render at the adapted physics positions. The flip-panels facade is intentionally retained behind the five interactive boards as a goal sign: its original footprint remains collidable. The source NPC/vehicle props remain visible stationary obstacles to match their source blockers; they are not claimed to be V13 AI actors. Breakfast/repair frontages remain; the caller must suppress old fixture pictures and old random facades/old full-map backdrop to avoid duplicate art. Five live boards and dynamic wheels continue to use the caller's gameplay renderer. Source decorative task icons do not define hitboxes.

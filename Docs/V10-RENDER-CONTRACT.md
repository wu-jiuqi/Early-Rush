# V10 component rendering contract

Status: implementation scaffold, not visually approved or compiled yet. V9 stays unchanged.

- `RushWorldV10.Build(V9.MapData geometry, int stage)` consumes `MapDataV10.geometry`.
- Actual Tilemap cells are painted from geometry.cells. Last matching zone selects material; texture cell indices use absolute world coordinates modulo four, preventing phase jumps at zone boundaries. Material bitmaps themselves must be authored tileable; this code does not claim to repair non-seamless source images.
- Structures use exact `artKey`, `position` ground-contact pivot and `width`. Collision holes belong to the map generator, never an independent visual collider. Connected ground and structure footprints must be validated together.
- `Place/Position` support actor and task components. `Occlusion(actor)` fades foreground structures only; Y ground-contact sorting is shared by actors/structures.
- `FullBounds()` includes every renderer, suitable for full chapter camera output.
- `Resources/V10Art/manifest.json` declares exact PNG resource paths and normalized ground pivots. Missing, unregistered or pending assets fail visibly; no SVG, wrong-icon replacement, or full concept image used as playable background.
- Initial manifest explicitly marks existing V8 PNGs `legacy-reuse-not-v10-approved`. This is an integration inventory, NOT a claim that new concept-matching art exists. Generated V10 cutouts must replace entries and have visual/alpha/pivot verification recorded before approval.
- Do not call this a nine-task playable build until a dedicated V10 simulation consumes the new geometry. V9 RunV9 currently hard-codes its map factory.

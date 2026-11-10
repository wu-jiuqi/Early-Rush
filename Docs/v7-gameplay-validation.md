# V7 gameplay integration and validation

## Implemented changes

- Independent **E** interaction input. Holding E in the clock-in pad brakes the requested movement, braces the player and fills the existing 0.8-second meter once actual speed falls below 1.4. Being airborne/recovering still cancels progress. Stationary Shift remains a compatibility path.
- V6 code analysis: there were not two competing Shift callbacks. Clocking required `IsBracing` AND speed < 1.4. Walking while braced remained ~2.35 units/s in the office, so holding direction + Shift reset the meter. This is a verified code-level mechanism, not a claim that every user-reported failure has been reproduced.
- One optional two-stop errand per stage: breakfast delivery, lost-item return, colleague coffee. Hold E for 0.65 seconds at each icon. Completed errands add 8 seconds of deadline grace, once each. Elapsed time and split times stay truthful. Restart clears all rewards and tasks; stage transitions start a new task.
- Rain in the second outdoor district eases in at z100–112 and out at z172–184. It reduces turning/braking acceleration to 62%, except while bracing. Main path stays traversable.
- Station first hall z84–90 has optional left stairs (72% movement speed) and right upward escalator (+0.85 units/s belt motion). Both remain planar top-down traversal mechanics, not a new 3D elevation system. Existing central route remains open.
- Existing alternating central narrow gaps and wide side bypasses, street construction bends and office island bends preserved. No unvalidated new barriers were added to commuter routes.

## Checks performed in cloud

`python Tools/check_v7_gameplay.py`: source-contract assertions only. These checks do not compile C#, test Unity API compatibility, confirm generated art placement, measure frame time or substitute for playtesting.

## Required Unity Player acceptance

1. Office clock: approach holding W+Shift (progress must not finish), release direction and hold Shift (compatibility), then repeat with W+E (brakes and completes). Hold E before stage transition: release latch must prevent accidental action until released.
2. Interrupt clocking at 0.4 seconds via collision, leaving pad, pause and focus loss. Each must reset progress; no double completion.
3. Each errand: reach pickup, hold E, carry to dropoff, hold E. Check icon change, +8 grace once, no negative elapsed time; leave early, pause, restart, finish stage without task. All remain valid.
4. Station: stairs slower, escalator aids upward travel and opposes downward travel; collision and dash remain functional; no graphical obstacle covers a traversable path.
5. Rain: entering/exiting is gradual; braking is visibly longer unless bracing; keyboard release and background return do not leave held inputs.
6. Run existing core, combat, impact and journey QA. New mechanics may alter timing of legacy route tests; inspect failures rather than weakening assertions to claim success.

At time of authoring no Unity compiler/Player was available in this cloud environment. Windows computer was offline. Runtime results remain pending.

## Integration review (cloud)

- Initialization reviewed: `RushRouteHUD.Awake` only caches RushGame; task positions are read in its first `Update`, after synchronous `BuildStage → BuildJourney → BuildDailyCommute`. No observed code-order race reading uninitialized task coordinates.
- Stage rebuild clears phase and hold; run reset also clears rewards. Old bubbles are parented to the previous world and are destroyed with it. Task completion changes the target on the next HUD update and cannot award repeatedly once phase reaches 2.
- Corrected rain handling from a binary `Rain01 > 0` friction switch to interpolation `Lerp(1, 0.62, Rain01)` so mechanical friction eases with the visible weather.
- `python Tools/test_v7_math.py`: six executable Python **mathematical model** tests pass, including clock braking at 30/60/120 Hz and speeds up to 11.3, interruption reset, two-stop single reward, directional escalator and continuous rain friction. The Python model mirrors formulas; it is **not execution of C# gameplay, Unity compilation, collision simulation or Player acceptance**.
- Reviewed cross-file member names/signatures; no obvious missing references found. Actual compiler verification is still required.

## Player QA coverage added, not yet executed

`RushJourneyQA` now drives the natural **risk** route's final clock-in using `QAInteract` (E) while continuing to command forward movement, without requesting Shift. Its result requires one clock-in plus >0.1 seconds of observed clock progress while E and movement were both commanded. Other natural routes retain stationary Shift clock-in regression coverage. All zero-input paths use `ClearInput`, which clears E as well as movement, brace and dash; resume-held-action coverage includes E. Optional errand routes remain manual acceptance rather than expanding/changing the existing authored path traversal tests. No new Player report has been generated or marked passed.

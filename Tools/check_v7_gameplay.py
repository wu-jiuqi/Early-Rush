#!/usr/bin/env python3
"""Source-contract checks only. Not Unity compilation or Player acceptance."""
from pathlib import Path
root=Path(__file__).resolve().parents[1]
game=(root/'Assets/Scripts/RushGame.cs').read_text()
journey=(root/'Assets/Scripts/RushJourney.cs').read_text()
daily=(root/'Assets/Scripts/RushDailyCommute.cs').read_text()
world=(root/'Assets/Scripts/RushWorld.cs').read_text()
checks={
'E input independent of Shift':'interactInput=!actionLocked&&Input.GetKey(KeyCode.E)' in game,
'interaction brakes desired movement':'requestedMove=interacting?Vector2.zero:moveInput' in game,
'held E included in release latch':'Input.GetKey(KeyCode.Return)||Input.GetKey(KeyCode.E)' in game,
'QA supports explicit interaction':'QAInteract' in game,
'clock accepts E and stationary legacy brace':'interactInput||(IsBracing&&moveInput.sqrMagnitude<.01f)' in journey,
'clock still rejects airborne/recovery':'!PlayerAirborne&&!PlayerRecovering&&velocity.magnitude<1.4f' in journey,
'pause cancels errand progress':'errandHold=0;interactInput=false;QAInteract=false;' in journey,
'reward does not alter elapsed/splits':'TimeLimit+=8' in daily and 'TotalTime=' not in daily and 'SplitTimes' not in daily,
'completed task cannot repeat':'if(errandPhase>=2)return;' in daily,
'errand spots checked for clearance':'StaticCircleRouteClear(route,p,.65f)' in daily,
'rain friction still permits brace':'Rain01>0&&!IsBracing' in game,
'escalator has directional belt':'targetVelocity+=Vector2.up*.85f' in game,
'background pauses':'void OnApplicationPause(bool paused)' in game,
'world signs routed to artwork':'RushArt.WorldSign(w,text,p,size,color,floor)' in world,
}
for name,ok in checks.items(): print(('PASS ' if ok else 'FAIL ')+name)
assert all(checks.values())
print(f'{len(checks)} source contracts passed; Unity build and interactive tests NOT performed.')

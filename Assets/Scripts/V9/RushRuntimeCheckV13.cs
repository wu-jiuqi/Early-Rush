using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace EarlyRush.V9
{
    /// <summary>Rendered Player checks driven through Tick and public action inputs. Fixture setup is explicit.</summary>
    public sealed class RushRuntimeCheckV13 : MonoBehaviour
    {
        const float Step = 1f / 60;
        [Serializable] public sealed class CheckRecord { public string name, evidence; public bool passed; }
        [Serializable] public sealed class SampleRecord
        {
            public string name, scope,densityAtStart,densityAtEnd,coverageAtStart,coverageAtEnd,observationLocation;
            public int stage, population, visiblePeak, movingPeople, charges, launches, blocks;
            public int aiPeople,distinctDestinations,progressingPeople,routePlans,destinationChanges,yieldEvents;
            public int dashLaunches,coffeeGuards,coffeeSpills,coffeeGuardSamples,coffeeSpillSamples,coffeeReturnSamples;
            public float peakNpcHeight;
            public int walkingSamples,yieldingSamples,waitingSignalSamples,waitingSamples,arrivedSamples,replanningSamples;
            public double elapsed;
            public float movementMeters, playerDistance, peakHeight;
            public Vector2 hotspotCenter,observerStart,cameraAtStart,cameraAtEnd;
        }
        [Serializable] public sealed class ViewObservation
        {
            public string screenshot,location,scope;
            public int stage,visiblePeople,peopleWithinRadius8;
            public double elapsed;
            public Vector2 player,cameraCenter,densityCenter;
            public float orthographicSize,aspect;
        }
        [Serializable] public sealed class PedestrianObservation
        {
            public int stage,id,routePlans,destinationChanges,yieldEvents;
            public double elapsed;public string state,profile,coffeeState;public Vector2 position,destination;
        }
        [Serializable] public sealed class Report
        {
            public string version = "v13-dash-map-route-runtime-2", utc, device, unity;
            public string scope = "Unmodified generated populations observed for 15 simulation seconds per stage (four 1/60-second ticks per rendered frame), plus isolated action/input-buffer fixtures and actual minimap pixels at three window sizes/eight directions. Only the observer is relocated for same-time main-route/task/bypass comparison views; NPCs are not moved or hidden in population observations. Counts cover every actor; individual snapshots sample IDs divisible by 23 plus all coffee coworkers. Actual camera/projection metadata are recorded. Accelerated simulation is NOT FPS, complete-route or human playtest evidence.";
            public bool passed;
            public float wallSeconds;
            public List<CheckRecord> checks = new List<CheckRecord>();
            public List<SampleRecord> samples = new List<SampleRecord>();
            public List<ViewObservation> views = new List<ViewObservation>();
            public List<MapObservation> mapViews = new List<MapObservation>();
            public List<PedestrianObservation> pedestrianSnapshots = new List<PedestrianObservation>();
            public List<string> screenshots = new List<string>();
            public List<string> errors = new List<string>();
        }
        [Serializable] public sealed class MapObservation
        {
            public bool expanded,markerPixelsFound;
            public int width,height;
            public Vector2 player,facing,markerGui,markerScreen;
            public Rect source,draw,uv;
            public float roundTripError;
            public RushMapPixelReviewV13.Result pixels;
        }

        readonly Report report = new Report();
        RushPlayV9 game;
        string output;
        float began;
        bool finished;

        void Awake()
        {
            Application.runInBackground = true;
            began = Time.realtimeSinceStartup;
            output = Path.GetFullPath(Path.Combine(Application.dataPath, "../QA/runtime-v13"));
            foreach (string arg in Environment.GetCommandLineArgs())
                if (arg.StartsWith("--v13-qa-output=", StringComparison.Ordinal)) output = Path.GetFullPath(arg.Substring("--v13-qa-output=".Length));
            Directory.CreateDirectory(output);
            report.utc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            report.device = SystemInfo.graphicsDeviceName; report.unity = Application.unityVersion;
            Application.logMessageReceived += OnLog;
        }

        void OnLog(string message, string stack, LogType type)
        {
            if (!finished && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)) report.errors.Add(message + "\n" + stack);
        }

        void Update()
        {
            // A real-time watchdog still terminates a stalled coroutine or a frozen simulation clock.
            if (!finished && Time.realtimeSinceStartup - began > 120)
            { report.errors.Add("Runtime suite exceeded the 120-second wall-clock budget"); Finish(); }
        }

        IEnumerator Start()
        {
            yield return null;
            game = GetComponent<RushPlayV9>();
            if (!game) game = FindFirstObjectByType<RushPlayV9>();
            if (!game) { report.errors.Add("RushPlayV9 not found"); Finish(); yield break; }
            IEnumerator suite = Suite();
            while (!finished)
            {
                bool moved = false; object next = null;
                try { moved = suite.MoveNext(); if (moved) next = suite.Current; }
                catch (Exception error) { report.errors.Add(error.ToString()); Finish(); }
                if (finished) yield break;
                if (!moved) break;
                yield return next;
            }
            if (!finished) Finish();
        }

        IEnumerator Suite()
        {
            for (int stage = 0; stage < 3; stage++)
            {
                game.Load(stage, 91026);
                yield return null;
                var run = game.run;
                // This changes only the initial observer position. It does not claim travel from spawn.
                ModuleV9 observationModule;Vector2 hotspotCenter;
                run.player = PopulationObservationPoint(run,out observationModule,out hotspotCenter); run.velocity = Vector2.zero;
                game.FrameForReview(); run.Begin(); game.RenderFrame();
                var sample = new SampleRecord { name = "pedestrians-stage-" + stage, stage = stage,
                    scope = "Unmodified generated AI actors at fixed module 1; arranged walkable shoulder observer start, zero movement input. Real contacts may displace the observer; screenshot metadata records actual camera centers. 15 simulated seconds, four ticks per render frame. Not FPS or complete-route evidence.",
                    population = ActivePeople(run),observationLocation=observationModule.kind,hotspotCenter=hotspotCenter,
                    observerStart=run.player,cameraAtStart=game.GameCamera.transform.position };
                Vector2 playerStart = run.player;
                var previous = new Dictionary<int, Vector2>(); var distances = new Dictionary<int, float>();
                var initialDistance = new Dictionary<int, float>(); var initialChanges = new Dictionary<int, int>();
                var destinations = new HashSet<Vector2Int>();
                foreach (var person in run.people)
                {
                    previous[person.id] = person.position; distances[person.id] = 0;
                    if(!person.hasPedestrianAI)continue;
                    sample.aiPeople++;destinations.Add(MapData.Cell(person.destination));
                    initialDistance[person.id]=Vector2.Distance(person.position,person.destination);
                    initialChanges[person.id]=person.destinationChanges;
                }
                sample.distinctDestinations=destinations.Count;sample.densityAtStart=run.CrowdDensitySummary();
                sample.coverageAtStart=run.MeasureRouteCrowdCoverage().Summary(true);
                RecordPedestrians(run);game.RenderFrame();
                yield return new WaitForEndOfFrame();CapturePopulationView(sample.name + "-t00.png",run,observationModule.kind,hotspotCenter,false);
                sample.visiblePeak=VisiblePeople(run);
                int ticks=0;bool signalWaitersStationary=true;
                while(ticks<900 && run.phase==RunPhase.Playing)
                {
                    for(int batch=0;batch<4&&ticks<900&&run.phase==RunPhase.Playing;batch++,ticks++)
                    {
                        run.Tick(Step, Vector2.zero);
                        foreach (var person in run.people)
                        {
                            if (person.exited || !previous.ContainsKey(person.id)) continue;
                            float distance = Vector2.Distance(previous[person.id], person.position);
                            distances[person.id] += distance; previous[person.id] = person.position; sample.movementMeters += distance;
                            if(person.kind==PersonKind.Coffee)
                            {
                                if(person.coffeeState==CoffeeState.GuardingCup)sample.coffeeGuardSamples++;
                                else if(person.coffeeState==CoffeeState.SpillReaction)sample.coffeeSpillSamples++;
                                else if(person.coffeeState==CoffeeState.ReturningEmpty)sample.coffeeReturnSamples++;
                            }
                            if(!person.hasPedestrianAI)continue;
                            switch(person.aiState)
                            {
                                case PedestrianState.Walking:sample.walkingSamples++;break;
                                case PedestrianState.Yielding:sample.yieldingSamples++;break;
                                case PedestrianState.WaitingSignal:sample.waitingSignalSamples++;signalWaitersStationary&=person.velocity.magnitude<.1f;break;
                                case PedestrianState.Waiting:sample.waitingSamples++;break;
                                case PedestrianState.Arrived:sample.arrivedSamples++;break;
                                case PedestrianState.Replanning:sample.replanningSamples++;break;
                            }
                        }
                    }
                    game.RenderFrame();sample.visiblePeak = Math.Max(sample.visiblePeak, VisiblePeople(run));
                    sample.peakHeight = Mathf.Max(sample.peakHeight, run.PlayerHeight);
                    if(ticks==300)
                    {
                        yield return new WaitForEndOfFrame();
                        CapturePopulationView(sample.name + "-t05.png",run,observationModule.kind,hotspotCenter,false);
                    }
                    yield return null;
                }
                foreach (float distance in distances.Values) if (distance > .1f) sample.movingPeople++;
                foreach(var person in run.people)if(person.hasPedestrianAI)
                {
                    sample.routePlans+=person.routePlans;sample.destinationChanges+=person.destinationChanges;sample.yieldEvents+=person.yieldEvents;
                    if(person.kind==PersonKind.Coffee){sample.coffeeGuards+=person.coffeeGuardCount;if(person.spilled)sample.coffeeSpills++;}
                    if(initialDistance.ContainsKey(person.id)&&(person.destinationChanges>initialChanges[person.id]||
                        Vector2.Distance(person.position,person.destination)<initialDistance[person.id]-.5f))sample.progressingPeople++;
                }
                sample.elapsed = run.elapsed; sample.playerDistance = Vector2.Distance(playerStart, run.player);sample.cameraAtEnd=game.GameCamera.transform.position;
                sample.densityAtEnd=run.CrowdDensitySummary();
                sample.coverageAtEnd=run.MeasureRouteCrowdCoverage().Summary(true);
                Debug.Log("V13_RUNTIME_DENSITY stage="+stage+" t00="+sample.densityAtStart+" t15="+sample.densityAtEnd);
                sample.charges = run.NpcChargeCount; sample.launches = run.PlayerLaunchCount; sample.blocks = run.BraceBlockCount;sample.dashLaunches=run.DashLaunchCount;
                report.samples.Add(sample);RecordPedestrians(run);
                Check(ticks==900 && run.elapsed>=14.99,"Full 15-second generated population observation stage="+stage,"ticks="+ticks+" elapsed="+run.elapsed);
                Check(sample.aiPeople>0&&sample.distinctDestinations>1,"Generated actors have independent destinations stage="+stage,"AI="+sample.aiPeople+" unique destination cells="+sample.distinctDestinations);
                Check(sample.population > 0 && sample.visiblePeak > 0, "Generated pedestrians are visible stage=" + stage, "active=" + sample.population + " visiblePeak=" + sample.visiblePeak);
                Check(sample.movingPeople > 0&&sample.progressingPeople>0, "Pedestrians make destination progress stage=" + stage, "moving=" + sample.movingPeople + " progressing="+sample.progressingPeople+" totalMeters=" + sample.movementMeters);
                Check(sample.routePlans>0,"Pedestrians execute path plans stage="+stage,"plans="+sample.routePlans+" destinationChanges="+sample.destinationChanges+" yields="+sample.yieldEvents);
                Check(signalWaitersStationary,"Observed signal-wait state remains stationary stage="+stage,"waitingSignalSamples="+sample.waitingSignalSamples+" (zero means no runtime signal-wait observation; model checks cover the rule)");
                Check(run.map.IsWalkable(run.player, RunV9.PlayerRadius), "Stationary observer remains in walkable area stage=" + stage, "zero movement input; actual contact displacement="+sample.playerDistance+" position="+run.player);
                yield return new WaitForEndOfFrame(); CapturePopulationView(sample.name + "-t15.png",run,observationModule.kind,hotspotCenter,false); SaveTrace(run, sample.name);

                // Comparison views of the same t=15 population state. These are camera samples,
                // never traversal/task-completion evidence: only the observer is moved, no Tick.
                foreach(var task in run.map.tasks)
                {
                    PlaceComparisonObserver(run,task.entrance);
                    yield return new WaitForEndOfFrame();
                    CapturePopulationView(sample.name+"-task-"+task.index+"-entry-t15.png",run,task.kind+" task entrance",task.entrance,true);
                    PlaceComparisonObserver(run,task.center);
                    yield return new WaitForEndOfFrame();
                    CapturePopulationView(sample.name+"-task-"+task.index+"-workspace-t15.png",run,task.kind+" task workspace",task.center,true);
                }
                ZoneV9 quietLoop=run.map.zones.Find(zone=>zone.moduleIndex==observationModule.index&&zone.kind=="loop-lane");
                if(quietLoop==null)throw new InvalidOperationException("Module 1 has no comparison loop lane");
                PlaceComparisonObserver(run,quietLoop.bounds.center);
                yield return new WaitForEndOfFrame();
                CapturePopulationView(sample.name+"-quiet-loop-t15.png",run,observationModule.kind+" parallel loop",quietLoop.bounds.center,true);

                // The former four-peak view can hide empty connecting roads. Show every other
                // module as well, without changing the generated population or advancing time.
                for(int moduleIndex=0;moduleIndex<run.map.modules.Count;moduleIndex+=2)
                {
                    var module=run.map.modules[moduleIndex];Vector2 point=(module.entrance+module.exit)*.5f;
                    PlaceComparisonObserver(run,point);
                    yield return new WaitForEndOfFrame();
                    CapturePopulationView(sample.name+"-route-module-"+moduleIndex+"-t15.png",run,module.kind+" through-route",point,true);
                }

            }

            game.Load(0, 91026); yield return null;
            var dash = game.run; HideGeneratedPeople(dash); dash.Begin(); game.FrameForReview();
            Vector2 dashStart = dash.player;
            Check(dash.TryDash(Vector2.up), "Dash accepts a real public input", "isolated local fixture at spawn");
            for (int tick = 0; tick < 6; tick++) { dash.Tick(Step, Vector2.up); game.RenderFrame(); yield return null; }
            Check(dash.IsDashing && Vector2.Distance(dashStart, dash.player) > dash.config.walkSpeed * .1f, "Dash has observed increased displacement", "distance=" + Vector2.Distance(dashStart, dash.player));
            yield return new WaitForEndOfFrame(); Capture("fixture-player-dash.png");
            dash.SetBracing(true); dash.Tick(Step, Vector2.up); game.RenderFrame();
            Check(dash.IsBracing && !dash.IsDashing, "Steady stance cancels dash", "public SetBracing(true), then real Tick");
            yield return new WaitForEndOfFrame(); Capture("fixture-player-brace.png");
            double pausedTime = dash.elapsed; Vector2 pausedPosition = dash.player; float cooldown = dash.DashReady01;
            dash.Pause(); Vector3 cameraPosition = game.GameCamera.transform.position;
            dash.Tick(5, Vector2.up);
            for (int frame = 0; frame < 3; frame++) { game.RenderFrame(); yield return null; }
            Check(dash.elapsed == pausedTime && dash.player == pausedPosition && dash.DashReady01 == cooldown && game.GameCamera.transform.position == cameraPosition,
                "Pause freezes actual runtime simulation and follow camera", "three rendered frames and a rejected five-second Tick");
            SaveTrace(dash, "fixture-dash-brace-pause");

            // Real dash contact against one ordinary actor, without directly setting flight results.
            game.Load(0,91026);yield return null;
            var impact=game.run;var target=PrepareChargeFixture(impact);
            Vector2 impactDirection=(target.position-impact.player).normalized;
            target.canRush=false;impact.player=target.position-impactDirection*1.35f;impact.velocity=Vector2.zero;
            Vector2 targetStart=target.position;impact.Begin();game.FrameForReview();
            Check(impact.TryDash(impactDirection),"Dash impact fixture accepts public dash input","ordinary non-rushing actor; initial positions explicitly arranged");
            bool impactCaptured=false;float npcHeight=0;
            for(int tick=0;tick<75;tick++)
            {
                impact.Tick(Step,impactDirection);game.RenderFrame();npcHeight=Mathf.Max(npcHeight,target.dashHeight);
                if(!impactCaptured&&target.dashAirborne&&target.dashHeight>.6f)
                {
                    impactCaptured=true;yield return new WaitForEndOfFrame();Capture("fixture-player-dash-launch.png");
                }
                yield return null;
            }
            Check(impact.DashLaunchCount==1&&npcHeight>.6f&&impactCaptured,"Player dash causes visible ordinary-NPC flight","launches="+impact.DashLaunchCount+" peakHeight="+npcHeight);
            Check(!target.dashAirborne&&target.dashHeight==0&&Vector2.Distance(targetStart,target.position)>.1f,
                "Dash target lands after physical displacement","distance="+Vector2.Distance(targetStart,target.position)+" airborne="+target.dashAirborne);
            report.samples.Add(new SampleRecord{name="fixture-player-dash-launch",stage=0,population=1,elapsed=impact.elapsed,dashLaunches=impact.DashLaunchCount,peakNpcHeight=npcHeight,
                scope="Controlled ordinary-actor fixture; only initial positions/canRush are arranged. TryDash and Tick produce contact, displacement, flight and landing."});
            SaveTrace(impact,"fixture-player-dash-launch");

            // The shared test fixture establishes only initial conditions. Guard/spill outcomes develop via Tick.
            PersonV9 coffeePerson;Vector2 coffeeDirection;
            RunV9 coffee=RushCoffeeAITestsV12.CreateDashFixture(out coffeePerson,out coffeeDirection);
            game.LoadModelForReview(coffee);yield return null;coffee.Begin();game.FrameForReview();
            Check(coffee.TryDash(coffeeDirection),"Coffee reaction fixture accepts public dash input","no spilled/coffeeState/task-result assignment");
            bool guardCaptured=false,spillCaptured=false;
            for(int tick=0;tick<150;tick++)
            {
                Vector2 coffeeInput=coffeePerson.spilled?Vector2.zero:tick<30?coffeeDirection:(coffeePerson.position-coffee.player).normalized;
                coffee.Tick(Step,coffeeInput);game.RenderFrame();
                if(!guardCaptured&&coffeePerson.coffeeState==CoffeeState.GuardingCup)
                {guardCaptured=true;yield return new WaitForEndOfFrame();Capture("fixture-coffee-guard.png");}
                if(!spillCaptured&&coffeePerson.coffeeState==CoffeeState.SpillReaction)
                {spillCaptured=true;yield return new WaitForEndOfFrame();Capture("fixture-coffee-spill.png");}
                yield return null;
            }
            Check(guardCaptured&&coffeePerson.coffeeGuardCount>0,"Coffee carrier visibly guards before contact","guardCount="+coffeePerson.coffeeGuardCount);
            Check(spillCaptured&&coffeePerson.spilled&&coffee.tasks[0].count==1,"Real contact triggers one coffee spill and visible reaction","spilled="+coffeePerson.spilled+" ledger="+coffee.tasks[0].count);
            Check(coffee.DashLaunchCount==0,"Coffee collision remains a task exception","dash launches="+coffee.DashLaunchCount);
            report.samples.Add(new SampleRecord{name="fixture-coffee-guard-spill",stage=2,population=1,elapsed=coffee.elapsed,coffeeGuards=coffeePerson.coffeeGuardCount,coffeeSpills=coffee.tasks[0].count,
                scope="Controlled coffee AI fixture from CreateDashFixture. Public dash input and real Tick trigger guard and spill; no reaction or result is assigned."});
            SaveTrace(coffee,"fixture-coffee-guard-spill");

            for (int defence = 0; defence < 2; defence++)
            {
                game.Load(0, 91026); yield return null;
                var run = game.run; PersonV9 attacker = PrepareChargeFixture(run);
                bool braced = defence == 1;
                run.Begin(); run.SetBracing(braced); game.FrameForReview();
                bool sawWindup = false, capturedWindup = false, capturedImpact = false;
                Vector2 start = run.player; float height = 0;
                for (int tick = 0; tick < 180 && run.phase == RunPhase.Playing; tick++)
                {
                    run.Tick(Step, Vector2.zero); game.RenderFrame();
                    height = Mathf.Max(height, run.PlayerHeight);
                    if (attacker.actionPhase == NpcActionPhase.Windup)
                    {
                        sawWindup = true;
                        if (!capturedWindup && run.NpcWindup01(attacker) >= .25f)
                        {
                            capturedWindup = true;
                            yield return new WaitForEndOfFrame(); Capture(braced ? "fixture-braced-npc-windup.png" : "fixture-unbraced-npc-windup.png");
                        }
                    }
                    if (!capturedImpact && (braced ? run.BraceBlockCount > 0 : run.PlayerHeight > .55f))
                    {
                        capturedImpact = true;
                        yield return new WaitForEndOfFrame(); Capture(braced ? "fixture-brace-block.png" : "fixture-player-knockback.png");
                    }
                    if (capturedImpact && !run.IsAirborne && attacker.actionPhase == NpcActionPhase.Recovery && (braced || tick > 65)) break;
                    yield return null;
                }
                string name = braced ? "fixture-brace-block" : "fixture-unbraced-knockback";
                Check(sawWindup && run.NpcChargeCount > 0, name + " observes windup before an actual charge", "charges=" + run.NpcChargeCount + " windup=" + sawWindup);
                Check(braced ? run.BraceBlockCount == 1 && run.PlayerLaunchCount == 0 : run.PlayerLaunchCount == 1 && height > 0,
                    name + " observed outcome", "blocks=" + run.BraceBlockCount + " launches=" + run.PlayerLaunchCount + " peakHeight=" + height);
                Check(capturedImpact, name + " captured the observed outcome", "No attack-result or task-state assignment is used");
                for (int tick = 0; tick < 40; tick++) { run.Tick(Step, Vector2.zero); game.RenderFrame(); yield return null; }
                Check(!run.IsAirborne && run.PlayerHeight == 0, name + " leaves bounded recovery", "height=" + run.PlayerHeight);
                report.samples.Add(new SampleRecord { name = name, scope = "Legacy action mechanism fixture without pedestrian AI: generated actors hidden; one ordinary attacker and player arranged on a verified lane. Attack state evolves through Tick. This does not certify V13 AI impatience decisions.", stage = 0, population = 1,
                    charges = run.NpcChargeCount, launches = run.PlayerLaunchCount, blocks = run.BraceBlockCount, elapsed = run.elapsed, peakHeight = height, playerDistance = Vector2.Distance(start, run.player) });
                SaveTrace(run, name);
            }
            IEnumerator cadence=CheckBufferedDash();while(cadence.MoveNext())yield return cadence.Current;
            IEnumerator minimap=CheckMinimapRendering();while(minimap.MoveNext())yield return minimap.Current;
            yield return null;
        }

        IEnumerator CheckBufferedDash()
        {
            var run=new RunV9(0,91026,new ConfigV9{pedestrianStreetCount=0,pedestrianMetroCount=0,pedestrianOfficeCount=0});
            game.LoadModelForReview(run);yield return null;run.Begin();game.FrameForReview();
            Check(run.PressDash(Vector2.up),"V13 real key press begins a dash","same PressDash API used by gameplay Space GetKeyDown");
            Check(AdvanceToTime(run,.55,Vector2.up),"Dash fixture reaches the late-press time","bounded simulation; paused input cannot hang the main thread");
            Check(run.PressDash(Vector2.up)&&run.DashBuffered,"A late cooldown press is buffered","elapsed="+run.elapsed+" starts="+run.DashStartCount);
            game.RenderFrame();yield return new WaitForEndOfFrame();Capture("fixture-dash-buffer.png");
            Check(AdvanceToTime(run,.72,Vector2.up),"Dash fixture reaches the next launch time","bounded simulation; paused input cannot hang the main thread");
            Check(run.DashStartCount==2&&run.IsDashing&&!run.DashBuffered,"Buffered Space starts exactly one next dash","elapsed="+run.elapsed+" starts="+run.DashStartCount);
            game.RenderFrame();yield return new WaitForEndOfFrame();Capture("fixture-dash-repeat.png");
            run.Tick(.3f,Vector2.zero);run.Tick(1,Vector2.zero);
            Check(run.DashStartCount==2&&!run.IsDashing&&run.velocity.magnitude<.05f,"No repeated dash or lingering glide without another press","starts="+run.DashStartCount+" speed="+run.velocity.magnitude);
            SaveTrace(run,"fixture-dash-cadence");
        }

        static bool AdvanceToTime(RunV9 run,double target,Vector2 input)
        {
            for(int tick=0;tick<120&&run.elapsed<target&&run.phase==RunPhase.Playing;tick++)run.Tick(Step,input);
            return run.elapsed>=target;
        }

        IEnumerator CheckMinimapRendering()
        {
            var run=new RunV9(0,91026,new ConfigV9{pedestrianStreetCount=0,pedestrianMetroCount=0,pedestrianOfficeCount=0});
            game.LoadModelForReview(run);yield return null;run.Begin();
            // Initial camera/player/facing arrangements are visual calibration fixtures only.
            // No task completion, travelled route, or keyboard resize is claimed here.
            PlaceComparisonObserver(run,run.map.PointAtDistance(run.map.PathLength*.31f));
            var sizes=new[]{new Vector2Int(1280,720),new Vector2Int(1024,768),new Vector2Int(1600,900)};
            var directions=new[]{Vector2.up,Vector2.right,Vector2.down,Vector2.left,new Vector2(1,1).normalized,new Vector2(-1,1).normalized,new Vector2(1,-1).normalized,new Vector2(-1,-1).normalized};
            foreach(var size in sizes)
            {
                Screen.SetResolution(size.x,size.y,FullScreenMode.Windowed);
                yield return null;yield return null;game.FrameForReview();
                Check(Screen.width==size.x&&Screen.height==size.y,"Requested minimap viewport is active "+size,"actual="+Screen.width+"x"+Screen.height);
                foreach(bool expanded in new[]{false,true})
                {
                    game.SetExpandedMapForReview(expanded);
                    for(int direction=0;direction<directions.Length;direction++)
                    {
                        run.facing=directions[direction];game.RenderFrame();
                        yield return new WaitForEndOfFrame();
                        var projection=game.CreateMinimapProjection(game.GetMapPanelForReview(expanded),expanded);
                        Vector2 gui=projection.WorldToGui(run.player),pixel=game.GuiToScreenPixel(gui);
                        float error=Vector2.Distance(projection.GuiToWorld(gui),run.player);
                        var capture=ScreenCapture.CaptureScreenshotAsTexture();
                        if(!capture)throw new InvalidOperationException("Minimap capture failed");
                        RushMapPixelReviewV13.Result pixelReview=null;
                        try
                        {
                            if(capture.width!=Screen.width||capture.height!=Screen.height)
                                throw new InvalidOperationException("Screen and minimap capture pixel dimensions differ");
                            pixelReview=RushMapPixelReviewV13.Review(capture,run,game.GetMapPanelForReview(expanded),expanded,direction==0);
                            if(direction==4)
                            {
                                string name="minimap-"+Screen.width+"x"+Screen.height+(expanded?"-full":"-local")+".png";
                                File.WriteAllBytes(Path.Combine(output,name),capture.EncodeToPNG());report.screenshots.Add(name);
                            }
                        }
                        finally{Destroy(capture);}
                        report.mapViews.Add(new MapObservation{expanded=expanded,width=Screen.width,height=Screen.height,player=run.player,facing=run.facing,
                            markerGui=gui,markerScreen=pixel,source=projection.SourceRect,draw=projection.DrawRect,uv=projection.UvRect,roundTripError=error,markerPixelsFound=pixelReview.goldPixels>0,pixels=pixelReview});
                        Check(pixelReview.arrowPassed&&error<.001f,"Rendered player marker center and direction align "+Screen.width+"x"+Screen.height+" expanded="+expanded+" direction="+direction,
                            "world="+run.player+" GUI="+gui+" topLeftPixel="+pixel+" inverseError="+error+" "+pixelReview.ArrowSummary());
                        if(pixelReview.terrainChecked)Check(pixelReview.terrainPassed,"Rendered map landmarks align "+Screen.width+"x"+Screen.height+" expanded="+expanded,pixelReview.TerrainSummary());
                    }
                }
            }
            game.SetExpandedMapForReview(false);Screen.SetResolution(1280,720,FullScreenMode.Windowed);yield return null;
        }

        void RecordPedestrians(RunV9 run)
        {
            // Representative IDs plus all coffee coworkers; counts above still cover everybody.
            foreach(var person in run.people)if(person.hasPedestrianAI&&!person.exited&&(person.id%23==0||person.kind==PersonKind.Coffee))
                report.pedestrianSnapshots.Add(new PedestrianObservation{stage=run.stage,id=person.id,elapsed=run.elapsed,
                    position=person.position,destination=person.destination,state=person.aiState.ToString(),profile=person.profile.ToString(),coffeeState=person.kind==PersonKind.Coffee?person.coffeeState.ToString():"",
                    routePlans=person.routePlans,destinationChanges=person.destinationChanges,yieldEvents=person.yieldEvents});
        }
        static void HideGeneratedPeople(RunV9 run) { foreach (var person in run.people) person.exited = true; }

        static PersonV9 PrepareChargeFixture(RunV9 run)
        {
            HideGeneratedPeople(run);
            for (float distance = 18; distance < run.map.PathLength - 18; distance += 2)
            {
                Vector2 point = run.map.PointAtDistance(distance), direction = (run.map.PointAtDistance(distance + 1) - point).normalized;
                bool clear = direction.sqrMagnitude > .9f;
                for (float along = 0; along < 4; along += .3f)
                {
                    Vector2 sample = point + direction * along;
                    if (run.IsCrowdProtected(sample, 1) || !run.map.IsWalkable(sample, .7f)) clear = false;
                }
                if (!clear) continue;
                run.player = point + direction * 2.2f; run.velocity = Vector2.zero;
                var attacker = new PersonV9 { id = 8100, kind = PersonKind.Ordinary, position = point, home = point, target = run.player, canRush = true, actionInitialized = true };
                run.people.Add(attacker); return attacker;
            }
            throw new InvalidOperationException("No clear unprotected lane for the runtime charge fixture");
        }

        static Vector2 PopulationObservationPoint(RunV9 run,out ModuleV9 module,out Vector2 hotspotCenter)
        {
            module=run.map.modules.Find(candidate=>candidate.index==1);
            if(module==null)throw new InvalidOperationException("No semantic module 1 for fixed population observation");
            hotspotCenter=(module.entrance+module.exit)*.5f;
            Vector2 direction=(module.exit-module.entrance).normalized,shoulder=new Vector2(-direction.y,direction.x);
            // Use the same semantic location for every density revision, never search for a
            // flattering maximum. The observer stands beside the central flow, not across it.
            return FindReviewPoint(run,hotspotCenter+shoulder*3.25f);
        }

        static Vector2 FindReviewPoint(RunV9 run,Vector2 preferred)
        {
            for(int ring=0;ring<=6;ring++)for(int x=-ring;x<=ring;x++)for(int y=-ring;y<=ring;y++)
            {
                if(Math.Max(Math.Abs(x),Math.Abs(y))!=ring)continue;
                Vector2 point=preferred+new Vector2(x,y)*.5f;
                if(!run.map.IsWalkable(point,RunV9.PlayerRadius))continue;
                bool clear=true;
                foreach(var person in run.people)if(!person.exited&&(person.position-point).sqrMagnitude<Mathf.Pow(person.radius+RunV9.PlayerRadius+.05f,2)){clear=false;break;}
                if(!clear)continue;
                foreach(var body in run.bodies)if((body.position-point).sqrMagnitude<Mathf.Pow(body.radius+RunV9.PlayerRadius+.05f,2)){clear=false;break;}
                if(!clear)continue;
                foreach(var fixture in run.fixtures)if((fixture.kind==FixtureKind.FlipBoard||fixture.kind==FixtureKind.Capsule)&&
                    (fixture.position-point).sqrMagnitude<Mathf.Pow(fixture.radius+RunV9.PlayerRadius+.05f,2)){clear=false;break;}
                if(clear)return point;
            }
            throw new InvalidOperationException("No clear walkable observer point near "+preferred);
        }

        void PlaceComparisonObserver(RunV9 run,Vector2 preferred)
        {
            run.player=FindReviewPoint(run,preferred);run.velocity=Vector2.zero;
            game.FrameForReview();game.RenderFrame();
        }

        void CapturePopulationView(string name,RunV9 run,string location,Vector2 densityCenter,bool relocated)
        {
            int nearby=0;foreach(var person in run.people)if(!person.exited&&(person.position-densityCenter).sqrMagnitude<64)nearby++;
            Capture(name);
            report.views.Add(new ViewObservation{screenshot=name,location=location,stage=run.stage,elapsed=run.elapsed,
                player=run.player,cameraCenter=game.GameCamera.transform.position,densityCenter=densityCenter,
                orthographicSize=game.GameCamera.orthographicSize,aspect=game.GameCamera.aspect,
                visiblePeople=VisiblePeople(run),peopleWithinRadius8=nearby,
                scope=relocated?"Arranged comparison view: observer relocated after the 15-second observation; identical NPC state, no further Tick. Not travel or task-completion evidence.":
                    "Fixed semantic hotspot, arranged shoulder start and zero movement input. Original generated NPCs evolve only through Tick; actual camera center recorded if contacts move the observer."});
            Debug.Log("V13_RUNTIME_VIEW "+name+" location="+location+" elapsed="+run.elapsed+" camera="+game.GameCamera.transform.position+" radius8="+nearby);
        }

        static int ActivePeople(RunV9 run) { int count = 0; foreach (var person in run.people) if (!person.exited) count++; return count; }
        int VisiblePeople(RunV9 run)
        {
            int count = 0;
            foreach (var person in run.people)
            {
                Vector3 view = game.GameCamera.WorldToViewportPoint(new Vector3(person.position.x, person.position.y, 0));
                if (!person.exited && view.z > 0 && view.x >= 0 && view.x <= 1 && view.y >= 0 && view.y <= 1) count++;
            }
            return count;
        }

        void Check(bool passed, string name, string evidence)
        {
            report.checks.Add(new CheckRecord { name = name, passed = passed, evidence = evidence });
            Debug.Log("V13_RUNTIME_CHECK " + (passed ? "PASS " : "FAIL ") + name + " " + evidence);
        }

        void Capture(string name)
        {
            Texture2D image = ScreenCapture.CaptureScreenshotAsTexture();
            if (!image) throw new InvalidOperationException("Screenshot capture failed: " + name);
            try { File.WriteAllBytes(Path.Combine(output, name), image.EncodeToPNG()); report.screenshots.Add(name); }
            finally { Destroy(image); }
        }

        void SaveTrace(RunV9 run, string name)
        {
            var text = new StringBuilder("time,type,id,x,y\n"); var culture = CultureInfo.InvariantCulture;
            foreach (var entry in run.telemetry)
                text.AppendLine(string.Join(",", entry.time.ToString(culture), entry.type, entry.id.ToString(culture), entry.position.x.ToString(culture), entry.position.y.ToString(culture)));
            File.WriteAllText(Path.Combine(output, name + ".csv"), text.ToString());
        }

        void Finish()
        {
            if (finished) return;
            finished = true; report.wallSeconds = Time.realtimeSinceStartup - began;
            report.passed = report.errors.Count == 0 && report.checks.Count > 0 && report.checks.TrueForAll(check => check.passed);
            File.WriteAllText(Path.Combine(output, "runtime-report.json"), JsonUtility.ToJson(report, true));
            File.WriteAllText(Path.Combine(output, "README.txt"), report.scope + "\nPopulation t00/t05/t15 frames use the fixed module-1 observer, whose initial position is arranged. Main-route modules 0/2/4/6/8, tasks and parallel-loop comparison frames relocate only the observer at t15, without advancing the same population. These views are not actual travel. coverageAtStart/End sample the whole route rather than only hotspots. mapViews records actual gold-marker pixels at 1280x720, 1024x768 and 1600x900, local/full maps and eight directions; the screenshot calibrations arrange player position/facing rather than claiming keyboard travel. Action fixtures use public inputs and real contacts.\nPassed: " + report.passed + "\nErrors: " + report.errors.Count);
            Debug.Log("V13_RUNTIME_RESULT passed=" + report.passed + " checks=" + report.checks.Count + " errors=" + report.errors.Count);
            Application.Quit(report.passed ? 0 : 1);
        }

        void OnDestroy() { Application.logMessageReceived -= OnLog; }
    }
}

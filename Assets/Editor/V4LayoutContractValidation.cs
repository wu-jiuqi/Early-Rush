using System;
using UnityEditor;
using UnityEngine;
using EarlyRush.V9;

/// <summary>Read-only source/DTO tests. Not a gameplay, navigation or timing certification.</summary>
public static class V4LayoutContractValidation
{
    static int checks;
    static void Check(bool value,string message) { checks++;if(!value)throw new Exception("ArtV4 contract: "+message); }
    static void Equal(Vector2 actual,Vector2 expected,string message) { Check(Vector2.Distance(actual,expected)<.001f,message); }
    [MenuItem("Early Rush/Art V4/Validate Layout DTO Contract (Read Only)")]
    public static void Validate()
    {
        checks=0;
        foreach(var name in new[]{"street","metro","office"})
        {
            var l=RushV4Layout.Load(name);Check(l.tasks.Length==3,name+" three tasks");
            string prefix=name=="street"?"S":name=="metro"?"M":"O";
            for(int i=1;i<=3;i++) { var t=l.TaskWorld(prefix+i); Check(t.id==prefix+i,"ID lookup "+prefix+i);Equal(l.Pixel(t.position),l.TaskById(prefix+i).position,"coordinate roundtrip"); }
            Equal(l.World(new Vector2(0,l.worldHeight)),Vector2.zero,"worldHeight conversion");
        }
        var metro=RushV4Layout.Load("metro");var sections=metro.MainRouteSections();
        Check(sections.Length==2,"metro has two route sections");Check(sections[0].Length==11&&sections[1].Length==13,"metro section cardinality");
        Equal(metro.Pixel(sections[0][10]),new Vector2(4352,11008),"first section end");
        Equal(metro.Pixel(sections[1][0]),new Vector2(4352,9472),"second section start");
        Check(metro.TaskById("M1").kind=="boarding"&&metro.TaskById("M2").kind=="capsule","tasks bound by ID, not source order");
        var portal=metro.PortalWorld("M1");Check(portal!=null&&portal.id=="train-journey"&&portal.taskId=="M1","metro portal identity");
        Equal(metro.Pixel(portal.from),new Vector2(4352,11088),"portal from");Equal(metro.Pixel(portal.to),new Vector2(4352,9552),"portal to");
        Check(Mathf.Abs(portal.radius-3.25f)<.001f,"130px portal radius");Equal(metro.TaskWorld("M1").exit.Value,portal.to,"M1 exit agrees with portal");
        Check(metro.TaskWorld("M3").patches.Length==3,"three source water patches");
        var office=RushV4Layout.Load("office");var chairs=office.TaskWorld("O3");
        Check(chairs.pickups.Length==3&&chairs.targetZones.Length==3,"three chairs and three targets (not five)");
        Vector2[] sourceChairs={new Vector2(3455,3390),new Vector2(3805,3460),new Vector2(4155,3390)};
        for(int i=0;i<3;i++)
        {
            var chair=chairs.pickups[i];Check(chair.id=="O3-office-chair-"+i,"chair stable source ID");Equal(office.Pixel(chair.position),sourceChairs[i],"chair spawn "+i);
            Check(chair.target.HasValue&&chair.target.Value==chairs.targetZones[i],"chair-target mapping "+i);
            Check(Mathf.Abs(chair.target.Value.width-4.5f)<.001f&&Mathf.Abs(chair.target.Value.height-3.75f)<.001f,"chair target size");
        }
        var files=office.TaskWorld("O2");Check(files.pickups.Length==3&&files.deliveries.Length==3,"three files and deliveries");
        var colors=new[]{"blue","pink","green"};
        for(int i=0;i<3;i++) { Check(files.pickups[i].id=="O2-folder-"+colors[i],"folder ID");Check(files.deliveries[i].id=="O2-leader-"+colors[i]&&files.deliveries[i].portrait==colors[i],"matching leader ID/color"); }
        Check(office.TaskWorld("O1").actors.Length==3,"three authored coffee actors");Check(office.PortalWorld("O1")==null,"no office portal");
        Check(RushV4Layout.Load("street").TaskWorld("S2").pickups.Length==2,"two street wheels");
        // Guard against accidentally reintroducing the existing street importer's fixed height.
        var synthetic=new RushV4Layout{worldHeight=1000};Equal(synthetic.World(new Vector2(80,120)),new Vector2(2,22),"conversion uses instance worldHeight");
        Check(!metro.TaskWorld("M1").zone.HasValue,"missing M1 zone remains optional after JsonUtility");
        Check(!office.TaskWorld("O1").zone.HasValue&&!office.TaskWorld("O2").zone.HasValue,"missing office zones remain optional");
        Check(!office.TaskWorld("O2").pickups[0].target.HasValue,"document pickup has no chair-style target");
        Check(!office.TaskWorld("O3").exit.HasValue,"missing or empty exit is optional");
        Check(!synthetic.OptionalWorldRect(new RushV4Layout.Box()).HasValue,"JsonUtility zero-box sentinel");
        Check(!synthetic.OptionalWorldPoint(new float[0]).HasValue,"JsonUtility empty-array sentinel");
        bool malformedRejected=false;
        try { synthetic.OptionalWorldPoint(new float[]{1}); } catch(InvalidOperationException) { malformedRejected=true; }
        Check(malformedRejected,"nonempty malformed point still rejected");
        Debug.Log("ART_V4_LAYOUT_CONTRACT_PASS checks="+checks+" (read-only parsing/coordinates only; no gameplay or timing claim)");
    }
    [MenuItem("Early Rush/Art V4/Validate Interior Run Initialization (Read Only)")]
    public static void ValidateRuntimeInitialization()
    {
        Validate();
        // Constructor only: no Begin/Tick/teleports, rewards, task completion or navigation writes.
        // Ambient crowd is omitted to keep this a bounded entity-initialization contract test.
        var office=new RunV9(2,RushV4InteriorMap.Seed,new ConfigV9{pedestrianStreetCount=0,pedestrianMetroCount=0,pedestrianOfficeCount=0});
        Check(office.map.authoredLayout!=null,"office experimental seed must route to authored map");
        var chairs=office.map.authoredLayout.TaskWorld("O3");var files=office.map.authoredLayout.TaskWorld("O2");
        var bodies=office.bodies.FindAll(b=>b.kind==BodyKind.Chair);
        var sockets=office.fixtures.FindAll(f=>f.kind==FixtureKind.ChairSocket);
        var desks=office.fixtures.FindAll(f=>f.kind==FixtureKind.FileDesk);
        Check(bodies.Count==3&&sockets.Count==3&&desks.Count==3,"runtime exactly 3 chairs/3 sockets/3 desks");
        Check(office.tasks[2].id=="O3"&&office.tasks[2].limit==3,"runtime O3 limit from authored three chairs, not default five");
        Check(office.tasks[1].id=="O2"&&office.tasks[1].limit==3,"runtime O2 limit three deliveries");
        for(int i=0;i<chairs.pickups.Length;i++)
        {
            var source=chairs.pickups[i];Equal(bodies[i].position,source.position,"runtime chair position "+source.id);
            Equal(bodies[i].home,source.position,"runtime rescue home "+source.id);
            var socket=sockets.Find(f=>f.index==i);Check(socket!=null,"socket source ordinal "+source.id);
            Equal(socket.position,source.target.Value.center,"runtime socket target "+source.id);
            Check(!socket.occupied&&!bodies[i].done&&!bodies[i].touched,"chair is not pre-awarded "+source.id);
            Check(office.map.IsWalkable(bodies[i].position,bodies[i].radius),"chair body footprint clear "+source.id);
        }
        for(int i=0;i<files.deliveries.Length;i++)
        {
            var source=files.deliveries[i];var desk=desks.Find(f=>f.index==i);Check(desk!=null,"delivery source ordinal "+source.id);
            Equal(desk.position,source.position,"runtime destination "+source.id);Check(!office.IsDelivered(i),"delivery not pre-awarded "+source.id);
        }
        var coffeeSources=office.map.authoredLayout.TaskWorld("O1").actors;
        var coworkers=office.people.FindAll(c=>c.kind==PersonKind.Coffee);
        Check(coworkers.Count==3,"exactly three authored coffee coworkers, no generic extra cups");
        for(int i=0;i<coffeeSources.Length;i++)
        {
            var source=coffeeSources[i];var coworker=coworkers.Find(c=>c.authoredCoffeeId==source.id);
            Check(coworker!=null&&coworker.authoredCoffee&&coworker.hasPedestrianAI&&coworker.coffeeAIInitialized,"source coworker has active initialized AI "+source.id);
            Equal(coworker.position,source.position,"coffee source start "+source.id);
            Equal(coworker.authoredCoffeeA,source.a,"coffee source endpoint A "+source.id);
            Equal(coworker.authoredCoffeeB,source.b,"coffee source endpoint B "+source.id);
            Check(coworker.PlannedPath!=null&&coworker.PlannedPath.Count>=2&&!coworker.spilled,"coffee has route and unspilled cup "+source.id);
        }
        Check(office.phase==RunPhase.Ready&&office.elapsed==0&&office.Completed==0,"office constructor does not simulate or reward");
        var metro=new RunV9(1,RushV4InteriorMap.Seed,new ConfigV9{pedestrianStreetCount=0,pedestrianMetroCount=0,pedestrianOfficeCount=0});
        Check(metro.map.authoredLayout!=null,"metro experimental seed must route to authored map");
        var portal=metro.map.authoredLayout.PortalWorld("M1");Check(portal!=null,"runtime metro portal exists");
        Check(metro.map.IsWalkable(portal.from,RunV9.PlayerRadius)&&metro.map.IsWalkable(portal.to,RunV9.PlayerRadius),"runtime portal endpoints clear");
        Check(metro.map.routeBreaks.Count==1&&metro.map.routeBreaks.Contains(11),"runtime portal gap not a walking route edge");
        var cloth=metro.bodies.FindAll(b=>b.kind==BodyKind.Cloth);Check(cloth.Count==1,"runtime one cloth (M3 has no pickups array)");
        var clothSource=Array.Find(metro.map.authoredLayout.objects,o=>o.id=="M3-cloth");Check(clothSource!=null,"source M3-cloth object exists");
        var clothPosition=metro.map.authoredLayout.World(new Vector2(clothSource.x+clothSource.w/2,clothSource.y+clothSource.h));
        Equal(cloth[0].position,clothPosition,"runtime cloth matches source object foot point");
        Check(metro.dirt.Count>0&&metro.tasks[2].limit==metro.dirt.Count,"runtime nonempty dirt and matching M3 goal");
        var clean=metro.map.authoredLayout.TaskWorld("M3");
        foreach(var patch in clean.patches)
            Check(metro.dirt.Exists(d=>patch.bounds.Contains(d.position)),"each authored patch has cleanable samples "+patch.id);
        foreach(var dirt in metro.dirt)
        {
            Check(!dirt.cleaned&&metro.map.IsWalkable(dirt.position,.05f),"dirt starts uncleaned and accessible");
            Check(Array.Exists(clean.patches,p=>p.bounds.Contains(dirt.position)),"dirt belongs to source patch, not legacy grid");
        }
        Check(metro.phase==RunPhase.Ready&&metro.elapsed==0&&metro.Completed==0&&metro.trainPhase==TrainPhase.Waiting,"metro constructor does not simulate, transfer or reward");
        Equal(metro.player,metro.map.spawn,"metro player remains at spawn");
        Debug.Log("ART_V4_RUNTIME_INITIALIZATION_PASS checks="+checks+" (constructor contract only; no full-crowd/playthrough/timing claim)");
    }    [MenuItem("Early Rush/Art V4/Validate Coffee Patrol (Real Tick)")]
    public static void ValidateCoffeePatrol()
    {
        checks=0;
        var run=new RunV9(2,RushV4InteriorMap.Seed,new ConfigV9{pedestrianStreetCount=0,pedestrianMetroCount=0,pedestrianOfficeCount=0});
        var actors=run.people.FindAll(p=>p.authoredCoffee&&p.kind==PersonKind.Coffee);
        Check(actors.Count==3,"patrol has three authored coffee actors");
        var initial=new Vector2[actors.Count];var previous=new Vector2[actors.Count];
        var distance=new float[actors.Count];var visitedA=new bool[actors.Count];var visitedB=new bool[actors.Count];
        for(int i=0;i<actors.Count;i++)initial[i]=previous[i]=actors[i].position;
        run.Begin();
        const float dt=1f/60;
        // Actual public simulation entry only. Player supplies no movement or task actions.
        for(int frame=0;frame<3600;frame++)
        {
            run.Tick(dt,Vector2.zero);
            for(int i=0;i<actors.Count;i++)
            {
                var actor=actors[i];distance[i]+=Vector2.Distance(previous[i],actor.position);previous[i]=actor.position;
                visitedA[i]|=Vector2.Distance(actor.position,actor.authoredCoffeeA)<.7f;
                visitedB[i]|=Vector2.Distance(actor.position,actor.authoredCoffeeB)<.7f;
                Check(run.map.IsWalkable(actor.position,actor.radius),"patrol physical clearance "+actor.authoredCoffeeId);
            }
        }
        Equal(run.player,run.map.spawn,"idle player stays at spawn during patrol");
        for(int i=0;i<actors.Count;i++)
        {
            var actor=actors[i];Check(distance[i]>20,"coffee actually traveled "+actor.authoredCoffeeId);
            Check(visitedA[i]&&visitedB[i]&&actor.coffeeDeskVisits>=1,"coffee visited both source endpoints "+actor.authoredCoffeeId);
            Check(!actor.spilled&&actor.destinationChanges>=2,"patrol retains cup and alternates destinations "+actor.authoredCoffeeId);
        }
        run.Pause();double pausedAt=run.elapsed;var paused=new Vector2[actors.Count];
        for(int i=0;i<actors.Count;i++)paused[i]=actors[i].position;
        for(int frame=0;frame<300;frame++)run.Tick(dt,Vector2.zero);
        Check(run.phase==RunPhase.Paused&&run.elapsed==pausedAt,"pause freezes simulation clock");
        for(int i=0;i<actors.Count;i++)Equal(actors[i].position,paused[i],"pause freezes coffee "+actors[i].authoredCoffeeId);
        run.Resume();var resumedDistance=new float[actors.Count];
        for(int i=0;i<actors.Count;i++)previous[i]=actors[i].position;
        // Allow an existing desk pause to finish naturally before asserting resumed motion.
        for(int frame=0;frame<600;frame++)
        {
            run.Tick(dt,Vector2.zero);
            for(int i=0;i<actors.Count;i++)
            {
                resumedDistance[i]+=Vector2.Distance(previous[i],actors[i].position);previous[i]=actors[i].position;
                Check(run.map.IsWalkable(actors[i].position,actors[i].radius),"resumed patrol physical clearance");
            }
        }
        Check(run.phase==RunPhase.Playing&&run.elapsed>pausedAt,"resume advances simulation clock");
        for(int i=0;i<actors.Count;i++)Check(resumedDistance[i]>1,"resume restores coffee motion "+actors[i].authoredCoffeeId);
        Check(run.Completed==0,"patrol test never awards tasks");
        Debug.Log("ART_V4_COFFEE_PATROL_PASS checks="+checks+" simulatedSeconds="+run.elapsed+" (real Tick, 3 authored coworkers, ambient0; full-crowd gameplay not certified)");
    }    [MenuItem("Early Rush/Art V4/Validate Source NPC Ownership (Read Only)")]
    public static void ValidateNpcSources()
    {
        checks=0;int[] humans={93,65,81},retained={644,535,601};
        for(int stage=0;stage<3;stage++)
        {
            var layout=RushV4Layout.Load(stage);int blockerCount=layout.blockers.Length;
            var source=RushV4NpcSources.BuildExclusions(layout);
            Check(source.Count==humans[stage]&&source.Sources.Count==humans[stage],"source human cardinality "+layout.id);
            Check(source.RetainedBlockerCount==retained[stage],"nonhuman walls/furniture retained "+layout.id);
            Check(layout.blockers.Length==blockerCount,"source blocker array unmodified");
            var owned=new System.Collections.Generic.HashSet<int>();int shifted=0;float maxShift=0;
            foreach(var npc in source.Sources)
            {
                Check(RushV4NpcSources.IsNpcObject(npc.src),"human whitelist only");
                Check(owned.Add(npc.blockerIndex)&&source.IsExcluded(npc.blockerIndex),"unique blocker ownership "+npc.id);
                Check(source.Find(npc.id)==npc&&source.IsReplacedObject(npc.id),"stable source lookup "+npc.id);
                var item=layout.objects[npc.objectIndex];var expected=RushV4NpcSources.ExpectedBlockerPixels(item);var actual=layout.blockers[npc.blockerIndex];
                Check(Mathf.Abs(expected.x-actual.x)<.011f&&Mathf.Abs(expected.y-actual.y)<.011f&&Mathf.Abs(expected.width-actual.w)<.011f&&Mathf.Abs(expected.height-actual.h)<.011f,"exact generated footprint "+npc.id);
                Check(npc.Shift<=RushV4NpcSources.MaxSpawnShift+.0001f,"bounded local spawn shift "+npc.id);
                Check(source.IsWalkable(npc.position,RushV4NpcSources.SpawnRadius)&&source.IsInOriginalCorridor(npc,npc.position),"same corridor physical clearance "+npc.id);
                if(npc.Shift>.001f)shifted++;maxShift=Mathf.Max(maxShift,npc.Shift);
            }
            foreach(var item in layout.objects)
                if(item.src.StartsWith("sprites/npcs/")&&!RushV4NpcSources.IsNpcObject(item.src))
                    Check(!source.IsReplacedObject(item.id),"vehicles remain source objects "+item.id);
            // A matched human index is the sole reason any blocker may be excluded.
            for(int b=0;b<layout.blockers.Length;b++)Check(source.IsExcluded(b)==owned.Contains(b),"no nearby building deletions");
            var first=layout.objects[source.Sources[0].objectIndex];var block=layout.blockers[source.Sources[0].blockerIndex];
            var malformed=new RushV4Layout{objects=new[]{first},blockers=new[]{block,block},corridors=layout.corridors};
            bool rejected=false;try{RushV4NpcSources.BuildExclusions(malformed);}catch(InvalidOperationException){rejected=true;}
            Check(rejected,"ambiguous blocker match rejected");
            malformed.blockers=new RushV4Layout.Box[0];rejected=false;
            try{RushV4NpcSources.BuildExclusions(malformed);}catch(InvalidOperationException){rejected=true;}
            Check(rejected,"missing blocker match rejected");
            Debug.Log("ART_V4_NPC_SOURCE_STAGE "+layout.id+" humans="+source.Count+" retainedBlockers="+source.RetainedBlockerCount+" shifted="+shifted+" maxShift="+maxShift);
        }
        Check(!RushV4NpcSources.IsNpcObject("sprites/npcs/city-bus.png")&&!RushV4NpcSources.IsNpcObject("sprites/npcs/cream-car.png"),"vehicles excluded from human whitelist");
        Debug.Log("ART_V4_NPC_SOURCE_CONTRACT_PASS checks="+checks+" (read-only matching/geometry; no NPC spawning or navigation execution)");
    }}

using System;
using System.Collections.Generic;
using UnityEngine;
namespace EarlyRush.V9
{
    public enum RunPhase { Ready, Playing, Paused, Results }
    public enum TaskState { NotStarted, Active, Complete, Failed }
    public enum BodyKind { Wheel, Cloth, Chair }
    public enum PersonKind { Ordinary, Alighting, Coffee }
    public enum FixtureKind { Breakfast, Repair, FlipBoard, Capsule, FilePickup, FileDesk, ChairSocket }
    public enum TrainPhase { Waiting, Alighting, Boarding, Riding, Arrived }
    [Serializable] public sealed class ConfigV9
    {
        // V3 design starting values; route timing still needs human playtesting.
        public double stageLimit=420, deliveryDuration=200;
        public double breakfastGameMinutes=3.5, gameMinutesPerRealSecond=.05;
        // Compatibility for existing diagnostic fixtures. Author new settings in game minutes.
        public double breakfastDuration { get=>breakfastGameMinutes/gameMinutesPerRealSecond; set=>breakfastGameMinutes=value*gameMinutesPerRealSecond; }
        public int breakfastTolerance=8,coffeeTolerance=3,chairCount=5,fileCount=3;
        public float walkSpeed=3.6f,bikeSpeed=5.3f,stableDuration=.4f,stableSpeed=.12f;
        public float greenDuration=12,redDuration=10,beatPeriod=1.5f,beatWindow=.35f;
        public int capsuleGoal=5,capsulePenalty=1;
    }
    public sealed class TaskV9
    {
        public string id,title,icon;public TaskState state;public int count,limit;public float progress;
        public double startedAt;public Vector2 position;public bool done=>state==TaskState.Complete;
    }
    public sealed class BodyV9
    {
        public int id;public BodyKind kind;public Vector2 position,home,velocity;public float radius=.55f,pushed,stable;
        public bool touched,done;public int socket=-1;public string art;
    }
    public sealed class PersonV9
    {
        public int id;public PersonKind kind;public Vector2 position,home,target,velocity;public float radius=.45f;
        public bool spilled,exited;public string art="people/man";
    }
    public sealed class FixtureV9
    {
        public int id,index;public FixtureKind kind;public Vector2 position;public float radius=.65f;public string art;
        public bool occupied;public int occupant=-1;
    }
    public sealed class TelemetryV9
    {
        public double time;public string type;public int id;public Vector2 position;
    }
    public sealed class DirtV9 { public Vector2 position;public bool cleaned; }
}

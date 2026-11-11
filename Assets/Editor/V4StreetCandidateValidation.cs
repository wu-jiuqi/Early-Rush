using System;
using System.Globalization;
using System.IO;
using EarlyRush.V9;
using UnityEditor;
using UnityEngine;

// Experimental profile only: never selected by the shipped game or default ConfigV9.
public static class V4StreetCandidateValidation
{
    const string Folder = "Builds/EarlyRush-V14/QA/v4-street";
    const float Speed = 3.8f;
    const double Deadline = 600;

    [MenuItem("Early Rush/Art V4/Candidate 3.8/Mainline Only")]
    public static void Mainline() { Run(false); }

    [MenuItem("Early Rush/Art V4/Candidate 3.8/All Tasks Only")]
    public static void AllTasks() { Run(true); }

    static void Run(bool allTasks)
    {
        Directory.CreateDirectory(Folder);
        var map = RushMapV9.Create(0, RushV4StreetMap.Seed);
        if (!map.Validate(out string geometryReason))
            throw new Exception("Candidate geometry failed: " + geometryReason);
        var config = new ConfigV9 { stageLimit = Deadline, walkSpeed = Speed };
        var result = allTasks
            ? RushTestsV9.ReplayAllTasks(0, RushV4StreetMap.Seed, config)
            : RushTestsV9.ReplayMainline(0, RushV4StreetMap.Seed, config);
        bool accepted = result.success && result.elapsed <= Deadline
            && (allTasks ? result.completed == 3 : result.elapsed >= 420);
        string text = "EXPERIMENT ONLY; seed=91027; walkSpeed=3.8; deadline=600.\n"
            + "Only walking-speed profile differs; default source config, collisions and task rules unchanged.\n"
            + "Actual Tick automated replay, NOT human playtime or release acceptance.\n"
            + "mode=" + (allTasks ? "allTasks" : "mainline")
            + " success=" + result.success
            + " seconds=" + result.elapsed.ToString("R", CultureInfo.InvariantCulture)
            + " completed=" + result.completed
            + " states=" + string.Join("|", result.taskStates ?? new string[0])
            + " stop=" + result.stoppedAt + " reason=" + result.reason
            + "\ncandidateCriterionPassed=" + accepted
            + "; mainline requires 420..600s; allTasks requires success and 3 tasks within 600s.\n";
        File.WriteAllText(Folder + "/candidate-3p8-" + (allTasks ? "alltasks" : "mainline") + ".txt", text);
        Debug.Log(text);
        if (!accepted) throw new Exception("Candidate pacing criterion failed; see individual report.");
    }
}

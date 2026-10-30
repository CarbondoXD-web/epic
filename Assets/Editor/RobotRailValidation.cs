using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class RobotRailValidation
{
    const string Key="EPIC.RailValidation";
    static RobotMachiningCycle cycle;
    static RobotRailController rail;
    static double started,pauseTime;
    static bool paused,pauseTested;
    static Vector3 pausedGrip,pausedPart,pausedCarriage;
    static int previousTrips;
    static List<string> failures=new List<string>();
    static HashSet<string> contacts=new HashSet<string>();
    static float minPosition=0,maxPosition=0;
    static RobotRailValidation(){EditorApplication.playModeStateChanged+=State;EditorApplication.update+=Tick;Application.logMessageReceived+=Log;}
    public static void RunBatch(){SessionState.SetBool(Key+".Batch",true);Begin();}
    [MenuItem("Tools/EPIC/Validate Robot Rail And Full Cycle")]
    public static void Run(){SessionState.SetBool(Key+".Batch",false);Begin();}
    static void Begin(){EditorApplication.delayCall+=()=>{EditorSceneManager.OpenScene("Assets/Scenes/RobotMachineGrab.unity");SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;};}
    static void State(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode)
        {
            cycle=UnityEngine.Object.FindFirstObjectByType<RobotMachiningCycle>();rail=cycle.rail;
            failures.Clear();contacts.Clear();started=EditorApplication.timeSinceStartup;
            cycle.GetComponent<FactoryConveyorDemo>().autoRun=false;
            if(!rail || !cycle.robot.transform.IsChildOf(rail.carriage)){Finish("Robot is not mounted to the carriage.");return;}
            if(!File.Exists("ValidationReports/rail-static-contacts.txt") || File.ReadAllText("ValidationReports/rail-static-contacts.txt").Trim().Length>0){Finish("Static rail layout must pass clearance inspection first.");return;}
            cycle.StartCycle();cycle.StartCycle();
        }
        if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool(Key,false);if(SessionState.GetBool(Key+".Batch",false))EditorApplication.Exit(failures.Count==0?0:1);}
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||!cycle||!rail)return;
        double now=EditorApplication.timeSinceStartup;
        if(now-started>240){Finish("Timed out before two full rail cycles completed.");return;}
        minPosition=Mathf.Min(minPosition,rail.Position);maxPosition=Mathf.Max(maxPosition,rail.Position);
        if(rail.IsMoving && cycle.robot.IsHolding)
        {
            var part=cycle.raw.transform.parent==cycle.robot.grip?cycle.raw.transform:cycle.finished;
            if(part.parent!=cycle.robot.grip){Finish("Rail moved while holding flag was set but workpiece was detached.");return;}
            if(!pauseTested && !paused && Mathf.Abs(rail.Position)>.15f)
            {
                cycle.robot.ToggleDamage();paused=true;pauseTime=now;pausedGrip=cycle.robot.grip.position;pausedPart=part.position;pausedCarriage=rail.carriage.position;
            }
            if(paused)
            {
                if(Vector3.Distance(pausedGrip,cycle.robot.grip.position)>.0001f || Vector3.Distance(pausedPart,part.position)>.0001f || Vector3.Distance(pausedCarriage,rail.carriage.position)>.0001f){Finish("Damage did not pause rail, gripper and held workpiece together.");return;}
                if(now-pauseTime>.3){cycle.robot.ToggleDamage();paused=false;pauseTested=true;}
            }
        }
        if(!cycle.robot.IsBusy)
        {
            if(cycle.CompletedCount==0){Finish("Cycle aborted: "+cycle.Message);return;}
            if(Mathf.Abs(rail.Position)>.001f){Finish("Carriage did not return to machine standby station.");return;}
            if(rail.LoadedTrips<previousTrips+2){Finish("Cycle must include both raw and finished loaded trips.");return;}
            previousTrips=rail.LoadedTrips;
            if(cycle.CompletedCount<2)cycle.StartCycle();else Finish(null);
        }
    }
    static void Log(string message,string stack,LogType type)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(message.Contains("FULL CONTACT:") || message.Contains("RETURN CLOSE CONTACT:"))contacts.Add(message);
        if(type==LogType.Exception || type==LogType.Error)failures.Add(message);
    }
    [Serializable]class Report{public bool passed,damagePauseTested;public int completedCycles,loadedTrips;public float minimumPosition,maximumPosition;public string[] failures,contacts;}
    static void Finish(string error)
    {
        if(error!=null)failures.Add(error);
        if(contacts.Count>0)failures.Add("Equipment collision diagnostics reported intersections.");
        if(cycle.CompletedCount>=2 && !pauseTested)failures.Add("Loaded rail damage pause was not tested.");
        Directory.CreateDirectory("ValidationReports");
        File.WriteAllText("ValidationReports/robot-rail-validation.json",JsonUtility.ToJson(new Report{passed=failures.Count==0,damagePauseTested=pauseTested,completedCycles=cycle.CompletedCount,loadedTrips=rail.LoadedTrips,minimumPosition=minPosition,maximumPosition=maxPosition,failures=failures.ToArray(),contacts=new List<string>(contacts).ToArray()},true));
        FactoryConveyorLayout.Capture(cycle.robot.viewCamera,"ValidationReports/robot-rail-result.png");
        EditorApplication.isPlaying=false;
    }
}

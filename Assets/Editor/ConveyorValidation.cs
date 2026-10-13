using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Runs the actual scene. SessionState keeps the request alive across Play Mode reloads.
[InitializeOnLoad]
public static class ConveyorValidation
{
    const string ActiveKey="EPIC.Validation.Active", BatchKey="EPIC.Validation.Batch";
    const string PassedKey="EPIC.Validation.Passed", ErrorKey="EPIC.Validation.Error";
    const string StartedKey="EPIC.Validation.Started";
    const string SelfCheckKey="EPIC.Validation.SelfCheck";
    const string QueuedKey="EPIC.Validation.Queued", StartupKey="EPIC.Validation.Startup";
    const string ScenePath="Assets/Scenes/RobotMachineGrab.unity";
    enum Phase { WaitFeed, WaitSettle, Paused, WaitComplete }
    [Serializable] sealed class Report
    {
        public string unityVersion, startedUtc, finishedUtc;
        public string editorStartupLog;
        public bool passed;
        public int completedCycles;
        public int completedCollisionChecks;
        public bool collisionSelfCheckPassed;
        public List<string> checks=new List<string>();
        public List<string> failures=new List<string>();
        public List<string> contacts=new List<string>();
    }
    static Report report;
    static RobotMachiningCycle cycle;
    static ConveyorFeedController conveyor;
    static Phase phase;
    static int round, initialCount;
    static double roundStarted, pauseStarted;
    static Transform[] pausedTransforms;
    static Vector3[] pausedPositions;
    static Quaternion[] pausedRotations;
    static Vector3 previousRaw, previousTread;

    static ConveyorValidation()
    {
        EditorApplication.playModeStateChanged+=OnPlayMode;
        EditorApplication.update+=Tick;
        Application.logMessageReceived+=OnLog;
    }
    [MenuItem("Tools/EPIC/Validate Conveyor And Full Cycle")]
    public static void Run() { Begin(false); }
    // Unity.exe -batchmode -projectPath <project> -executeMethod ConveyorValidation.RunBatch
    // Omit -quit: the validator exits with code 0/1 after Play Mode finishes.
    public static void RunBatch()
    {
        SessionState.SetBool(QueuedKey,true);
        SessionState.SetString(StartupKey,"");
        SessionState.SetString(StartedKey,DateTime.UtcNow.ToString("o"));
        // Let queued editor startup callbacks complete before measuring the scene.
        EditorApplication.delayCall+=()=>EditorApplication.delayCall+=()=>Begin(true);
    }
    static void Begin(bool batch)
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool(ActiveKey,false))
            throw new InvalidOperationException("Stop Play Mode before starting EPIC validation.");
        if(!batch && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        SessionState.SetBool(QueuedKey,false);
        if(!batch)SessionState.SetString(StartupKey,"");
        SessionState.SetBool(BatchKey,batch);
        SessionState.SetBool(PassedKey,false);
        SessionState.SetString(ErrorKey,"");
        SessionState.SetBool(SelfCheckKey,false);
        SessionState.SetString(StartedKey,DateTime.UtcNow.ToString("o"));
        SessionState.SetBool(ActiveKey,true);
        try
        {
            EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            EditorApplication.isPlaying=true;
        }
        catch(Exception e) { Fail(e.Message); }
    }
    static void OnPlayMode(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(ActiveKey,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode)
        {
            report=new Report {unityVersion=Application.unityVersion,startedUtc=SessionState.GetString(StartedKey,""),editorStartupLog=SessionState.GetString(StartupKey,"")};
            try
            {
                cycle=UnityEngine.Object.FindFirstObjectByType<RobotMachiningCycle>();
                Require(cycle && cycle.robot && cycle.raw && cycle.finished && cycle.machine,"Scene workflow references are missing.");
                conveyor=cycle.conveyor;
                Require(conveyor && conveyor.beltSurface && conveyor.raw==cycle.raw && conveyor.robot==cycle.robot,"Conveyor references do not match the workflow.");
                Require(Time.timeScale==1f,"Validation requires Time.timeScale = 1.");
                report.collisionSelfCheckPassed=SessionState.GetBool(SelfCheckKey,false);
                Require(report.collisionSelfCheckPassed,"Collision query self-check did not pass; collision validation is unavailable.");
                ValidateRails();
                Require(!cycle.robot.IsDamaged,"Robot must begin repaired.");
                initialCount=cycle.CompletedCount;
                round=0;
                StartRound();
            }
            catch(Exception e) { Fail(e.Message); }
        }
        if(state==PlayModeStateChange.ExitingPlayMode && report!=null && report.finishedUtc==null)
            Finish("Validation interrupted before completion.");
        if(state==PlayModeStateChange.EnteredEditMode)
        {
            bool batch=SessionState.GetBool(BatchKey,false),passed=SessionState.GetBool(PassedKey,false);
            SessionState.SetBool(ActiveKey,false);
            report=null;
            if(batch)EditorApplication.Exit(passed?0:1);
        }
    }
    static void ValidateRails()
    {
        Vector3 forward=conveyor.pickupPosition-conveyor.feedStart;forward.y=0;forward.Normalize();
        for(int i=0;i<2;i++)
        {
            string name=i==0?"Feed guide rail left":"Feed guide rail right";
            var rails=conveyor.GetComponentsInChildren<Transform>(true).Where(t=>t.name==name).ToArray();
            Require(rails.Length==1,"Expected exactly one "+name);
            var rail=rails[0];var box=rail.GetComponent<BoxCollider>();
            Require(box && box.enabled && !box.isTrigger,"Guide rail must have a solid BoxCollider: "+name);
            float end=Vector3.Dot(rail.position-conveyor.feedStart,forward)+rail.TransformVector(Vector3.forward).magnitude*.5f;
            float clearance=Vector3.Dot(conveyor.pickupPosition-conveyor.feedStart,forward)-end;
            Require(clearance>=.199f,"Guide rail leaves less than 200 mm pickup clearance: "+name);
        }
        report.checks.Add("Both solid guide rails exist once and preserve 200 mm pickup clearance.");
        RobotGrabController.ProjectionRange(cycle.raw.transform,Vector3.zero,Vector3.up,out float bottom,out _);
        float beltTop=conveyor.beltSurface.position.y+conveyor.beltSurface.TransformVector(Vector3.up).magnitude*.5f;
        Require(Mathf.Abs(bottom-beltTop)<.003f,"Blank is floating above or penetrating the belt at the feed entrance.");
        Require(Mathf.Abs(conveyor.feedStart.y-conveyor.pickupPosition.y)<.001f,"Feed route must remain on the horizontal belt surface.");
        report.checks.Add("Blank sits on the belt; the delivery route is level.");
    }
    static void StartRound()
    {
        Require(Vector3.Distance(cycle.raw.position,conveyor.feedStart)<.001f && !conveyor.IsReady,"Raw blank was not restaged at the feed entrance.");
        roundStarted=EditorApplication.timeSinceStartup;
        phase=round==0?Phase.WaitComplete:round==1?Phase.WaitFeed:Phase.WaitSettle;
        previousRaw=cycle.raw.position;
        Require(conveyor.treadMarkers!=null && conveyor.treadMarkers.Length>0 && conveyor.treadMarkers[0],"Belt tread reference is missing.");
        previousTread=conveyor.treadMarkers[0].position;
        cycle.StartCycle();
        cycle.StartCycle(); // Busy gating must prevent a second concurrent cycle.
    }
    static void Tick()
    {
        if(!SessionState.GetBool(ActiveKey,false))
        {
            if(SessionState.GetBool(QueuedKey,false) && DateTime.TryParse(SessionState.GetString(StartedKey,""),out var queued) && (DateTime.UtcNow-queued.ToUniversalTime()).TotalSeconds>180)
                Fail("Timed out waiting for editor startup callbacks.");
            return;
        }
        if(report==null)
        {
            if(DateTime.TryParse(SessionState.GetString(StartedKey,""),out var started) && (DateTime.UtcNow-started.ToUniversalTime()).TotalSeconds>180)
                Fail("Timed out entering Play Mode. Check the Unity compiler and editor log.");
            return;
        }
        if(report.finishedUtc!=null || !EditorApplication.isPlaying)return;
        try
        {
            string error=SessionState.GetString(ErrorKey,"");
            Require(error.Length==0,error);
            Require(EditorApplication.timeSinceStartup-roundStarted<180,"Cycle timed out.");
            if(conveyor.IsMoving)
            {
                Vector3 direction=(conveyor.pickupPosition-conveyor.feedStart).normalized;
                float length=conveyor.beltSurface.TransformVector(Vector3.forward).magnitude;
                Require(length>.001f,"Belt surface has no usable length.");
                float rawDistance=Vector3.Dot(cycle.raw.position-previousRaw,direction);
                float treadDistance=Vector3.Dot(conveyor.treadMarkers[0].position-previousTread,direction);
                float errorDistance=Mathf.Repeat(treadDistance-rawDistance+length*.5f,length)-length*.5f;
                Require(Mathf.Abs(errorDistance)<.001f,"Belt tread and blank movement are out of sync.");
            }
            previousRaw=cycle.raw.position;previousTread=conveyor.treadMarkers[0].position;
            if(phase==Phase.WaitFeed && conveyor.IsMoving && Vector3.Distance(cycle.raw.position,conveyor.feedStart)>.05f)Pause();
            else if(phase==Phase.WaitSettle && !conveyor.IsMoving && !conveyor.IsReady)Pause();
            else if(phase==Phase.Paused)
            {
                for(int i=0;i<pausedTransforms.Length;i++)
                {
                    Require(Vector3.Distance(pausedTransforms[i].localPosition,pausedPositions[i])<.0001f,"Transform moved while damaged: "+pausedTransforms[i].name);
                    Require(Quaternion.Angle(pausedTransforms[i].localRotation,pausedRotations[i])<.05f,"Transform rotated while damaged: "+pausedTransforms[i].name);
                }
                Require(!conveyor.IsReady,"Conveyor became ready while damaged.");
                if(EditorApplication.timeSinceStartup-pauseStarted>=.5)
                {
                    cycle.robot.ToggleDamage();
                    report.checks.Add(round==1?"Damage pauses delivery and belt; repair resumes.":"Damage pauses settling; pickup remains blocked until repair.");
                    phase=Phase.WaitComplete;
                }
            }
            if(!cycle.robot.IsBusy)
            {
                Require(phase==Phase.WaitComplete,"Pause test phase was missed.");
                Require(cycle.CompletedCount==initialCount+round+1,"Cycle recovered or ran more than once instead of completing.");
                Require(!cycle.machine.IsBusy && !cycle.robot.IsHolding && cycle.finished.gameObject.activeInHierarchy,"Cycle did not release the finished part or clear its busy state.");
                report.completedCycles=round+1;
                report.checks.Add("Cycle "+(round+1)+" completed; duplicate start ignored.");
                if(++round<3)StartRound();else Finish(null);
            }
        }
        catch(Exception e) { Fail(e.Message); }
    }
    static void Pause()
    {
        cycle.robot.ToggleDamage();
        var transforms=new List<Transform>(cycle.robot.GetComponentsInChildren<Transform>(true));
        transforms.Add(cycle.raw.transform);transforms.Add(cycle.machine.door);transforms.Add(cycle.machine.carriage);
        if(conveyor.treadMarkers!=null)transforms.AddRange(conveyor.treadMarkers.Where(t=>t));
        pausedTransforms=transforms.Where(t=>t).Distinct().ToArray();
        pausedPositions=pausedTransforms.Select(t=>t.localPosition).ToArray();
        pausedRotations=pausedTransforms.Select(t=>t.localRotation).ToArray();
        pauseStarted=EditorApplication.timeSinceStartup;
        phase=Phase.Paused;
    }
    static void OnLog(string condition,string stackTrace,LogType type)
    {
        if(SessionState.GetBool(QueuedKey,false) && (type==LogType.Error || type==LogType.Exception || type==LogType.Assert))
            SessionState.SetString(StartupKey,SessionState.GetString(StartupKey,"")+condition+"\n"+stackTrace+"\n");
        if(!SessionState.GetBool(ActiveKey,false) || (report!=null && report.finishedUtc!=null))return;
        if(condition.Contains("CLEARANCE QUERY SELF CHECK: True"))SessionState.SetBool(SelfCheckKey,true);
        if(report!=null && condition.StartsWith("[Machining] FULL CYCLE CHECK:"))report.completedCollisionChecks++;
        if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert ||
            condition.Contains("CLEARANCE QUERY SELF CHECK: False") || condition.Contains("CLEARANCE QUERY OVERFLOW"))
            SessionState.SetString(ErrorKey,condition+"\n"+stackTrace);
        if(report!=null && condition.StartsWith("[Machining] FULL CONTACT:"))report.contacts.Add(condition);
    }
    static void Require(bool condition,string message) { if(!condition)throw new InvalidOperationException(message); }
    static void Fail(string message) { Finish(message); }
    static void Finish(string failure)
    {
        if(report==null)report=new Report {unityVersion=Application.unityVersion,startedUtc=SessionState.GetString(StartedKey,"")};
        if(report.finishedUtc!=null)return;
        SessionState.SetBool(QueuedKey,false);
        if(!string.IsNullOrEmpty(failure))report.failures.Add(failure);
        string runtimeError=SessionState.GetString(ErrorKey,"");
        if(runtimeError.Length>0 && !report.failures.Contains(runtimeError))report.failures.Add(runtimeError);
        if(report.contacts.Count>0)report.failures.Add("Robot/workpiece collision contacts detected; inspect contacts in this report.");
        if(report.completedCollisionChecks!=3)report.failures.Add("Expected three completed full-cycle collision checks.");
        report.passed=report.completedCycles==3 && report.failures.Count==0;
        report.finishedUtc=DateTime.UtcNow.ToString("o");
        SessionState.SetBool(PassedKey,report.passed);
        string directory=Path.GetFullPath("ValidationReports");Directory.CreateDirectory(directory);
        string path=Path.Combine(directory,"conveyor-validation.json");
        File.WriteAllText(path,JsonUtility.ToJson(report,true));
        Debug.Log("[EPIC Validation] "+(report.passed?"PASS":"FAIL")+". Report: "+path);
        if(EditorApplication.isPlaying)EditorApplication.isPlaying=false;
        else if(!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            SessionState.SetBool(ActiveKey,false);
            if(SessionState.GetBool(BatchKey,false))EditorApplication.Exit(1);
        }
    }
}

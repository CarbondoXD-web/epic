using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class FactoryConveyorPreview
{
    const string Key="EPIC.FactoryPreview.Requested";
    const string DoorKey="EPIC.FactoryPreview.Door";
    static RobotMachiningCycle cycle;
    static double nextFrame;
    static int frame;
    static bool recording;
    static string frameDirectory;
    static FactoryConveyorPreview()
    {
        EditorApplication.playModeStateChanged+=StateChanged;
        EditorApplication.update+=Tick;
    }
    [MenuItem("Tools/EPIC/Show Central Aisle Dynamic Preview")]
    public static void Launch()
    {
        Begin(false);
    }
    [MenuItem("Tools/EPIC/Show Chamber Door Dynamic Preview")]
    public static void LaunchDoor()
    {
        Begin(true);
    }
    static void Begin(bool door)
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        SessionState.SetBool(Key,true);
        SessionState.SetBool(DoorKey,door);
        EditorApplication.delayCall+=()=>EditorApplication.delayCall+=()=>
        {
            EditorSceneManager.OpenScene("Assets/Scenes/RobotMachineGrab.unity");
            EditorApplication.isPlaying=true;
        };
    }
    static void StateChanged(PlayModeStateChange state)
    {
        if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key,false))
        {
            SessionState.SetBool(Key,false);
            cycle=UnityEngine.Object.FindFirstObjectByType<RobotMachiningCycle>();
            cycle.GetComponent<FactoryConveyorDemo>().autoRun=true;
            bool door=SessionState.GetBool(DoorKey,false);
            if(door)
                cycle.robot.viewCamera.GetComponent<RobotOrbitCamera>().Frame(new Vector3(.40f,1.5f,-.10f),new Vector3(-3.25f,3.05f,2.75f));
            var gameView=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if(gameView!=null){var window=EditorWindow.GetWindow(gameView);window.Show();window.Focus();}
            frameDirectory="ValidationReports/"+(door?"door-preview-frames-":"preview-frames-")+DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            Directory.CreateDirectory(frameDirectory);
            frame=0;recording=true;nextFrame=EditorApplication.timeSinceStartup+2;
            File.WriteAllText("ValidationReports/preview-status.json","{\"playing\":true,\"scene\":\"RobotMachineGrab\",\"autoRun\":true}");
            Debug.Log("[Factory preview] Play Mode active; continuous machining cycle enabled. 1 = perspective, 2 = top view.");
        }
        if(state==PlayModeStateChange.ExitingPlayMode){recording=false;cycle=null;}
    }
    static void Tick()
    {
        if(!recording || !cycle || !EditorApplication.isPlaying || EditorApplication.timeSinceStartup<nextFrame)return;
        nextFrame=EditorApplication.timeSinceStartup+.20;
        var camera=cycle.robot.viewCamera;
        FactoryConveyorLayout.Capture(camera,frameDirectory+"/"+frame.ToString("D4")+".png",800,560);
        if(frame++==0)
        {
            FactoryConveyorLayout.Capture(camera,SessionState.GetBool(DoorKey,false)?"ValidationReports/door-perspective.png":"ValidationReports/aisle-perspective.png");
            var position=camera.transform.position;var rotation=camera.transform.rotation;
            bool orthographic=camera.orthographic;float size=camera.orthographicSize;
            try
            {
                camera.transform.SetPositionAndRotation(new Vector3(-.30f,8,-.70f),Quaternion.Euler(90,0,0));
                camera.orthographic=true;camera.orthographicSize=2.5f;
                FactoryConveyorLayout.Capture(camera,"ValidationReports/aisle-top.png");
            }
            finally {camera.transform.SetPositionAndRotation(position,rotation);camera.orthographic=orthographic;camera.orthographicSize=size;}
        }
        if(cycle.CompletedCount>=1 || frame>=450)
        {
            recording=false;
            File.WriteAllText("ValidationReports/preview-status.json","{\"playing\":true,\"autoRun\":true,\"completedCycles\":"+cycle.CompletedCount+",\"capturedFrames\":"+frame+",\"frameDirectory\":\""+frameDirectory+"\"}");
            Debug.Log("[Factory preview] Captured "+frame+" frames; preview remains running for inspection.");
        }
    }
}

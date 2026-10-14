using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class FactoryConveyorPreview
{
    const string Key="EPIC.FactoryPreview.Requested";
    static RobotMachiningCycle cycle;
    static double nextFrame;
    static int frame;
    static bool recording;
    static FactoryConveyorPreview()
    {
        EditorApplication.playModeStateChanged+=StateChanged;
        EditorApplication.update+=Tick;
    }
    [MenuItem("Tools/EPIC/Show Central Aisle Dynamic Preview")]
    public static void Launch()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        SessionState.SetBool(Key,true);
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
            var gameView=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if(gameView!=null){var window=EditorWindow.GetWindow(gameView);window.Show();window.Focus();}
            Directory.CreateDirectory("ValidationReports/preview-frames");
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
        FactoryConveyorLayout.Capture(camera,"ValidationReports/preview-frames/"+frame.ToString("D4")+".png",800,560);
        if(frame++==0)
        {
            FactoryConveyorLayout.Capture(camera,"ValidationReports/aisle-perspective.png");
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
            File.WriteAllText("ValidationReports/preview-status.json","{\"playing\":true,\"autoRun\":true,\"completedCycles\":"+cycle.CompletedCount+",\"capturedFrames\":"+frame+"}");
            Debug.Log("[Factory preview] Captured "+frame+" frames; preview remains running for inspection.");
        }
    }
}

using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FactoryConveyorLayout
{
    public static void ApplyBatch()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/RobotMachineGrab.unity");
        Apply();
    }
    [MenuItem("Tools/EPIC/Build Central Aisle Feed Line")]
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        var cycle=Object.FindFirstObjectByType<RobotMachiningCycle>();
        if(!cycle || !cycle.conveyor)throw new System.InvalidOperationException("Open RobotMachineGrab first.");
        var conveyor=cycle.conveyor;
        Undo.RegisterFullObjectHierarchyUndo(conveyor.gameObject,"Build central aisle feed line");
        Undo.RecordObject(cycle.raw.transform,"Move blank to new pickup station");
        // The machine faces 25 degrees west of the world X axis. The aisle
        // runs parallel to its frontage, rather than pointing into the bay.
        Vector3 forward=Quaternion.Euler(0,25,0)*Vector3.forward;
        Vector3 outward=Vector3.Cross(forward,Vector3.up);
        Vector3 originalPickup=new Vector3(.061818335f,1.3223505f,-.025834167f);
        conveyor.pickupPosition=originalPickup+outward*.20f-forward*.25f;
        conveyor.feedStart=conveyor.pickupPosition-forward*2.65f;
        conveyor.feedSeconds=5f;
        cycle.raw.transform.position=conveyor.pickupPosition;
        var oldModel=conveyor.transform.Find("Imported Conveyor Module");
        if(oldModel)oldModel.gameObject.SetActive(false); // Keep original CAD data recoverable.
        foreach(Transform t in conveyor.transform)
            if(t.name=="Adjustable support leg")t.gameObject.SetActive(false);
        const float length=2.95f,width=.30f;
        RobotGrabController.ProjectionRange(cycle.raw.transform,Vector3.zero,Vector3.up,out float top,out _);
        Vector3 center=conveyor.pickupPosition-forward*1.375f;center.y=top-.012f;
        var surface=conveyor.beltSurface;
        surface.SetPositionAndRotation(center,Quaternion.LookRotation(forward));
        surface.localScale=new Vector3(width,.024f,length);
        var metal=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Conveyor_Legs.mat");
        // The final 550 mm uses a shallow frame above the base swing envelope.
        Piece(conveyor,"Central line bed",center-forward*.275f-Vector3.up*.065f,new Vector3(width+.06f,.10f,length-.55f),forward,metal);
        Piece(conveyor,"Pickup nose bed",center+forward*(length*.5f-.275f)-Vector3.up*.045f,new Vector3(width+.06f,.06f,.55f),forward,metal);
        for(int i=0;i<5;i++)
        {
            // Leave the final 550 mm cantilevered beyond the last frame so
            // a support cannot occupy the fixed robot's pedestal/swing zone.
            Vector3 station=center+forward*(-length*.5f+.16f+i*(length-.32f-.55f)/4);
            // Bed underside is top - .127 m. Crossbeam top meets it;
            // uprights finish exactly at the crossbeam underside.
            Piece(conveyor,"Central line crossbeam "+i,new Vector3(station.x,top-.152f,station.z),new Vector3(.38f,.05f,.05f),forward,metal);
            foreach(int side in new[]{-1,1})
            {
                Vector3 foot=station+Vector3.Cross(Vector3.up,forward)*(.14f*side);
                float height=top-.177f;
                Piece(conveyor,"Central line leg "+i+" "+side,new Vector3(foot.x,height*.5f,foot.z),new Vector3(.04f,height,.04f),forward,metal);
            }
        }
        // Existing rail objects are updated, preserving the 300 mm open
        // gripping zone. No second rail set or invisible obstacle is created.
        ConveyorSetup.InstallGuideRails();
        for(int i=0;i<conveyor.treadMarkers.Length;i++)
        {
            var tread=conveyor.treadMarkers[i];
            tread.SetPositionAndRotation(center+forward*(i*length/conveyor.treadMarkers.Length-length*.5f)+Vector3.up*.014f,Quaternion.LookRotation(forward));
        }
        var camera=cycle.robot.viewCamera;
        Undo.RecordObject(camera.transform,"Frame central aisle");
        Vector3 focus=new Vector3(-.30f,1.1f,-.70f);
        camera.transform.position=new Vector3(-4.7f,4.9f,-5.7f);
        camera.transform.LookAt(focus);
        Undo.RecordObject(camera.GetComponent<RobotOrbitCamera>(),"Frame central aisle");
        camera.GetComponent<RobotOrbitCamera>().focus=focus;
        var demo=cycle.GetComponent<FactoryConveyorDemo>();
        if(!demo)demo=Undo.AddComponent<FactoryConveyorDemo>(cycle.gameObject);
        demo.cycle=cycle;demo.autoRun=false;
        EditorUtility.SetDirty(conveyor);EditorUtility.SetDirty(demo);
        EditorSceneManager.MarkSceneDirty(cycle.gameObject.scene);
        EditorSceneManager.SaveScene(cycle.gameObject.scene);
        Debug.Log("[Factory layout] Central feed line: length=2.95m, route=2.65m, pickup="+conveyor.pickupPosition+"; robot base unchanged.");
    }
    static void Piece(ConveyorFeedController conveyor,string name,Vector3 position,Vector3 size,Vector3 forward,Material material)
    {
        var child=conveyor.transform.Find(name);
        var item=child?child.gameObject:GameObject.CreatePrimitive(PrimitiveType.Cube);
        if(!child){item.name=name;item.transform.SetParent(conveyor.transform,true);Undo.RegisterCreatedObjectUndo(item,"Create central conveyor frame");}
        item.transform.SetPositionAndRotation(position,Quaternion.LookRotation(forward));
        item.transform.localScale=size;
        item.GetComponent<Renderer>().sharedMaterial=material;
    }
    public static void InspectBatch()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/RobotMachineGrab.unity");
        Directory.CreateDirectory("ValidationReports");
        File.WriteAllLines("ValidationReports/layout-inspection.txt",
            Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
                .Select(r=>r.transform.root.name+" / "+r.name+" center="+r.bounds.center.ToString("F4")+" size="+r.bounds.size.ToString("F4")));
        var cycle=Object.FindFirstObjectByType<RobotMachiningCycle>();
        var camera=cycle.robot.viewCamera;
        File.AppendAllText("ValidationReports/layout-inspection.txt","\nCamera="+camera.transform.position+" rotation="+camera.transform.eulerAngles+"\nMachine approach="+cycle.robot.machineApproach.position+"\n");
        Capture(camera,"ValidationReports/layout-before.png");
        camera.transform.position=new Vector3(0,8,0);
        camera.transform.rotation=Quaternion.Euler(90,0,0);
        camera.orthographic=true;camera.orthographicSize=3.5f;
        Capture(camera,"ValidationReports/layout-top.png");
    }
    public static void Capture(Camera camera,string path,int width=1280,int height=900)
    {
        var previous=camera.targetTexture;
        var previousActive=RenderTexture.active;
        var target=new RenderTexture(width,height,24);
        var pixels=new Texture2D(width,height,TextureFormat.RGB24,false);
        try
        {
            camera.targetTexture=target;camera.Render();RenderTexture.active=target;
            pixels.ReadPixels(new Rect(0,0,width,height),0,0);pixels.Apply();
            File.WriteAllBytes(path,pixels.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture=previous;RenderTexture.active=previousActive;
            Object.DestroyImmediate(pixels);Object.DestroyImmediate(target);
        }
    }
}

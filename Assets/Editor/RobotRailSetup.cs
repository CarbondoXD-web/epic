using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RobotRailSetup
{
    public static void FinishSupportLayoutBatch()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/RobotMachineGrab.unity");
        var cycle=Object.FindFirstObjectByType<RobotMachiningCycle>();
        var old=GameObject.Find("Machine foundation");if(old)Object.DestroyImmediate(old);
        foreach(var footing in cycle.gameObject.scene.GetRootGameObjects().Where(g=>g.name.StartsWith("Machine foundation footing ")).ToArray())Object.DestroyImmediate(footing);
        var metal=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Conveyor_Legs.mat");
        int i=0;
        foreach(var pad in cycle.machine.GetComponentsInChildren<Renderer>().Where(r=>r.name.StartsWith("Pads_DF")))
        {
            var b=pad.bounds;var footing=GameObject.CreatePrimitive(PrimitiveType.Cube);footing.name="Machine foundation footing "+i++;
            footing.transform.position=new Vector3(b.center.x,b.min.y*.5f,b.center.z);
            footing.transform.localScale=new Vector3(b.size.x+.04f,b.min.y,b.size.z+.04f);footing.GetComponent<Renderer>().sharedMaterial=metal;
        }
        Vector3 inward=Vector3.Cross(Vector3.up,cycle.rail.direction).normalized;
        foreach(var leg in GameObject.Find("Finished Parts Table").GetComponentsInChildren<Transform>().Where(t=>t.name=="Table Leg"))
        {
            float offset=Vector3.Dot(leg.position-cycle.robot.transform.position,inward);
            if(offset<.62f)leg.position+=inward*(.62f-offset);
        }
        cycle.raw.transform.position=cycle.conveyor.feedStart;
        EditorSceneManager.MarkSceneDirty(cycle.gameObject.scene);EditorSceneManager.SaveScene(cycle.gameObject.scene);
        InspectInstalledBatch();
    }
    [MenuItem("Tools/EPIC/Fix Rail Mount Orientation")]
    public static void FixMount()
    {
        if(Application.isBatchMode)EditorSceneManager.OpenScene("Assets/Scenes/RobotMachineGrab.unity");
        var cycle=Object.FindFirstObjectByType<RobotMachiningCycle>();var rail=cycle.rail;
        var robot=rail.robot.transform;robot.SetParent(null,true);
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/track.fbx");
        rail.transform.rotation=Quaternion.Euler(0,25,0)*source.transform.rotation;
        var mount=rail.GetComponentsInChildren<Renderer>().Single(r=>r.name=="DEFAULT.igs");
        Vector3 point=mount.bounds.center;point.y=mount.bounds.max.y;
        rail.transform.position+=robot.position-point;
        var slide=rail.carriage.GetChild(0);slide.SetParent(rail.transform,true);
        rail.carriage.position=robot.position;slide.SetParent(rail.carriage,true);robot.SetParent(rail.carriage,true);
        EditorSceneManager.MarkSceneDirty(cycle.gameObject.scene);EditorSceneManager.SaveScene(cycle.gameObject.scene);
        InspectInstalledBatch();
    }
    [MenuItem("Tools/EPIC/Inspect Robot Rail")]
    public static void InspectInstalledBatch()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/RobotMachineGrab.unity");
        var rail=Object.FindFirstObjectByType<RobotRailController>();
        Directory.CreateDirectory("ValidationReports");
        File.WriteAllLines("ValidationReports/installed-track.txt",rail.GetComponentsInChildren<Renderer>().Select(r=>r.name+" "+r.bounds.ToString("F4")+" scale="+r.transform.lossyScale));
        Physics.SyncTransforms();
        var obstacles=Object.FindObjectsByType<BoxCollider>(FindObjectsSortMode.None).Where(c=>!c.transform.IsChildOf(rail.transform)).ToArray();
        var hits=new System.Collections.Generic.List<string>();
        foreach(var mesh in rail.GetComponentsInChildren<MeshCollider>())
        {
            if(mesh.transform.IsChildOf(rail.robot.transform))continue;
            foreach(var box in obstacles)
                if(Physics.ComputePenetration(mesh,mesh.transform.position,mesh.transform.rotation,box,box.transform.position,box.transform.rotation,out _,out float depth) && depth>.001f)
                    hits.Add(mesh.name+" / "+box.name+" depth="+depth.ToString("F4"));
        }
        File.WriteAllLines("ValidationReports/rail-static-contacts.txt",hits);
    }
    public static void RepairMachineStations()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/RobotMachineGrab.unity");
        var cycle=Object.FindFirstObjectByType<RobotMachiningCycle>();
        RaiseMachine(cycle.machine,Vector3.up*.46f);
        AddRailColliders(cycle.rail);
        EditorUtility.SetDirty(cycle.machine);EditorSceneManager.SaveScene(cycle.gameObject.scene);
    }
    static void AddRailColliders(RobotRailController rail)
    {
        foreach(var filter in rail.GetComponentsInChildren<MeshFilter>())
        {
            if(filter.transform.IsChildOf(rail.robot.transform) || filter.GetComponent<Collider>())continue;
            filter.gameObject.AddComponent<MeshCollider>().sharedMesh=filter.sharedMesh;
        }
    }
    static void RaiseMachine(MachineOperationController machine,Vector3 up)
    {
        machine.toolRest+=up;machine.toolLoading+=up;
        machine.doorClosed+=up;machine.doorOpen+=up;machine.doorClearance+=up;
        machine.carriageLoading+=up;machine.carriageProcessing+=up;
    }
    public static void BuildBatch()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/RobotMachineGrab.unity");
        var cycle=Object.FindFirstObjectByType<RobotMachiningCycle>();
        if(Object.FindFirstObjectByType<RobotRailController>())throw new System.InvalidOperationException("Rail already installed.");
        var robot=cycle.robot;
        Vector3 up=Vector3.up*.46f;
        // Raise all equipment and world-space stations together, retaining the
        // previously checked IK/door geometry. Supports extend down to the floor.
        foreach(var root in cycle.gameObject.scene.GetRootGameObjects())
            if(root.name!="Workshop Floor" && !root.GetComponent<Light>())root.transform.position+=up;
        cycle.machinePosition+=up;
        RaiseMachine(cycle.machine,up);
        cycle.conveyor.feedStart+=up;cycle.conveyor.pickupPosition+=up;
        robot.viewCamera.GetComponent<RobotOrbitCamera>().focus+=up;
        Vector3 axis=Quaternion.Euler(0,25,0)*Vector3.forward;
        Vector3 pickupOffset=-axis*.9f,outputOffset=axis*.6f;
        cycle.conveyor.transform.position+=pickupOffset;
        cycle.conveyor.feedStart+=pickupOffset;cycle.conveyor.pickupPosition+=pickupOffset;
        cycle.raw.position=cycle.conveyor.pickupPosition;
        GameObject.Find("Finished Parts Table").transform.position+=outputOffset;
        cycle.finished.position+=outputOffset;
        var model=PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/track.fbx")) as GameObject;
        PrefabUtility.UnpackPrefabInstance(model,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        model.name="Robot Linear Rail - track";
        // One supplied 5.7 m module is sufficient for this cell; surplus modules
        // remain in the imported asset. No stretching of the robot or gripper.
        for(int i=model.transform.childCount-1;i>=1;i--)Object.DestroyImmediate(model.transform.GetChild(i).gameObject);
        var slide=model.GetComponentsInChildren<Transform>().Single(t=>t.name=="______________.STEP1");
        var mount=slide.GetComponentsInChildren<Renderer>().Single(r=>r.name=="DEFAULT.igs");
        // The long HGR45 guides are fixed, whereas HGW45 bearing blocks move.
        foreach(var guide in slide.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("HGR45_")).ToArray())guide.SetParent(model.transform,true);
        // A static CAD drag-chain loop cannot follow the moving carriage.
        // Preserve it in the asset, and keep only the moving drive/slide here.
        foreach(var chain in slide.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("H65Q.")).ToArray())chain.gameObject.SetActive(false);
        model.transform.localScale*=.65f;
        model.transform.rotation=Quaternion.Euler(0,25,0)*model.transform.rotation;
        Vector3 mountPoint=mount.bounds.center;mountPoint.y=mount.bounds.max.y;
        model.transform.position+=robot.transform.position-mountPoint;
        var rail=model.AddComponent<RobotRailController>();rail.robot=robot;rail.direction=axis;
        rail.carriage=new GameObject("Robot carriage assembly").transform;
        rail.carriage.position=robot.transform.position;
        rail.carriage.SetParent(model.transform,true);
        slide.SetParent(rail.carriage,true);
        robot.transform.SetParent(rail.carriage,true);
        cycle.rail=rail;
        AddRailColliders(rail);
        // Restore ground contact for the raised conveyor and output supports.
        foreach(var t in cycle.conveyor.GetComponentsInChildren<Transform>().Concat(GameObject.Find("Finished Parts Table").GetComponentsInChildren<Transform>()))
        {
            if(!t.name.StartsWith("Central line leg ") && t.name!="Table Leg")continue;
            var size=t.localScale;size.y+=up.y;t.localScale=size;t.position-=up*.5f;
        }
        var machineBounds=new Bounds(cycle.machine.transform.position,Vector3.zero);
        foreach(var r in cycle.machine.GetComponentsInChildren<Renderer>())machineBounds.Encapsulate(r.bounds);
        var foundation=GameObject.CreatePrimitive(PrimitiveType.Cube);foundation.name="Machine foundation";
        foundation.transform.position=new Vector3(machineBounds.center.x,up.y*.5f,machineBounds.center.z);
        foundation.transform.localScale=new Vector3(machineBounds.size.x*.8f,up.y,machineBounds.size.z*.8f);
        foundation.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Conveyor_Legs.mat");
        foreach(var r in model.GetComponentsInChildren<Renderer>())
        {
            if(r.transform.IsChildOf(robot.transform))continue;
            // Convert embedded legacy CAD materials to URP so the rail is visible.
            var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color=r.name.StartsWith("HGR")?new Color(.65f,.69f,.72f):new Color(.22f,.27f,.30f);
            string path="Assets/Materials/RobotRail_"+(r.name.StartsWith("HGR")?"Steel":"Frame")+".mat";
            var existing=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!existing){AssetDatabase.CreateAsset(material,path);existing=material;}else Object.DestroyImmediate(material);
            r.sharedMaterials=Enumerable.Repeat(existing,r.sharedMaterials.Length).ToArray();
        }
        var camera=robot.viewCamera;camera.transform.position=new Vector3(-5.8f,5.5f,-6.5f);
        var orbit=camera.GetComponent<RobotOrbitCamera>();orbit.focus=new Vector3(-.5f,1.35f,-.65f);camera.transform.LookAt(orbit.focus);
        EditorUtility.SetDirty(cycle);EditorUtility.SetDirty(cycle.conveyor);
        EditorSceneManager.MarkSceneDirty(cycle.gameObject.scene);EditorSceneManager.SaveScene(cycle.gameObject.scene);AssetDatabase.SaveAssets();
        Directory.CreateDirectory("ValidationReports");
        FactoryConveyorLayout.Capture(camera,"ValidationReports/robot-rail-layout.png");
        Debug.Log("[RobotRail] Installed supplied slide and fixed guides; stations -0.9 / 0 / 0.6 m.");
        FinishSupportLayoutBatch();
    }
}

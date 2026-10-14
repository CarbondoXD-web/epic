using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MachineDoorSetup
{
    [System.Serializable] public sealed class SweepReport
    {
        public int samples;
        public float travelMetres, maxPenetration;
        public bool passed;
        public System.Collections.Generic.List<string> contacts=new System.Collections.Generic.List<string>();
    }
    public static SweepReport CheckSweep(MachineOperationController machine)
    {
        var report=new SweepReport {samples=201,travelMetres=machine.doorHasClearanceStage?Vector3.Distance(machine.doorClosed,machine.doorClearance)+Vector3.Distance(machine.doorClearance,machine.doorOpen):Vector3.Distance(machine.doorClosed,machine.doorOpen)};
        var temporary=new GameObject("Temporary door sweep probe");
        temporary.hideFlags=HideFlags.HideAndDontSave;
        temporary.transform.position=Vector3.one*1000;
        temporary.transform.localScale=machine.door.lossyScale;
        var probe=temporary.AddComponent<MeshCollider>();
        probe.sharedMesh=machine.door.GetComponent<MeshFilter>().sharedMesh;probe.convex=true;
        Vector3 previous=machine.door.position;
        try
        {
            for(int i=0;i<report.samples;i++)
            {
                machine.door.position=machine.DoorSweepPosition(i/(float)(report.samples-1));
                Physics.SyncTransforms();
                var obstacles=new System.Collections.Generic.List<Collider>(machine.GetComponentsInChildren<Collider>());
                var conveyor=Object.FindFirstObjectByType<ConveyorFeedController>();
                if(conveyor)obstacles.AddRange(conveyor.GetComponentsInChildren<Collider>());
                var output=GameObject.Find("Finished Parts Table");
                if(output)obstacles.AddRange(output.GetComponentsInChildren<Collider>());
                foreach(var obstacle in obstacles)
                {
                    if(obstacle.transform.IsChildOf(machine.door))continue;
                    if(Physics.ComputePenetration(probe,machine.door.position,machine.door.rotation,obstacle,obstacle.transform.position,obstacle.transform.rotation,out _,out float depth) && depth>.001f)
                    {
                        report.maxPenetration=Mathf.Max(report.maxPenetration,depth);
                        report.contacts.Add(i+":"+obstacle.name+":"+depth.ToString("F4"));
                    }
                }
            }
            report.passed=report.contacts.Count==0;
            Directory.CreateDirectory("ValidationReports");
            File.WriteAllText("ValidationReports/door-clearance.json",JsonUtility.ToJson(report,true));
            return report;
        }
        finally {machine.door.position=previous;Physics.SyncTransforms();Object.DestroyImmediate(temporary);}
    }
    public static void ApplyBatch()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/RobotMachineGrab.unity");
        Apply();
    }
    [MenuItem("Tools/EPIC/Set Up Sliding Chamber Door")]
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        var machine=Object.FindFirstObjectByType<MachineOperationController>();
        if(!machine)throw new System.InvalidOperationException("Open RobotMachineGrab first.");
        Undo.RegisterFullObjectHierarchyUndo(machine.gameObject,"Set up chamber sliding door");
        Vector3 normal=Quaternion.Euler(0,-65,0)*Vector3.forward;
        Vector3 travel=Quaternion.Euler(0,-65,0)*Vector3.right;
        // Fixed factory coordinates make repeated setup idempotent.
        machine.doorClosed=new Vector3(1.1860356f,1.0051379f,-.74156696f)+normal*.10f;
        machine.doorHasClearanceStage=true;
        machine.doorClearance=machine.doorClosed+normal*.20f+Vector3.up*.20f;
        machine.doorOpen=machine.doorClearance-travel*.70f;
        machine.door.position=machine.doorClosed;
        machine.doorSeconds=1.6f;
        var bounds=machine.door.GetComponent<Renderer>().bounds;
        RobotGrabController.ProjectionRange(machine.door,Vector3.zero,travel,out float min,out float max);
        var metal=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Conveyor_Legs.mat");
        for(int i=0;i<1;i++)
        {
            string name=i==0?"Door upper guide track":"Door lower guide track";
            var existing=machine.transform.Find(name);
            var rail=existing?existing.gameObject:GameObject.CreatePrimitive(PrimitiveType.Cube);
            if(!existing){rail.name=name;rail.transform.SetParent(machine.transform,true);Undo.RegisterCreatedObjectUndo(rail,"Create chamber door guide");}
            Vector3 center=bounds.center+normal*.20f+Vector3.up*.20f-travel*.35f;
            center.y=bounds.max.y+.235f;
            rail.transform.SetPositionAndRotation(center,Quaternion.LookRotation(travel));
            rail.transform.localScale=Vector3.one;
            Vector3 scale=rail.transform.lossyScale;
            rail.transform.localScale=new Vector3(.04f/scale.x,.03f/scale.y,(max-min+.70f+.08f)/scale.z);
            rail.GetComponent<Renderer>().sharedMaterial=metal;
        }
        FactoryStructureSetup.Build(machine);
        EditorUtility.SetDirty(machine);
        EditorSceneManager.MarkSceneDirty(machine.gameObject.scene);
        EditorSceneManager.SaveScene(machine.gameObject.scene);
        Debug.Log("[Door setup] Door withdraws 0.20m, then slides 0.70m away from original machine attachments; duration=1.6s.");
    }
    public static void InspectBatch()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/RobotMachineGrab.unity");
        var machine=Object.FindFirstObjectByType<MachineOperationController>();
        var mesh=machine.door.GetComponent<MeshFilter>();
        var temporary=new GameObject("Temporary door clearance probe");
        temporary.hideFlags=HideFlags.HideAndDontSave;
        temporary.transform.position=Vector3.one*1000;
        temporary.transform.localScale=mesh.transform.lossyScale;
        var probe=temporary.AddComponent<MeshCollider>();probe.sharedMesh=mesh.sharedMesh;probe.convex=true;
        var report=new StringBuilder();
        var root=machine.transform;
        report.AppendLine("machine rotation="+root.eulerAngles+" door rotation="+machine.door.eulerAngles+" size="+mesh.sharedMesh.bounds.size+" scale="+mesh.transform.lossyScale);
        Vector3 initial=machine.door.position;
        try
        {
            foreach(float outward in new[]{0f,.03f,.06f,.10f,.15f,.20f,.30f})
            {
                Vector3 normal=Quaternion.Euler(0,-65,0)*Vector3.forward;
                report.AppendLine("OUTWARD "+outward);
                for(int i=0;i<=20;i++)
                {
                    machine.door.position=Vector3.Lerp(machine.doorClosed,machine.doorOpen,i/20f)+normal*outward;
                    Physics.SyncTransforms();
                    foreach(var obstacle in root.GetComponentsInChildren<Collider>())
                    {
                        if(obstacle.transform==machine.door || obstacle.transform.IsChildOf(machine.door))continue;
                        if(Physics.ComputePenetration(probe,machine.door.position,machine.door.rotation,obstacle,obstacle.transform.position,obstacle.transform.rotation,out _,out float depth) && depth>.001f)
                            report.AppendLine(i+" "+obstacle.name+" "+depth.ToString("F4"));
                    }
                }
            }
            Directory.CreateDirectory("ValidationReports");
            File.WriteAllText("ValidationReports/door-sweep.txt",report.ToString());
            var camera=machine.robot.viewCamera;
            machine.door.position=machine.doorOpen;
            FactoryConveyorLayout.Capture(camera,"ValidationReports/door-old-open.png");
        }
        finally {machine.door.position=initial;Object.DestroyImmediate(temporary);}
    }
}

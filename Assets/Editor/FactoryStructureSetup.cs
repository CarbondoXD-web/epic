using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class FactoryStructureSetup
{
    [Serializable] public sealed class Report
    {
        public bool passed;
        public System.Collections.Generic.List<string> checks=new System.Collections.Generic.List<string>();
    }
    public static void ApplyBatch()
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/RobotMachineGrab.unity");
        FactoryConveyorLayout.Apply();
        MachineDoorSetup.Apply();
        Check(UnityEngine.Object.FindFirstObjectByType<RobotMachiningCycle>());
    }
    public static void CheckBatch()
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/RobotMachineGrab.unity");
        Check(UnityEngine.Object.FindFirstObjectByType<RobotMachiningCycle>());
    }
    public static void Build(MachineOperationController machine)
    {
        var renderers=machine.GetComponentsInChildren<Renderer>();
        var tray=renderers.Single(r=>r.name=="CONV-SHELF-VF-SM");
        var holder=renderers.Single(r=>r.name=="CONV-HOLDER-VF-XS");
        Vector3 along=Quaternion.Euler(0,-65,0)*Vector3.right;
        Vector3 normal=Quaternion.Euler(0,-65,0)*Vector3.forward;
        RobotGrabController.ProjectionRange(tray.transform,Vector3.zero,along,out float low,out float high);
        RobotGrabController.ProjectionRange(tray.transform,Vector3.zero,normal,out float back,out float front);
        Vector3 center=along*((low+high)*.5f)+normal*((back+front)*.5f);
        float width=high-low,depth=front-back;
        float upperBottom=holder.bounds.min.y;
        var metal=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Conveyor_Legs.mat");
        // Preserve both CAD attachments as useful shelves on one grounded
        // tool rack. No suspended shelf or invisible supporting collider.
        for(int i=0;i<4;i++)
        {
            Vector3 corner=center+along*((i<2?-1:1)*(width*.5f-.025f))+normal*((i%2==0?-1:1)*(depth*.5f-.025f));
            float height=upperBottom-.03f;
            Cube(machine,"Tool rack upright "+i,corner+Vector3.up*(height*.5f),new Vector3(.05f,height,.05f),along,metal);
        }
        Cube(machine,"Tool rack upper shelf",center+Vector3.up*(upperBottom-.015f),new Vector3(depth,.03f,width),along,metal);
        Cube(machine,"Tool rack lower support",center+Vector3.up*(tray.bounds.min.y-.025f),new Vector3(depth,.05f,width),along,metal);
        Cube(machine,"Tool rack floor base",center+Vector3.up*.025f,new Vector3(depth+.08f,.05f,width+.08f),along,metal);
        // Short rigid spacers join the external door guides to the cabinet;
        // their vertical envelope stays outside the moving door panel.
        foreach(string railName in new[]{"Door upper guide track","Door lower guide track"})
        {
            var rail=machine.transform.Find(railName);
            for(int i=0;i<3;i++)
            {
                Vector3 position=rail.position+along*((i-1)*.63f)-normal*.06f;
                Cube(machine,railName+" mounting spacer "+i,position,new Vector3(.16f,.025f,.07f),along,metal);
            }
        }
    }
    static void Cube(MachineOperationController machine,string name,Vector3 position,Vector3 size,Vector3 forward,Material material)
    {
        var existing=machine.transform.Find(name);
        var piece=existing?existing.gameObject:GameObject.CreatePrimitive(PrimitiveType.Cube);
        if(!existing){piece.name=name;piece.transform.SetParent(machine.transform,true);Undo.RegisterCreatedObjectUndo(piece,"Create factory support");}
        piece.transform.SetPositionAndRotation(position,Quaternion.LookRotation(forward));
        piece.transform.localScale=Vector3.one;
        var scale=piece.transform.lossyScale;
        piece.transform.localScale=new Vector3(size.x/scale.x,size.y/scale.y,size.z/scale.z);
        piece.GetComponent<Renderer>().sharedMaterial=material;
    }
    public static Report Check(RobotMachiningCycle cycle)
    {
        var report=new Report();
        var machine=cycle.machine;
        var upper=machine.transform.Find("Tool rack upper shelf").GetComponent<Renderer>().bounds;
        for(int i=0;i<4;i++)
        {
            var post=machine.transform.Find("Tool rack upright "+i).GetComponent<Renderer>().bounds;
            Require(Mathf.Abs(post.min.y)<.001f && Mathf.Abs(post.max.y-upper.min.y)<.001f,"Tool rack upright must connect floor and upper shelf.");
        }
        var tray=machine.GetComponentsInChildren<Renderer>().Single(r=>r.name=="CONV-SHELF-VF-SM");
        var holder=machine.GetComponentsInChildren<Renderer>().Single(r=>r.name=="CONV-HOLDER-VF-XS");
        var lower=machine.transform.Find("Tool rack lower support").GetComponent<Renderer>().bounds;
        Require(Mathf.Abs(lower.max.y-tray.bounds.min.y)<.001f,"Lower tray must sit on the rack support.");
        Require(Mathf.Abs(upper.max.y-holder.bounds.min.y)<.001f,"Upper attachment must sit on the rack shelf.");
        report.checks.Add("Both original CAD attachments sit on a four-post grounded tool rack.");
        var bed=cycle.conveyor.transform.Find("Central line bed").GetComponent<Renderer>().bounds;
        for(int i=0;i<5;i++)
        {
            var beam=cycle.conveyor.transform.Find("Central line crossbeam "+i).GetComponent<Renderer>().bounds;
            Require(Mathf.Abs(beam.max.y-bed.min.y)<.001f,"Conveyor beam must meet the bed underside.");
            foreach(int side in new[]{-1,1})
            {
                var leg=cycle.conveyor.transform.Find("Central line leg "+i+" "+side).GetComponent<Renderer>().bounds;
                Require(Mathf.Abs(leg.min.y)<.001f && Mathf.Abs(leg.max.y-beam.min.y)<.001f,"Conveyor leg must join floor and crossbeam.");
            }
        }
        report.checks.Add("All ten conveyor legs join the floor and five crossbeams; crossbeams meet the bed.");
        foreach(string name in new[]{"Door upper guide track","Door lower guide track"})
            for(int i=0;i<3;i++)Require(machine.transform.Find(name+" mounting spacer "+i)!=null,"Door track mounting spacer is missing.");
        report.checks.Add("Door upper/lower guides have visible rigid mounting spacers.");
        Physics.SyncTransforms();
        var output=GameObject.Find("Finished Parts Table");
        foreach(var rack in machine.GetComponentsInChildren<BoxCollider>().Where(c=>c.name.StartsWith("Tool rack ")))
            foreach(var obstacle in output.GetComponentsInChildren<Collider>())
            {
                bool intersects=Physics.ComputePenetration(rack,rack.transform.position,rack.transform.rotation,obstacle,obstacle.transform.position,obstacle.transform.rotation,out _,out float penetration);
                Require(!intersects || penetration<=.001f,"Tool rack overlaps finished table: "+rack.name+":"+obstacle.name);
            }
        report.checks.Add("Tool rack base, uprights and shelves clear the finished-part table.");
        report.passed=true;
        Directory.CreateDirectory("ValidationReports");
        File.WriteAllText("ValidationReports/structure-validation.json",JsonUtility.ToJson(report,true));
        return report;
    }
    static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
}

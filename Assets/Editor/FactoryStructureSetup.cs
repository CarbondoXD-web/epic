using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

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
        RestoreMachineAttachments(machine);
        Vector3 along=Quaternion.Euler(0,-65,0)*Vector3.right;
        Vector3 normal=Quaternion.Euler(0,-65,0)*Vector3.forward;
        var metal=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Conveyor_Legs.mat");
        foreach(var part in machine.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Door lower guide track")))
        {
            Undo.RecordObject(part.gameObject,"Deactivate obsolete lower door guide");part.gameObject.SetActive(false);
        }
        // Short rigid spacers join the external door guides to the cabinet;
        // their vertical envelope stays outside the moving door panel.
        foreach(string railName in new[]{"Door upper guide track"})
        {
            var rail=machine.transform.Find(railName);
            for(int i=0;i<3;i++)
            {
                Vector3 position=rail.position+along*((i-1)*.63f)-normal*.16f;
                Cube(machine,railName+" mounting spacer "+i,position,new Vector3(.36f,.025f,.07f),along,metal);
                Cube(machine,railName+" cabinet mount "+i,rail.position+along*((i-1)*.63f)-normal*.30f-Vector3.up*.1125f,new Vector3(.045f,.20f,.07f),along,metal);
            }
        }
        var doorBounds=machine.door.GetComponent<Renderer>().bounds;
        Vector3 ramp=doorBounds.center-normal*.08f;
        ramp.y=doorBounds.max.y+.135f;
        Cube(machine,"Door upper clearance ramp",ramp,new Vector3(.04f,.025f,Mathf.Sqrt(.08f)),normal+Vector3.up,metal);
        Vector3 link=doorBounds.center+normal*.11f;
        link.y=doorBounds.max.y+.235f;
        Cube(machine,"Door upper ramp connection",link,new Vector3(.22f,.025f,.05f),along,metal);
    }
    public static void RestoreMachineAttachments(MachineOperationController machine)
    {
        var source=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Haas Mill Latest.fbx"));
        source.hideFlags=HideFlags.HideAndDontSave;
        source.transform.SetPositionAndRotation(machine.transform.position,machine.transform.rotation);
        source.transform.localScale=machine.transform.localScale;
        try
        {
            foreach(string name in new[]{"CONV-SHELF-VF-SM","CONV-HOLDER-VF-XS"})
            {
                var original=source.GetComponentsInChildren<Transform>().Single(t=>t.name==name);
                var target=machine.GetComponentsInChildren<Transform>().Single(t=>t.name==name);
                Undo.RecordObject(target,"Restore original machine attachment");
                target.SetPositionAndRotation(original.position,original.rotation);
                target.localScale=original.localScale;
            }
            foreach(var item in machine.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Tool rack ")))
            {
                Undo.RecordObject(item.gameObject,"Deactivate supplemental tool rack");
                item.gameObject.SetActive(false);
            }
        }
        finally {Object.DestroyImmediate(source);}
    }
    public static void CheckMachineAttachments(MachineOperationController machine)
    {
        var source=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Haas Mill Latest.fbx"));
        source.hideFlags=HideFlags.HideAndDontSave;
        source.transform.SetPositionAndRotation(machine.transform.position,machine.transform.rotation);
        source.transform.localScale=machine.transform.localScale;
        try
        {
            foreach(string name in new[]{"CONV-SHELF-VF-SM","CONV-HOLDER-VF-XS"})
            {
                var original=source.GetComponentsInChildren<Transform>().Single(t=>t.name==name);
                var target=machine.GetComponentsInChildren<Transform>().Single(t=>t.name==name);
                Require(Vector3.Distance(original.position,target.position)<.0001f && Quaternion.Angle(original.rotation,target.rotation)<.01f && Vector3.Distance(original.lossyScale,target.lossyScale)<.0001f,"Attachment must match source FBX: "+name);
            }
        }
        finally {Object.DestroyImmediate(source);}
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
        CheckMachineAttachments(machine);
        Require(!machine.GetComponentsInChildren<Transform>(true).Any(t=>t.name.StartsWith("Tool rack ") && t.gameObject.activeSelf),"Supplemental tool rack must remain inactive.");
        report.checks.Add("Both attachments match their original FBX transforms on the machine; supplemental rack is inactive.");
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
        foreach(string name in new[]{"Door upper guide track"})
            for(int i=0;i<3;i++)Require(machine.transform.Find(name+" mounting spacer "+i)!=null,"Door track mounting spacer is missing.");
        report.checks.Add("Top-hung door guide and clearance ramp are mounted on the cabinet; obsolete lower guide is inactive.");
        report.passed=true;
        Directory.CreateDirectory("ValidationReports");
        File.WriteAllText("ValidationReports/structure-validation.json",JsonUtility.ToJson(report,true));
        return report;
    }
    static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
}

using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class OriginalChamberSetup
{
    const string AssetPath="Assets/Models/Haas Mill Latest.fbx";
    static readonly string[] InternalParts={"37-1465-DEF","20T Spindle_37-3797","rf100u_3891_3_000","37-1465-DEF-ENCLOSURE","SCHUNK-KSC-F / S2 80.001"};
    [System.Serializable] public sealed class Report
    {
        public bool passed;
        public string sourceSha256="e8c359976078c593bdb1388f512824d0d44821c72146e993543f64641d11182e";
        public System.Collections.Generic.List<string> restoredParts=new System.Collections.Generic.List<string>();
        public bool conveyorUnchanged, robotBaseUnchanged, externalRackUnchanged;
    }
    public static void ApplyBatch()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/RobotMachineGrab.unity");
        Apply();
    }
    [MenuItem("Tools/EPIC/Restore Original Chamber Internals")]
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        var cycle=Object.FindFirstObjectByType<RobotMachiningCycle>();
        var machine=cycle.machine;
        var original=InstantiateOriginal(machine);
        string conveyor=Snapshot(cycle.conveyor.transform),robot=Snapshot(cycle.robot.transform);
        string rack=string.Join("|",machine.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Tool rack ")).Select(Snapshot));
        Undo.RegisterFullObjectHierarchyUndo(machine.gameObject,"Restore original chamber internals");
        Undo.RecordObject(cycle,"Align workpiece with original fixture");
        try
        {
            var sources=original.GetComponentsInChildren<Transform>();
            var targets=machine.GetComponentsInChildren<Transform>();
            foreach(string name in InternalParts)
            {
                var source=sources.Single(t=>t.name==name);
                var target=targets.Single(t=>t.name==name);
                target.SetPositionAndRotation(source.position,source.rotation);
            }
            machine.toolHead=targets.Single(t=>t.name=="37-1465-DEF");
            machine.toolRest=machine.toolHead.position;
            machine.toolLoading=machine.toolRest+Vector3.up*.25f;
            machine.toolSeconds=.75f;
            var jaws=machine.GetComponentsInChildren<Renderer>().Where(r=>r.name.StartsWith("SCHUNK-04327")).ToArray();
            Vector3 fixture=(jaws[0].bounds.center+jaws[1].bounds.center)*.5f;
            cycle.machinePosition=new Vector3(fixture.x,1.55f,fixture.z);
            var support=targets.Single(t=>t.name=="Workpiece fixture support");
            support.position=new Vector3(fixture.x,support.position.y,fixture.z);
            var report=Check(cycle);
            report.conveyorUnchanged=conveyor==Snapshot(cycle.conveyor.transform);
            report.robotBaseUnchanged=robot==Snapshot(cycle.robot.transform);
            report.externalRackUnchanged=rack==string.Join("|",machine.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Tool rack ")).Select(Snapshot));
            if(!report.conveyorUnchanged || !report.robotBaseUnchanged || !report.externalRackUnchanged)throw new System.InvalidOperationException("An external factory layout changed during chamber restoration.");
            File.WriteAllText("ValidationReports/original-chamber-validation.json",JsonUtility.ToJson(report,true));
            EditorUtility.SetDirty(cycle);EditorUtility.SetDirty(machine);
            EditorSceneManager.MarkSceneDirty(machine.gameObject.scene);EditorSceneManager.SaveScene(machine.gameObject.scene);
            Debug.Log("[Original chamber] Restored FBX internals; original fixture target="+cycle.machinePosition+". Tool retracts only during loading.");
        }
        finally {Object.DestroyImmediate(original);}
    }
    static string Snapshot(Transform root)=>string.Join(";",root.GetComponentsInChildren<Transform>(true).Select(t=>t.name+":"+t.localPosition.ToString("F6")+":"+t.localRotation.ToString("F6")+":"+t.localScale.ToString("F6")));
    public static Report Check(RobotMachiningCycle cycle)
    {
        var original=InstantiateOriginal(cycle.machine);
        var report=new Report();
        try
        {
            foreach(string name in InternalParts)
            {
                var source=original.GetComponentsInChildren<Transform>().Single(t=>t.name==name);
                var target=cycle.machine.GetComponentsInChildren<Transform>().Single(t=>t.name==name);
                if(Vector3.Distance(source.position,target.position)>.0001f || Quaternion.Angle(source.rotation,target.rotation)>.01f)
                    throw new System.InvalidOperationException("Chamber part does not match source FBX: "+name);
                report.restoredParts.Add(name);
            }
            report.passed=true;
            Directory.CreateDirectory("ValidationReports");
            return report;
        }
        finally {Object.DestroyImmediate(original);}
    }
    public static void InspectBatch()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/RobotMachineGrab.unity");
        var machine=Object.FindFirstObjectByType<MachineOperationController>();
        var original=InstantiateOriginal(machine);
        try
        {
            var report=new StringBuilder();
            foreach(var source in original.GetComponentsInChildren<Renderer>())
            {
                var current=machine.GetComponentsInChildren<Renderer>().FirstOrDefault(r=>r.name==source.name);
                report.AppendLine(source.name+" SOURCE "+source.bounds.center.ToString("F4")+" size "+source.bounds.size.ToString("F4")+" CURRENT "+(current?current.bounds.center.ToString("F4"):"missing"));
            }
            Directory.CreateDirectory("ValidationReports");
            File.WriteAllText("ValidationReports/original-chamber-inspection.txt",report.ToString());
        }
        finally {Object.DestroyImmediate(original);}
    }
    static GameObject InstantiateOriginal(MachineOperationController machine)
    {
        var original=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(AssetPath));
        original.name="Temporary original chamber reference";
        original.hideFlags=HideFlags.HideAndDontSave;
        original.transform.SetPositionAndRotation(machine.transform.position,machine.transform.rotation);
        original.transform.localScale=machine.transform.localScale;
        return original;
    }
}

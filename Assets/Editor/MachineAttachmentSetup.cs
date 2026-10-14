using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MachineAttachmentSetup
{
    public static void ApplyBatch()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/RobotMachineGrab.unity");
        Apply();
    }
    [MenuItem("Tools/EPIC/Restore Machine-Mounted Attachments")]
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        MachineDoorSetup.Apply();
        var cycle=Object.FindFirstObjectByType<RobotMachiningCycle>();
        // Retain the output table; use a reachable outboard slot on its top.
        Vector3 normal=Quaternion.Euler(0,-65,0)*Vector3.forward;
        Undo.RecordObject(cycle.finished,"Use outboard slot on existing output table");
        cycle.finished.position=new Vector3(.27f,1.05f,.91f)+normal*.16f;
        EditorSceneManager.MarkSceneDirty(cycle.gameObject.scene);
        EditorSceneManager.SaveScene(cycle.gameObject.scene);
        FactoryStructureSetup.Check(cycle);
    }
}

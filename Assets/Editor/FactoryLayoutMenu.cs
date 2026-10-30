using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Linq;
using System.IO;

public static class FactoryLayoutMenu
{
    [MenuItem("EPIC/打开生产线模型布局")]
    public static void Open()
    {
        if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        EditorSceneManager.OpenScene("Assets/Scenes/FactoryLayout.unity");
        if(SceneView.lastActiveSceneView)SceneView.lastActiveSceneView.LookAt(new Vector3(0,0,10),Quaternion.Euler(38,52,0),20);
    }
    public static void CheckBatch()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/FactoryLayout.unity");
        var transforms=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
        int missing=transforms.Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
        int emptyMesh=transforms.SelectMany(t=>t.GetComponents<MeshFilter>()).Count(m=>!m.sharedMesh);
        int missingMaterials=transforms.SelectMany(t=>t.GetComponents<Renderer>()).Sum(r=>r.sharedMaterials.Count(m=>!m));
        int stations=transforms.Count(t=>t.name.StartsWith("CNC "));
        int gameplay=transforms.SelectMany(t=>t.GetComponents<MonoBehaviour>()).Count(m=>m && m.GetType().Assembly==typeof(FactoryLayoutView).Assembly && !(m is FactoryLayoutView));
        var boxes=transforms.Where(t=>t.name.StartsWith("CNC ")).Select(t=>{var renderers=t.GetComponentsInChildren<Renderer>();var b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);return b;}).ToArray();
        int overlaps=0;for(int i=0;i<boxes.Length;i++)for(int j=i+1;j<boxes.Length;j++)if(boxes[i].Intersects(boxes[j]))overlaps++;
        bool passed=missing==0&&emptyMesh==0&&missingMaterials==0&&stations==6&&gameplay==0&&overlaps==0;
        Directory.CreateDirectory("ValidationReports");File.WriteAllText("ValidationReports/layout-check.txt","Passed="+passed+"\nMachines="+stations+"\nMissingScripts="+missing+"\nMissingMeshes="+emptyMesh+"\nMissingMaterials="+missingMaterials+"\nGameplayComponents="+gameplay+"\nMachineBoundsOverlaps="+overlaps);
        if(!passed)throw new System.Exception("Layout check failed");
    }
}

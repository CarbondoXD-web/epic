using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ConveyorClearanceCalibration
{
    [Serializable] sealed class Candidate { public float angle; public int contacts; public float maxDepth; public List<string> pairs=new List<string>(); }
    [Serializable] sealed class Results { public float selectedAngle, selectedFeedYawDegrees; public List<string> disabledUnusedModules=new List<string>(); public List<Candidate> candidates=new List<Candidate>(); }
    public static void CalibrateBatch()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/RobotMachineGrab.unity");
        Calibrate();
    }
    [MenuItem("Tools/EPIC/Calibrate Feed Conveyor Clearance")]
    public static void Calibrate()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first.");
        var cycle=UnityEngine.Object.FindAnyObjectByType<RobotMachiningCycle>();
        if(!cycle || !cycle.conveyor)throw new InvalidOperationException("Open RobotMachineGrab first.");
        var conveyor=cycle.conveyor;
        Transform root=conveyor.transform;
        Undo.IncrementCurrentGroup();int undoGroup=Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Calibrate feed conveyor clearance");
        Undo.RecordObject(root,"Calibrate feed conveyor clearance");
        Undo.RecordObject(conveyor,"Calibrate feed conveyor clearance");
        var results=new Results();
        var model=root.Find("Imported Conveyor Module");
        if(model)
        {
            var duplicate=model.Find("05-10274069-DE1_Simplify_1.002");
            if(duplicate && duplicate.gameObject.activeSelf)
            {
                var renderer=duplicate.GetComponent<Renderer>();
                if(renderer && Vector3.Distance(renderer.bounds.center,conveyor.beltSurface.position)>1.5f)
                {
                    Undo.RecordObject(duplicate.gameObject,"Disable disconnected conveyor duplicate");
                    results.disabledUnusedModules.Add(duplicate.name);
                    duplicate.gameObject.SetActive(false);
                }
            }
        }
        Vector3 position=root.position,feed=conveyor.feedStart,pivot=conveyor.pickupPosition;
        Quaternion rotation=root.rotation;
        var meshes=cycle.robot.GetComponentsInChildren<MeshFilter>();
        var probes=new List<MeshCollider>();
        var conveyorMeshes=conveyor.GetComponentsInChildren<MeshFilter>();
        var conveyorProbes=new List<MeshCollider>();
        var temporary=new GameObject("Temporary conveyor clearance probes");
        temporary.hideFlags=HideFlags.HideAndDontSave;
        Candidate best=null;
        try
        {
            foreach(var mesh in meshes)
            {
                var probe=new GameObject("Probe");probe.transform.SetParent(temporary.transform);
                probe.transform.position=Vector3.one*1000;
                probe.transform.localScale=mesh.transform.lossyScale;
                var collider=probe.AddComponent<MeshCollider>();collider.sharedMesh=mesh.sharedMesh;collider.convex=true;
                probes.Add(collider);
            }
            foreach(var mesh in conveyorMeshes)
            {
                var probe=new GameObject("Conveyor probe");probe.transform.SetParent(temporary.transform);
                probe.transform.position=Vector3.one*1000;
                probe.transform.localScale=mesh.transform.lossyScale;
                var collider=probe.AddComponent<MeshCollider>();collider.sharedMesh=mesh.sharedMesh;collider.convex=true;
                conveyorProbes.Add(collider);
            }
            var outputProbe=new GameObject("Output keepout");outputProbe.transform.SetParent(temporary.transform);
            outputProbe.transform.position=Vector3.one*1000;
            var outputBox=outputProbe.AddComponent<BoxCollider>();outputBox.size=new Vector3(.60f,.70f,.60f);
            Vector3 outputCenter=cycle.finished.position+Vector3.up*.20f;
            var machineColliders=cycle.machine.GetComponentsInChildren<Collider>();
            foreach(float angle in new[]{0f,90f,-90f,180f,45f,-45f,135f,-135f})
            {
                Quaternion yaw=Quaternion.AngleAxis(angle,Vector3.up);
                root.SetPositionAndRotation(pivot+yaw*(position-pivot),yaw*rotation);
                Physics.SyncTransforms();
                var candidate=new Candidate {angle=angle};
                var obstacles=conveyor.GetComponentsInChildren<Collider>();
                for(int i=0;i<meshes.Length;i++)foreach(var obstacle in obstacles)
                {
                    if(!Physics.ComputePenetration(probes[i],meshes[i].transform.position,meshes[i].transform.rotation,obstacle,obstacle.transform.position,obstacle.transform.rotation,out _,out float depth) || depth<=.001f)continue;
                    candidate.contacts++;
                    candidate.maxDepth=Mathf.Max(candidate.maxDepth,depth);
                    candidate.pairs.Add(meshes[i].name+":"+obstacle.name+":"+depth.ToString("F4"));
                }
                foreach(var obstacle in obstacles)
                {
                    if(!Physics.ComputePenetration(outputBox,outputCenter,Quaternion.identity,obstacle,obstacle.transform.position,obstacle.transform.rotation,out _,out float depth) || depth<=.001f)continue;
                    candidate.contacts++;candidate.maxDepth=Mathf.Max(candidate.maxDepth,depth);
                    candidate.pairs.Add("Output keepout:"+obstacle.name+":"+depth.ToString("F4"));
                }
                for(int i=0;i<conveyorMeshes.Length;i++)foreach(var obstacle in machineColliders)
                {
                    var mesh=conveyorMeshes[i];
                    if(!Physics.ComputePenetration(conveyorProbes[i],mesh.transform.position,mesh.transform.rotation,obstacle,obstacle.transform.position,obstacle.transform.rotation,out _,out float depth) || depth<=.001f)continue;
                    candidate.contacts++;candidate.maxDepth=Mathf.Max(candidate.maxDepth,depth);
                    candidate.pairs.Add("Conveyor:"+mesh.name+":"+obstacle.name+":"+depth.ToString("F4"));
                }
                results.candidates.Add(candidate);
                if(best==null || candidate.contacts<best.contacts || (candidate.contacts==best.contacts && candidate.maxDepth<best.maxDepth))best=candidate;
            }
            if(best==null || best.contacts!=0)throw new InvalidOperationException("No tested conveyor orientation clears the robot, machine and output keepout.");
            results.selectedAngle=best.angle;
            Quaternion selected=Quaternion.AngleAxis(best.angle,Vector3.up);
            root.SetPositionAndRotation(pivot+selected*(position-pivot),selected*rotation);
            conveyor.feedStart=pivot+selected*(feed-pivot);
            Vector3 baseDirection=cycle.machinePosition-pivot;baseDirection.y=0;
            results.selectedFeedYawDegrees=Vector3.SignedAngle(baseDirection,pivot-conveyor.feedStart,Vector3.up);
            EditorUtility.SetDirty(conveyor);
            EditorSceneManager.MarkSceneDirty(cycle.gameObject.scene);
            EditorSceneManager.SaveScene(cycle.gameObject.scene);
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log("[Conveyor calibration] Selected rotation "+best.angle+" degrees; robot/machine/output contacts=0.");
        }
        catch { root.SetPositionAndRotation(position,rotation);conveyor.feedStart=feed;throw; }
        finally
        {
            UnityEngine.Object.DestroyImmediate(temporary);
            Directory.CreateDirectory("ValidationReports");
            File.WriteAllText("ValidationReports/conveyor-calibration.json",JsonUtility.ToJson(results,true));
        }
    }
}

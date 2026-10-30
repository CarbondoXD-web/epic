using System.Collections.Generic;
using UnityEngine;

// Narrow-phase checks use convex copies of moving meshes, not just link points.
public class FactoryClearance : MonoBehaviour
{
    public FactoryProductionLine line;
    public bool checking=true,SelfCheckPassed;
    public readonly HashSet<string> Contacts=new HashSet<string>();
    readonly Dictionary<MeshFilter,MeshCollider> probes=new Dictionary<MeshFilter,MeshCollider>();
    readonly Dictionary<MeshFilter,(Vector3 position,Quaternion rotation)> previous=new Dictionary<MeshFilter,(Vector3,Quaternion)>();
    readonly Collider[] overlaps=new Collider[256];
    Transform root;
    void Start()
    {
#if !UNITY_EDITOR
        checking=false;enabled=false;return;
#else
        root=new GameObject("Collision query probes").transform;
        foreach(var mesh in line.mover.robot.GetComponentsInChildren<MeshFilter>())Probe(mesh);
        var boxObject=new GameObject("Query self check");boxObject.transform.position=Vector3.one*10000;
        var box=boxObject.AddComponent<BoxCollider>();box.size=Vector3.one*5;
        foreach(var pair in probes)
        {
            var probe=pair.Value;Physics.SyncTransforms();
            if(Physics.ComputePenetration(probe,boxObject.transform.position,Quaternion.identity,box,boxObject.transform.position,Quaternion.identity,out _,out _)){SelfCheckPassed=true;break;}
        }
        Destroy(boxObject);Debug.Log("[Factory] Collision query self check="+SelfCheckPassed);
        line.mover.robot.WorkflowPoseSample+=SampleNow;
#endif
    }
    MeshCollider Probe(MeshFilter mesh)
    {
        if(!mesh.sharedMesh || mesh.sharedMesh.vertexCount<4)return null;
        if(probes.TryGetValue(mesh,out var existing))return existing;
        var go=new GameObject("Probe "+mesh.name);go.layer=2;go.transform.SetParent(root,false);go.transform.position=Vector3.one*10000;go.transform.localScale=mesh.transform.lossyScale;
        var collider=go.AddComponent<MeshCollider>();collider.sharedMesh=mesh.sharedMesh;collider.convex=true;probes[mesh]=collider;return collider;
    }
    void LateUpdate()
    {
        SampleNow();
    }
    public void SampleNow()
    {
        if(!checking || !line.Running || line.Paused)return;
        Physics.SyncTransforms();
        foreach(var mesh in line.mover.robot.GetComponentsInChildren<MeshFilter>())Check(mesh);
        if(line.mover.Held)foreach(var mesh in line.mover.Held.Visual.GetComponentsInChildren<MeshFilter>())Check(mesh);
    }
    void Check(MeshFilter mesh)
    {
        var probe=Probe(mesh);if(!probe || !mesh.gameObject.activeInHierarchy)return;
        Vector3 position=mesh.transform.position;Quaternion rotation=mesh.transform.rotation;
        if(!previous.TryGetValue(mesh,out var last))last=(position,rotation);
        Vector3 offset=Vector3.Scale(mesh.sharedMesh.bounds.center,mesh.transform.lossyScale);
        Vector3 oldCenter=last.position+last.rotation*offset,newCenter=position+rotation*offset;
        int steps=Mathf.Max(1,Mathf.CeilToInt(Mathf.Max(Vector3.Distance(oldCenter,newCenter)/.025f,Quaternion.Angle(last.rotation,rotation)/2)));
        for(int step=1;step<=steps;step++)
        {
            float phase=step/(float)steps;Quaternion r=Quaternion.Slerp(last.rotation,rotation,phase);
            CheckAt(mesh,probe,Vector3.Lerp(oldCenter,newCenter,phase)-r*offset,r);
        }
        previous[mesh]=(position,rotation);
    }
    void CheckAt(MeshFilter mesh,MeshCollider probe,Vector3 position,Quaternion rotation)
    {
        var b=mesh.sharedMesh.bounds;Vector3 scale=mesh.transform.lossyScale;Vector3 e=Vector3.Scale(b.extents,scale);e=new Vector3(Mathf.Abs(e.x),Mathf.Abs(e.y),Mathf.Abs(e.z));
        Vector3 center=position+rotation*Vector3.Scale(b.center,scale);
        int count=Physics.OverlapBoxNonAlloc(center,e,overlaps,rotation,1<<8,QueryTriggerInteraction.Ignore);
        if(count==overlaps.Length)Record("Collision query capacity exceeded");
        for(int i=0;i<count;i++)
        {
            var obstacle=overlaps[i];
            if(!Physics.ComputePenetration(probe,position,rotation,obstacle,obstacle.transform.position,obstacle.transform.rotation,out _,out float depth) || depth<.001f)continue;
            // The mounting face is a designed touching surface, never a deep overlap.
            if(mesh.gameObject==line.mover.robot.gameObject && depth<.002f)continue;
            var station=obstacle.GetComponentInParent<FactoryMachineStation>();
            Record(mesh.name+" / "+(station?"CNC"+station.id+"/":"")+obstacle.name+" depth="+depth.ToString("F4")+" task="+line.Task);
        }
    }
    void Record(string key){if(Contacts.Add(key))Debug.LogWarning("[Factory CONTACT] "+key);}
    void OnDestroy(){if(line && line.mover && line.mover.robot)line.mover.robot.WorkflowPoseSample-=SampleNow;if(root)Destroy(root.gameObject);}
}

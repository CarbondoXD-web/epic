using System.Collections;
using UnityEngine;

public class ConveyorFeedController : MonoBehaviour
{
    public RobotGrabController robot;
    public Rigidbody raw;
    public Transform beltSurface;
    public Vector3 feedStart, pickupPosition;
    [Min(.01f)] public float feedSeconds=1.5f;
    public bool IsMoving { get; private set; }
    public bool IsReady { get; private set; }
    public Transform[] treadMarkers;
    Vector3 direction;
    Quaternion rawRotation;
    float beltPhase;

    void Awake()
    {
        rawRotation=raw.rotation;
        direction=(pickupPosition-feedStart).normalized;
        StageNext();
    }
    public void StageNext()
    {
        IsMoving=false;IsReady=false;
        raw.transform.SetParent(null,true);
        raw.interpolation=RigidbodyInterpolation.None;raw.isKinematic=true;
        raw.position=feedStart;raw.rotation=rawRotation;
        raw.transform.SetPositionAndRotation(feedStart,rawRotation);
        beltPhase=0;
        AdvanceTreads(0);
        raw.gameObject.SetActive(true);
    }
    public IEnumerator Deliver()
    {
        if(IsReady)yield break;
        raw.isKinematic=true;raw.interpolation=RigidbodyInterpolation.None;
        Vector3 start=raw.position;
        float duration=Mathf.Max(.01f,feedSeconds);
        IsMoving=true;
        for(float elapsed=0;elapsed<duration;)
        {
            if(!robot.IsDamaged)
            {
                elapsed+=WorkflowTiming.DeltaTime;
                Vector3 next=Vector3.Lerp(start,pickupPosition,Mathf.SmoothStep(0,1,elapsed/duration));
                AdvanceTreads(Vector3.Dot(next-raw.transform.position,direction));
                raw.transform.position=next;
            }
            yield return null;
        }
        raw.transform.position=pickupPosition;
        IsMoving=false;
        // A stopped conveyor presents one blank. The robot never chases a moving part.
        for(float elapsed=0;elapsed<.15f;)
        {
            if(!robot.IsDamaged)elapsed+=WorkflowTiming.DeltaTime;
            yield return null;
        }
        while(robot.IsDamaged)yield return null;
        IsReady=true;
        Debug.Log("[Conveyor] Blank delivered; belt stopped before robot pickup.");
    }
    void AdvanceTreads(float distance)
    {
        if(!beltSurface || treadMarkers==null || treadMarkers.Length==0)return;
        float length=beltSurface.TransformVector(Vector3.forward).magnitude;
        if(length<=Mathf.Epsilon)return;
        beltPhase=Mathf.Repeat(beltPhase+distance,length);
        float top=beltSurface.TransformVector(Vector3.up).magnitude*.5f+.002f;
        for(int i=0;i<treadMarkers.Length;i++)
        {
            if(!treadMarkers[i])continue;
            float offset=Mathf.Repeat(i*length/treadMarkers.Length+beltPhase,length);
            treadMarkers[i].position=beltSurface.position+direction*(offset-length*.5f)+Vector3.up*top;
        }
    }
}

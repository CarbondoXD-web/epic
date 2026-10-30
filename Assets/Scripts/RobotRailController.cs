using System.Collections;
using UnityEngine;

// The carriage moves the complete articulated robot, including an attached part.
public class RobotRailController : MonoBehaviour
{
    public RobotGrabController robot;
    public Transform carriage;
    public Vector3 direction;
    public float pickupStation=-.9f, machineStation=0f, outputStation=.6f;
    public float minimum=-1f, maximum=.7f, metresPerSecond=1.1f;
    public bool IsMoving { get; private set; }
    public float Position { get; private set; }
    public int LoadedTrips { get; private set; }
    Vector3 origin;
    void Awake(){origin=carriage.position;direction.Normalize();}
    public IEnumerator Travel(float station)
    {
        float target=Mathf.Clamp(station,minimum,maximum);
        float start=Position;
        float duration=Mathf.Max(.1f,Mathf.Abs(target-start)/metresPerSecond);
        IsMoving=true;
        bool loaded=robot.IsHolding;
        for(float elapsed=0;elapsed<duration;)
        {
            if(!robot.IsDamaged)
            {
                elapsed+=WorkflowTiming.DeltaTime;
                Position=Mathf.Lerp(start,target,Mathf.SmoothStep(0,1,elapsed/duration));
                carriage.position=origin+direction*Position;
            }
            yield return null;
        }
        while(robot.IsDamaged)yield return null;
        Position=target;carriage.position=origin+direction*target;
        IsMoving=false;
        if(loaded && Mathf.Abs(start-target)>.05f)LoadedTrips++;
        Debug.Log("[RobotRail] ARRIVED station="+Position+" holding="+loaded);
    }
}

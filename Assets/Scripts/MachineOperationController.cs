using System.Collections;
using UnityEngine;

public class MachineOperationController : MonoBehaviour
{
    public RobotGrabController robot;
    public Transform door, carriage;
    public Transform toolHead;
    public Vector3 toolRest, toolLoading;
    [Min(.2f)] public float toolSeconds=.75f;
    public Renderer indicator;
    [Min(.2f)] public float doorSeconds=.85f, tableSeconds=.7f;
    public Vector3 doorClosed, doorOpen, carriageLoading, carriageProcessing;
    public bool doorHasClearanceStage;
    public Vector3 doorClearance;
    public bool DoorIsOpen { get; private set; }
    public bool DoorIsMoving { get; private set; }
    public bool TableIsLoading { get; private set; } = true;
    public bool IsProcessing { get; private set; }
    public bool IsBusy { get; set; }
    MaterialPropertyBlock lamp;
    Color lastColor;

    void Awake() { lamp=new MaterialPropertyBlock(); DoorIsOpen=false; }
    void Update()
    {
        Color color=robot.IsDamaged?Color.red:IsProcessing?new Color(0.1f,1f,.2f):IsBusy?new Color(1f,.6f,.02f):new Color(.1f,1f,.2f);
        if(color==lastColor || !indicator)return;
        lastColor=color;
        lamp.SetColor("_BaseColor",color);lamp.SetColor("_Color",color);
        lamp.SetColor("_EmissionColor",color*2f);
        indicator.SetPropertyBlock(lamp,0);
    }
    IEnumerator Slide(Transform target,Vector3 position,float duration)
    {
        Vector3 start=target.position;
        if(Vector3.Distance(start,position)<.0001f)yield break;
        for(float elapsed=0;elapsed<duration;)
        {
            if(!robot.IsDamaged)
            {
                elapsed+=Time.deltaTime;
                float t=Mathf.Clamp01(elapsed/duration);
                // Quintic easing keeps velocity and acceleration zero at both
                // ends of the fixed slide track: no sudden start or hard stop.
                float eased=t*t*t*(t*(6*t-15)+10);
                target.position=Vector3.Lerp(start,position,eased);
            }
            yield return null;
        }
        target.position=position;
    }
    public IEnumerator OpenDoor()
    {
        if(!TableIsLoading){Debug.LogError("[Machine] Door interlock: table has not returned.");yield break;}
        if(DoorIsOpen)yield break;
        if(toolHead)yield return Slide(toolHead,toolLoading,toolSeconds);
        DoorIsMoving=true;
        if(doorHasClearanceStage)yield return Slide(door,doorClearance,doorSeconds*.21875f);
        yield return Slide(door,doorOpen,doorSeconds*(doorHasClearanceStage ? .78125f : 1));
        DoorIsMoving=false;DoorIsOpen=true;
        Debug.Log("[Machine] Door open; loading enabled.");
    }
    public IEnumerator Process(Transform part)
    {
        // Robot has reached the outside hold point before this coroutine starts.
        yield return CloseDoor();
        part.SetParent(carriage,true);
        TableIsLoading=false;
        yield return Slide(carriage,carriageProcessing,tableSeconds);
        if(toolHead)yield return Slide(toolHead,toolRest,toolSeconds);
        IsProcessing=true;
        Debug.Log("[Machine] Door closed; carriage in processing position.");
    }
    public IEnumerator ReturnTable()
    {
        IsProcessing=false;
        if(toolHead)yield return Slide(toolHead,toolLoading,toolSeconds);
        yield return Slide(carriage,carriageLoading,tableSeconds);TableIsLoading=true;
        Debug.Log("[Machine] Carriage returned; spindle stopped.");
        yield return OpenDoor();
    }
    public IEnumerator Finish()
    {
        yield return CloseDoor();
    }
    IEnumerator CloseDoor()
    {
        DoorIsMoving=true;
        DoorIsOpen=false;
        if(doorHasClearanceStage)yield return Slide(door,doorClearance,doorSeconds*.78125f);
        yield return Slide(door,doorClosed,doorSeconds*(doorHasClearanceStage ? .21875f : 1));
        DoorIsMoving=false;
    }
    public Vector3 DoorSweepPosition(float fraction)
    {
        if(!doorHasClearanceStage)return Vector3.Lerp(doorClosed,doorOpen,fraction);
        float first=Vector3.Distance(doorClosed,doorClearance);
        float split=first/(first+Vector3.Distance(doorClearance,doorOpen));
        return fraction<split?Vector3.Lerp(doorClosed,doorClearance,fraction/split):Vector3.Lerp(doorClearance,doorOpen,(fraction-split)/(1-split));
    }
}

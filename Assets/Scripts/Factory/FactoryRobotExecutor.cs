using System.Collections;
using UnityEngine;

public class FactoryRobotExecutor : MonoBehaviour
{
    public FactoryProductionLine line;
    public RobotGrabController robot;
    public Transform carriage;
    public Vector3 railOrigin;
    public Quaternion baseRotation,entryRotation,outputRotation;
    public Vector3 transportPoint,pickupLocal;
    public float RailPosition,MinRail=0,MaxRail=22,metresPerSecond=1.43f;
    public bool Moving,Turning,ArmClear=true;
    public bool FacingLeft;
    public bool outputAtRailEnd;
    public bool tieredOutput;
    public float FacingYaw;
    public int LoadedTrips;
    public string MotionPhase;
    public FactoryWorkpiece Held;
    const float PickupSpeed=1.33f;
    public Vector3 BasePoint=>railOrigin+Vector3.forward*RailPosition;
    Quaternion Yaw=>Quaternion.Euler(0,FacingYaw,0);
    public Vector3 World(Vector3 local)=>BasePoint+Yaw*local;
    public Quaternion ToolRotation=>Yaw*entryRotation;
    public IEnumerator WaitReady(){while(line.Paused || robot.IsDamaged || line.faults.Blocked(FactoryFaultKind.GripConfirmation))yield return null;}
    IEnumerator Move(Vector3 target,float seconds=.65f,float actionSpeed=1f)
    {
        if(line.Failed)yield break;
        yield return WaitReady();yield return robot.WorkflowMove(target,seconds,actionSpeed);
        if(!robot.Reached(target))line.Halt("夹爪目标不可达，保留工件；目标 "+target.ToString("F2")+" 实际 "+robot.grip.position.ToString("F2"));
    }
    public IEnumerator Fold(float actionSpeed=1f)
    {
        robot.SetGripOrientation(ToolRotation);
        yield return Move(World(transportPoint),.8f,actionSpeed);ArmClear=!line.Failed;
    }
    public IEnumerator Travel(float target)
    {
        if(line.Failed)yield break;
        if(!ArmClear || target<MinRail-.001f || target>MaxRail+.001f){line.Halt("地轨移动联锁：机械臂未撤离或目标超出行程");yield break;}
        float start=RailPosition,duration=Mathf.Abs(start-target)/metresPerSecond;
        if(duration<.001f)yield break;
        Moving=true;
        float elapsed=0,stepped=0;
        while(stepped<duration)
        {
            yield return WaitReady();elapsed=Mathf.Min(duration,elapsed+WorkflowTiming.DeltaTime);
            while(stepped+.02f<=elapsed || (elapsed>=duration && stepped<duration))
            {
                stepped=Mathf.Min(duration,stepped+.02f);RailPosition=Mathf.Lerp(start,target,Mathf.SmoothStep(0,1,stepped/duration));
                carriage.position=railOrigin+Vector3.forward*RailPosition;robot.WorkflowPoseSample?.Invoke();
            }
            yield return null;
        }
        RailPosition=target;carriage.position=BasePoint;Moving=false;if(Held)LoadedTrips++;
    }
    IEnumerator Turn(bool left)
    {
        yield return TurnTo(left?180:0);
    }
    IEnumerator TurnTo(float angle)
    {
        if(Mathf.Abs(Mathf.DeltaAngle(FacingYaw,angle))<.01f)yield break;
        if(!ArmClear || (RailPosition>1 && RailPosition<21.8f)){line.Halt("仅允许在轨道端部安全回转区转向");yield break;}
        Turning=true;Quaternion start=robot.transform.rotation,target=Quaternion.Euler(0,angle,0)*baseRotation;
        float duration=Mathf.Max(.8f,Mathf.Abs(Mathf.DeltaAngle(FacingYaw,angle))/112.5f);
        float elapsed=0,stepped=0;
        while(stepped<duration)
        {
            yield return WaitReady();elapsed=Mathf.Min(duration,elapsed+WorkflowTiming.DeltaTime);
            while(stepped+.02f<=elapsed || (elapsed>=duration && stepped<duration))
            {
                stepped=Mathf.Min(duration,stepped+.02f);robot.transform.rotation=Quaternion.Slerp(start,target,Mathf.SmoothStep(0,1,stepped/duration));robot.WorkflowPoseSample?.Invoke();
            }
            yield return null;
        }
        robot.transform.rotation=target;FacingYaw=angle;FacingLeft=Mathf.Abs(Mathf.DeltaAngle(angle,180))<.01f;Turning=false;robot.SetGripOrientation(ToolRotation);
    }
    IEnumerator Dock(float station,bool left)
    {
        yield return Fold();
        if(Mathf.Abs(Mathf.DeltaAngle(FacingYaw,left?180:0))>.01f){yield return Travel(RailPosition>11?MaxRail:MinRail);yield return Turn(left);}
        yield return Travel(station);
    }
    void Attach(FactoryWorkpiece part)
    {
        Held=part;part.transform.SetParent(robot.grip,true);part.State=FactoryPartState.Held;robot.SetWorkflowState(true,true);
    }
    void Detach(FactoryWorkpiece part,Transform parent)
    {
        part.transform.SetParent(parent,true);Held=null;robot.SetWorkflowState(true,false);
    }
    public IEnumerator Load(FactoryMachineStation station,FactoryWorkpiece part)
    {
        robot.SetWorkflowState(true,false);
        line.Task="机械臂前往入口取料 "+part.Id;
        yield return Dock(line.directStockPickup?Mathf.Clamp(part.transform.position.z,MinRail,MaxRail):0,line.directStockPickup);
        robot.SetGripOrientation(ToolRotation);
        Vector3 pickup=(line.directStockPickup?part.transform.position:line.pickup)+Vector3.up*.025f;
        yield return Move(pickup+Vector3.up*.24f,.65f,PickupSpeed);yield return Move(pickup,.65f,PickupSpeed);
        if(line.Failed)yield break;
        var alarm=line.faults.Trigger(FactoryFaultKind.GripConfirmation,line.CreatedCount);
        if(alarm!=null){line.Task="夹紧未确认：请检查工件并重新夹紧";while(alarm.active)yield return null;}
        yield return robot.WorkflowFingers(false,PickupSpeed);Attach(part);
        yield return Move(pickup+Vector3.up*.24f,.65f,PickupSpeed);yield return Fold(PickupSpeed);
        line.Task="携带 "+part.Id+" 沿轨道前往机床 "+(station.id+1);
        yield return Dock(station.railPosition,station.leftSide);
        yield return station.machine.OpenDoor();
        if(!station.machine.DoorIsOpen || !station.machine.TableIsLoading){line.Halt("机床未满足开门/工作台到位条件");yield break;}
        station.ArmClear=false;ArmClear=false;
        robot.SetGripOrientation(station.Rotation(station.entryRotation));
        yield return Move(station.Point(station.approachPoint));
        Vector3 offset=robot.grip.position-part.transform.position;
        yield return Move(station.Point(station.loadPoint)+offset+Vector3.up*.04f);
        yield return Move(station.Point(station.loadPoint)+offset,.5f);
        if(line.Failed)yield break;
        Detach(part,line.partsRoot);part.transform.SetPositionAndRotation(station.Point(station.loadPoint),station.Rotation(station.blankRotation));
        part.StationId=station.id;station.Part=part;
        yield return robot.WorkflowFingers(true);
        yield return Move(station.Point(station.loadPoint)+offset+Vector3.up*.04f,.4f);
        yield return Move(station.Point(station.approachPoint));yield return Fold();station.ArmClear=ArmClear;
        robot.SetWorkflowState(false,false);
    }
    public IEnumerator Unload(FactoryMachineStation station,Transform slot)
    {
        station.State=FactoryStationState.Unloading;robot.SetWorkflowState(true,false);
        yield return Dock(station.railPosition,station.leftSide);
        if(!station.machine.DoorIsOpen || station.machine.IsProcessing){line.Halt("加工未停止或门未打开，禁止取件");yield break;}
        ArmClear=false;station.ArmClear=false;robot.SetGripOrientation(station.Rotation(station.entryRotation));
        yield return Move(station.Point(station.approachPoint),.65f,PickupSpeed);
        yield return Move(station.Point(station.loadPoint)+Vector3.up*.04f,.65f,PickupSpeed);yield return Move(station.Point(station.loadPoint),.5f,PickupSpeed);
        if(line.Failed)yield break;
        yield return robot.WorkflowFingers(false,PickupSpeed);var part=station.Part;Attach(part);
        yield return Move(station.Point(station.loadPoint)+Vector3.up*.04f,.45f,PickupSpeed);
        yield return Move(station.Point(station.approachPoint),.65f,PickupSpeed);yield return Fold(PickupSpeed);station.ArmClear=true;
        yield return station.machine.Finish();station.machine.IsBusy=false;
        station.Part=null;station.State=FactoryStationState.Idle;station.progress=0;station.waitSince=line.Elapsed;
        line.Task="携带阶梯轴 "+part.Id+" 前往成品货架存放位";
        // The end bay is clear of all machine doors and reserved for turning.
        yield return Travel(outputAtRailEnd?MaxRail:22);
        if(outputAtRailEnd)yield return TurnTo(-90);else{yield return Turn(false);yield return Travel(slot.position.z);}
        Quaternion outputFrame=Quaternion.Euler(0,outputAtRailEnd?-90:0,0);
        Vector3 upper=slot.position+Vector3.up*(outputAtRailEnd?.35f:.75f);
        if(!tieredOutput)yield return Move(upper,.85f);
        Quaternion visualToGrip=Quaternion.Inverse(robot.grip.rotation)*part.Visual.rotation;
        Quaternion placement=outputFrame*outputRotation*Quaternion.Inverse(visualToGrip);
        Vector3 palmDirection=robot.grip.InverseTransformDirection(robot.grip.parent.position-robot.grip.position).normalized;
        Vector3 rollAxis=outputFrame*Vector3.forward;
        Vector3 palm=Vector3.ProjectOnPlane(placement*palmDirection,rollAxis).normalized;
        float roll=Vector3.SignedAngle(palm,tieredOutput?(Vector3.back+Vector3.up*1.2f).normalized:Vector3.up,rollAxis);
        robot.SetGripOrientation(Quaternion.AngleAxis(roll,rollAxis)*placement);
        if(tieredOutput)
        {
            // Orient in open space, then enter the selected row horizontally.
            MotionPhase="orient outside rack";yield return Move(new Vector3(0,2.15f,slot.position.z-.68f),.7f);
            Vector3 front=slot.position+Vector3.back*.53f+Vector3.up*.14f;
            MotionPhase="descend outside rack";yield return Move(front,.8f);
        }
        else yield return Move(upper,.7f);
        RobotGrabController.ProjectionRange(part.Visual,robot.grip.position,Vector3.up,out float bottom,out _);
        Vector3 target=slot.position;target.y+=.003f-bottom;
        if(tieredOutput)
        {
            MotionPhase="align row";yield return Move(new Vector3(target.x,target.y+.04f,slot.position.z-.53f),.55f);
            MotionPhase="insert row";yield return Move(target+Vector3.up*.04f,.65f);
        }
        MotionPhase="lower onto shelf";yield return Move(target,.6f);
        if(line.Failed)yield break;
        Detach(part,line.partsRoot);part.State=FactoryPartState.Output;
        yield return robot.WorkflowRelease(part.Visual);
        // Delivered parts remain obstacles for later placements and withdrawal.
        if(tieredOutput)foreach(var mesh in part.Visual.GetComponentsInChildren<MeshFilter>())
        {
            mesh.gameObject.layer=8;
            var shape=mesh.gameObject.AddComponent<MeshCollider>();shape.sharedMesh=mesh.sharedMesh;shape.convex=true;
        }
        if(tieredOutput)
        {
            MotionPhase="release lift";yield return Move(target+Vector3.up*.025f,.35f);
            MotionPhase="withdraw row";yield return Move(target+Vector3.up*.025f+Vector3.back*.53f,.7f);
        }
        else
        {
            yield return Move(target+Vector3.up*.30f,1);
            yield return Move(robot.grip.position+outputFrame*Vector3.left*.25f,.6f);
        }
        MotionPhase="fold outside rack";yield return Fold();robot.SetWorkflowState(false,false);MotionPhase="";
    }
}

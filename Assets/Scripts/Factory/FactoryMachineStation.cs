using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class FactoryMachineStation : MonoBehaviour
{
    public int id;
    public float railPosition;
    public bool leftSide;
    public MachineOperationController machine;
    public Transform spindle;
    public Transform frame;
    public Vector3 loadPoint,approachPoint,holdPoint;
    public Quaternion entryRotation,blankRotation,shaftRotation;
    public FactoryStationState State=FactoryStationState.Idle;
    public FactoryWorkpiece Part;
    public int StartedCount,CompletedCount;
    public float progress,waitSince;
    public float processSeconds=30;
    public bool ArmClear=true;
    public FactoryProductionLine line;
    MaterialPropertyBlock lampBlock;
    Color lampColor;
    bool lampReady;
    public Vector3 Point(Vector3 local)=>frame.TransformPoint(local);
    public Quaternion Rotation(Quaternion local)=>frame.rotation*local;
    public bool HasFault=>line.faults.Blocked(FactoryFaultKind.DoorSensor,id);
    public void BeginProcessing()
    {
        StartedCount++;StartCoroutine(Process());
    }
    IEnumerator Process()
    {
        State=FactoryStationState.Loading;
        if(!ArmClear || !Part){line.Halt("机床 "+(id+1)+" 上料联锁未满足");yield break;}
        var alarm=line.faults.Trigger(FactoryFaultKind.DoorSensor,line.CreatedCount,id);
        if(alarm!=null){State=FactoryStationState.Fault;while(alarm.active)yield return null;}
        yield return machine.Process(Part.transform);
        if(machine.DoorIsOpen || machine.DoorIsMoving || !ArmClear){line.Halt("门未关好，禁止加工");yield break;}
        State=FactoryStationState.Processing;Part.State=FactoryPartState.Machining;
        Quaternion rest=spindle?spindle.localRotation:Quaternion.identity;
        for(float t=0;t<processSeconds;)
        {
            t+=WorkflowTiming.DeltaTime;progress=Mathf.Clamp01(t/processSeconds);
            if(spindle)spindle.Rotate(Vector3.up,480*WorkflowTiming.DeltaTime,Space.World);
            yield return null;
        }
        if(spindle)spindle.localRotation=rest;
        Part.Finished();Part.shaft.transform.rotation=Rotation(shaftRotation);
        yield return machine.ReturnTable();
        Part.transform.SetParent(line.partsRoot,true);
        Part.State=FactoryPartState.Ready;
        State=FactoryStationState.Ready;waitSince=line.Elapsed;progress=1;
    }
    public void RefreshLamp()
    {
        var color=HasFault?new Color(1,.10f,.08f):State==FactoryStationState.Idle || State==FactoryStationState.Processing?new Color(.08f,1,.25f):new Color(1,.65f,.05f);
        if(!machine.indicator)return;
        if(lampReady && lampColor==color)return;
        lampReady=true;lampColor=color;if(lampBlock==null)lampBlock=new MaterialPropertyBlock();lampBlock.SetColor("_BaseColor",color);lampBlock.SetColor("_EmissionColor",color*1.5f);machine.indicator.SetPropertyBlock(lampBlock,0);
    }
}

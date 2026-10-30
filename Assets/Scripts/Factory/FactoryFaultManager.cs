using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class FactoryFaultManager : MonoBehaviour
{
    public bool enabledForOrder=true;
    public List<FactoryAlarm> alarms=new List<FactoryAlarm>();
    public int wrongAnswers,firstCorrect;
    int[] triggers;
    bool[] emitted=new bool[3];
    public void ResetFaults(bool enabled)
    {
        enabledForOrder=enabled;alarms.Clear();wrongAnswers=firstCorrect=0;
        emitted=new bool[3];var random=new System.Random(42);
        triggers=new[]{1+random.Next(2),4+random.Next(2),8+random.Next(2)};
    }
    public FactoryAlarm Trigger(FactoryFaultKind kind,int serial,int station=-1)
    {
        int i=(int)kind;
        if(!enabledForOrder || emitted[i] || serial!=triggers[i])return null;
        emitted[i]=true;var alarm=new FactoryAlarm{kind=kind,station=station};alarms.Add(alarm);return alarm;
    }
    public bool Blocked(FactoryFaultKind kind,int station=-1) => alarms.Any(a=>a.active && a.kind==kind && (station<0 || a.station==station));
    public bool AnyActive => alarms.Any(a=>a.active);
    public bool Answer(FactoryAlarm alarm,int choice)
    {
        if(!alarm.active)return false;
        if(choice!=0){wrongAnswers++;alarm.firstTry=false;alarm.feedback="该措施不能解除联锁。请依据报警现象检查，设备保持安全停止。";return false;}
        alarm.active=false;if(alarm.firstTry)firstCorrect++;
        alarm.feedback="故障已解除，完成定位/到位确认，允许恢复。";return true;
    }
}

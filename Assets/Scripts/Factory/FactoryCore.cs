using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum FactoryStationState { Idle, Loading, Processing, Ready, Unloading, Fault }
public enum FactoryPartState { Supply, Held, Machining, Ready, Output }
public enum FactoryFaultKind { FeedJam, DoorSensor, GripConfirmation }



[Serializable]
public class FactoryAlarm
{
    public FactoryFaultKind kind;
    public int station=-1;
    public bool active=true,firstTry=true;
    public string feedback="";
    public string Title => kind==FactoryFaultKind.FeedJam?"送料超时 / 输送带卡料":kind==FactoryFaultKind.DoorSensor?"机床门到位信号异常":"夹爪夹紧未确认";
    public string Symptom => kind==FactoryFaultKind.FeedJam?"原料停在送料途中，取料位未检测到工件。":kind==FactoryFaultKind.DoorSensor?"工件已放入夹具，但关门信号未确认，加工联锁保持。":"工件仍有支承，夹紧信号未确认，禁止携件移动。";
    public string[] Options => kind==FactoryFaultKind.FeedJam?new[]{"停止送料，清除卡料并重新定位","提高输送速度继续送料","忽略到位信号，直接抓取"}:kind==FactoryFaultKind.DoorSensor?new[]{"检查门区，重新关门并复位","旁路门信号启动加工","移动工作台强制开始加工"}:new[]{"检查工件位置，重新夹紧确认","未确认夹紧也继续搬运","提高滑台速度甩离取料区"};
}







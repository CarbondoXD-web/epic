using UnityEngine;
using UnityEngine.InputSystem;
public class DirectFactoryControls : MonoBehaviour
{
    public FactoryProductionLine line;
    Font font;
    float lastRobotClick=-10;
    bool clickPending;
    static readonly float[] speeds={.5f,1f,1.5f,2f,3f};
    static readonly string[] speedNames={"0.5×","1×","1.5×","2×","3×"};
    void Awake(){font=Font.CreateDynamicFontFromOSFont("Microsoft YaHei",21);var panel=GetComponent<FactoryMachinePanel>();if(!panel)panel=gameObject.AddComponent<FactoryMachinePanel>();panel.line=line;}
    void Update()
    {
        var m=Mouse.current;
        if(m!=null && !FactoryMachinePanel.PointerOver(m.position.ReadValue()) && m.leftButton.wasPressedThisFrame && m.position.ReadValue().y>150f*Screen.width/1920f && m.position.ReadValue().y<Screen.height-170f*Screen.width/1920f)
            if(Physics.Raycast(Camera.main.ScreenPointToRay(m.position.ReadValue()),out var hit) && hit.transform.IsChildOf(line.mover.robot.transform))
            {
                if(Time.unscaledTime-lastRobotClick<.32f){clickPending=false;lastRobotClick=-10;line.mover.robot.ToggleDamage();}
                else {lastRobotClick=Time.unscaledTime;clickPending=true;}
            }
        if(clickPending && Time.unscaledTime-lastRobotClick>.32f){clickPending=false;line.StartOrder();}
        var k=Keyboard.current;if(k!=null && k.pKey.wasPressedThisFrame)line.SetPaused(!line.Paused);
    }
    void OnGUI()
    {
        float scale=Screen.width/1920f;float bottom=Screen.height/scale-145;
        GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));GUI.skin.font=font;
        var label=new GUIStyle(GUI.skin.label){fontSize=21};var button=new GUIStyle(GUI.skin.button){fontSize=22};
        GUI.Box(new Rect(20,bottom,1880,125),"");
        GUI.Label(new Rect(36,bottom+10,1500,30),"机械臂直接取料 → 机床加工 → 成品入架    |    已取原料 "+line.CreatedCount+" / 12    成品 "+line.DeliveredCount+" / 12",label);
        GUI.Label(new Rect(36,bottom+44,1500,30),line.mover.robot.IsDamaged?"机械臂已损坏：双击机械臂修复，修复后继续任务":line.Task,label);
        if(GUI.Button(new Rect(36,bottom+80,190,32),"开始搬运",button))line.StartOrder();
        if(GUI.Button(new Rect(240,bottom+80,190,32),line.Paused?"继续":"暂停",button))line.SetPaused(!line.Paused);
        if(GUI.Button(new Rect(444,bottom+80,190,32),"重新开始",button))line.Restart();
        GUI.Label(new Rect(660,bottom+80,650,32),"单击机械臂开始 · 双击损坏/修复 · P 暂停/继续",label);
        GUI.Label(new Rect(1320,bottom+80,80,32),"倍速",label);
        var speedButton=new GUIStyle(button){fontSize=20};
        var previousColor=GUI.backgroundColor;
        for(int i=0;i<speeds.Length;i++)
        {
            GUI.backgroundColor=Mathf.Approximately(WorkflowTiming.PlaybackSpeed,speeds[i])?new Color(.30f,.78f,.53f):previousColor;
            if(GUI.Button(new Rect(1400+i*94,bottom+80,86,32),speedNames[i],speedButton))WorkflowTiming.SetPlaybackSpeed(speeds[i]);
        }
        GUI.backgroundColor=previousColor;
    }
}

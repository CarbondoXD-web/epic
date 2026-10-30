using UnityEngine;

// Compact live monitor. Expanding a row never changes the production state.
public class FactoryMachinePanel : MonoBehaviour
{
    public FactoryProductionLine line;
    public bool collapsed;
    public int selected=-1;
    static FactoryMachinePanel active;
    Font font;
    Texture2D pixel;
    GUIStyle text,small,heading,rowButton;
    static readonly string[] states={"空闲","上料中","加工中","等待取件","下料中","故障"};
    readonly Color green=new Color(.20f,.85f,.38f),yellow=new Color(1f,.74f,.22f),red=new Color(1f,.35f,.30f);
    float Width=>248;
    float Height=>collapsed?34:42+line.stations.Length*34+(selected>=0?146:0)+12;
    Rect PanelRect=>new Rect(1920-Width-20,20,Width,line?Height:34);
    void OnEnable(){active=this;}
    void OnDisable(){if(active==this)active=null;}
    void Awake(){font=Font.CreateDynamicFontFromOSFont("Microsoft YaHei",18);pixel=new Texture2D(1,1);pixel.SetPixel(0,0,Color.white);pixel.Apply();}
    void OnDestroy(){if(pixel)Destroy(pixel);if(font)Destroy(font);}
    public static bool PointerOver(Vector2 screen)
    {
        if(!active||!active.enabled||!active.line)return false;
        float scale=Screen.width/1920f;
        return active.PanelRect.Contains(new Vector2(screen.x/scale,(Screen.height-screen.y)/scale));
    }
    public string Status(FactoryMachineStation station)
    {
        if(station.HasFault||station.State==FactoryStationState.Fault)return "故障";
        var value=states[(int)station.State];
        return line.Paused&&station.State==FactoryStationState.Processing?"暂停加工":value;
    }
    Color Tint(FactoryMachineStation s)=>s.HasFault||s.State==FactoryStationState.Fault?red:s.State==FactoryStationState.Idle||s.State==FactoryStationState.Processing?green:yellow;
    void Fill(Rect rect,Color color){var old=GUI.color;GUI.color=color;GUI.DrawTexture(rect,pixel);GUI.color=old;}
    void Styles()
    {
        if(text!=null)return;
        text=new GUIStyle(GUI.skin.label){font=font,fontSize=17,normal={textColor=new Color(.94f,.96f,.98f)}};
        small=new GUIStyle(text){fontSize=16};heading=new GUIStyle(text){fontSize=18,fontStyle=FontStyle.Bold};
        rowButton=new GUIStyle(GUI.skin.button){font=font,fontSize=17,alignment=TextAnchor.MiddleLeft};
    }
    void OnGUI()
    {
        if(!line||line.stations==null)return;
        var oldMatrix=GUI.matrix;float scale=Screen.width/1920f;GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));Styles();
        var panel=PanelRect;Fill(panel,new Color(.10f,.13f,.16f,.95f));
        GUI.Label(new Rect(panel.x+12,panel.y+5,160,26),"机床加工状态",heading);
        if(GUI.Button(new Rect(panel.xMax-44,panel.y+5,34,25),collapsed?"＋":"－")){collapsed=!collapsed;}
        if(!collapsed)
        {
            float y=panel.y+40;
            for(int i=0;i<line.stations.Length;i++)
            {
                var station=line.stations[i];if(!station)continue;
                var row=new Rect(panel.x+9,y,Width-18,30);
                if(GUI.Button(row,"",rowButton))selected=selected==i?-1:i;
                if(selected==i)Fill(new Rect(row.x,row.y,3,row.height),new Color(.3f,.78f,.95f));
                Fill(new Rect(row.x+9,row.y+9,8,8),Tint(station));
                GUI.Label(new Rect(row.x+25,row.y+3,63,24),"机床 "+(station.id+1).ToString("D2"),text);
                GUI.Label(new Rect(row.x+91,row.y+3,90,24),Status(station),small);
                GUI.Label(new Rect(row.xMax-48,row.y+3,44,24),Mathf.RoundToInt(station.progress*100)+"%",small);
                Fill(new Rect(row.x+25,row.yMax-4,row.width-32,2),new Color(.25f,.29f,.32f));
                Fill(new Rect(row.x+25,row.yMax-4,(row.width-32)*Mathf.Clamp01(station.progress),2),Tint(station));
                y+=34;
            }
            if(selected>=0&&selected<line.stations.Length)
            {
                var s=line.stations[selected];Fill(new Rect(panel.x+10,y+4,Width-20,1),new Color(.35f,.4f,.44f));
                GUI.Label(new Rect(panel.x+13,y+11,Width-26,24),"机床 "+(s.id+1).ToString("D2")+" · "+Status(s),heading);
                GUI.Label(new Rect(panel.x+13,y+37,Width-26,24),"工件："+(s.Part?s.Part.Id:s.State==FactoryStationState.Loading?"等待到位":"无"),small);
                GUI.Label(new Rect(panel.x+13,y+62,Width-26,24),"本批完成："+s.CompletedCount+" / 2 件",small);
                string remaining=s.State==FactoryStationState.Processing?"剩余约 "+Mathf.CeilToInt((1-s.progress)*s.processSeconds/WorkflowTiming.EffectiveSpeed)+" 秒":s.State==FactoryStationState.Ready?"加工完成，等待机械臂取件":"加工进度："+Mathf.RoundToInt(s.progress*100)+"%";
                GUI.Label(new Rect(panel.x+13,y+87,Width-26,24),remaining,small);
                GUI.Label(new Rect(panel.x+13,y+112,Width-26,24),s.HasFault?"报警：门到位异常":"报警：无",small);
            }
        }
        GUI.matrix=oldMatrix;
    }
}

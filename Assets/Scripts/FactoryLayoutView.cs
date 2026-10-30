using UnityEngine;
using UnityEngine.InputSystem;

// Camera controls for the production-line scene.
public class FactoryLayoutView : MonoBehaviour
{
    public Vector3 focus=new Vector3(0,0,10);
    float yaw=-128,pitch=38,distance=36;
    Font font;
    void Awake(){font=Font.CreateDynamicFontFromOSFont("Microsoft YaHei",20);View(0);}
    void View(int mode)
    {
        GetComponent<Camera>().orthographic=mode==1;
        GetComponent<Camera>().orthographicSize=16;
        focus=mode==3?new Vector3(0,1.05f,25.6f):mode==2?new Vector3(1,1,-1):new Vector3(0,0,10);
        yaw=mode==3?-155:mode==1?180:-128;pitch=mode==3?20:mode==1?89:38;distance=mode==3?6:mode==2?11:36;
        Pose();
    }
    void Pose(){transform.position=focus+Quaternion.Euler(-pitch,yaw,0)*Vector3.forward*distance;transform.LookAt(focus);}
    void LateUpdate()
    {
        var m=Mouse.current;
        if(m!=null && !FactoryMachinePanel.PointerOver(m.position.ReadValue()))
        {
            var d=m.delta.ReadValue();
            if(m.rightButton.isPressed){yaw+=d.x*.2f;pitch=Mathf.Clamp(pitch-d.y*.2f,8,89);}
            if(m.middleButton.isPressed)focus+=(-transform.right*d.x-transform.up*d.y)*distance*.001f;
            float wheel=m.scroll.ReadValue().y;if(Mathf.Abs(wheel)>10)wheel/=120;
            distance=Mathf.Clamp(distance*Mathf.Exp(-wheel*.12f),4,65);
        }
        var k=Keyboard.current;if(k!=null){if(k.digit1Key.wasPressedThisFrame||k.rKey.wasPressedThisFrame)View(0);if(k.digit2Key.wasPressedThisFrame)View(1);if(k.digit3Key.wasPressedThisFrame)View(2);if(k.digit4Key.wasPressedThisFrame)View(3);}
        Pose();
    }
    void OnGUI()
    {
        float s=Screen.width/1920f;GUI.matrix=Matrix4x4.Scale(new Vector3(s,s,1));
        GUI.skin.font=font;var label=new GUIStyle(GUI.skin.label){fontSize=21};
        GUI.Box(new Rect(20,20,650,85),"");
        GUI.Label(new Rect(36,28,620,30),"柔性生产线 · 全景观察",label);
        GUI.Label(new Rect(36,62,620,30),"右键旋转 / 中键平移 / 滚轮缩放",label);
        var b=new GUIStyle(GUI.skin.button){fontSize=20};
        if(GUI.Button(new Rect(20,118,145,40),"1 整线全景",b))View(0);
        if(GUI.Button(new Rect(175,118,145,40),"2 俯视布局",b))View(1);
        if(GUI.Button(new Rect(330,118,145,40),"3 原料区域",b))View(2);
        if(GUI.Button(new Rect(485,118,145,40),"4 成品货架",b))View(3);
    }
}


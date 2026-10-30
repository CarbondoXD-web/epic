using UnityEngine;
using UnityEngine.InputSystem;

// Optional continuous machining and linear rail preview.
public class FactoryConveyorDemo : MonoBehaviour
{
    public RobotMachiningCycle cycle;
    public bool autoRun;
    float nextCycle;
    void Update()
    {
        if(!cycle)return;
        if(autoRun && !cycle.robot.IsBusy && !cycle.robot.IsDamaged && Time.time>=nextCycle)
        {
            cycle.StartCycle();nextCycle=Time.time+2;
        }
        if(cycle.robot.IsBusy)nextCycle=Time.time+2;
        if(Keyboard.current!=null)
        {
            if(Keyboard.current.digit1Key.wasPressedThisFrame)View(false);
            if(Keyboard.current.digit2Key.wasPressedThisFrame)View(true);
        }
    }
    public void View(bool top)
    {
        var camera=cycle.robot.viewCamera;
        camera.GetComponent<RobotOrbitCamera>().enabled=!top;
        camera.orthographic=top;
        if(top)
        {
            camera.orthographicSize=3.5f;
            camera.transform.SetPositionAndRotation(new Vector3(-.30f,8,-.70f),Quaternion.Euler(90,0,0));
        }
    }
    void OnGUI()
    {
        var previous=GUI.matrix;
        GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,new Vector3(Screen.width/1280f,Screen.height/720f,1));
        GUI.Box(new Rect(20,20,450,115),"Linear rail / robot transfer / machining cell");
        autoRun=GUI.Toggle(new Rect(35,50,220,25),autoRun,"Continuous cycle preview");
        if(GUI.Button(new Rect(35,85,195,32),"1: Perspective"))View(false);
        if(GUI.Button(new Rect(245,85,195,32),"2: Top view"))View(true);
        GUI.matrix=previous;
    }
}

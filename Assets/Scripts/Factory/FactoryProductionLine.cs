using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class FactoryProductionLine : MonoBehaviour
{
    public FactoryMachineStation[] stations;
    public FactoryRobotExecutor mover;
    public FactoryFaultManager faults;
    public GameObject blankTemplate,shaftTemplate;
    public Transform partsRoot;
    public Transform[] outputSlots;
    public Vector3 feedStart,pickup;
    public bool directStockPickup;
    public Quaternion blankRotation,outputRotation;
    public Transform[] treads;
    public bool Running,Paused,Finished,Failed;
    public bool faultsEnabled=true;
    public int Target=12,CreatedCount,DeliveredCount;
    public string Task="准备就绪：开始订单，生产 12 件阶梯轴";
    public float Elapsed,Downtime;
    public int MaxParallelMachines;
    public List<FactoryWorkpiece> parts=new List<FactoryWorkpiece>();
    public float Score=>Finished?60+30*(faults.enabledForOrder?faults.firstCorrect/3f:1)+10*Mathf.Clamp01(1-Downtime/Mathf.Max(1,Elapsed*8)):0;
    AudioSource audioSource;
    AudioClip tone;
    GameObject[] stock;
    Vector3[] treadStarts;
    void Awake()
    {
        Application.targetFrameRate=60;QualitySettings.vSyncCount=0;
        foreach(var station in stations)station.line=this;
        faults.ResetFaults(faultsEnabled);
        var availableStock=GameObject.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t=>t.name=="库存原料");
        // Clear the column nearest the rail before reaching over it to the next column.
        stock=(directStockPickup?availableStock.OrderByDescending(t=>Mathf.RoundToInt(t.position.x*1000)).ThenBy(t=>t.position.z):availableStock.OrderBy(t=>t.position.y).ThenBy(t=>t.position.x)).Select(t=>t.gameObject).ToArray();
        treadStarts=treads.Select(t=>t.position).ToArray();
        audioSource=gameObject.AddComponent<AudioSource>();audioSource.volume=.12f;
        tone=AudioClip.Create("设备提示",4410,1,44100,false);
        var data=new float[4410];for(int i=0;i<data.Length;i++)data[i]=Mathf.Sin(i*880*2*Mathf.PI/44100)*(1-i/(float)data.Length)*.3f;tone.SetData(data,0);
    }
    void Update()
    {
        if(Running && !Paused)
        {
            Elapsed+=Time.deltaTime;
            int unavailable=stations.Count(s=>s.HasFault)+(mover.robot.IsDamaged || faults.Blocked(FactoryFaultKind.GripConfirmation)?1:0)+(faults.Blocked(FactoryFaultKind.FeedJam)?1:0);
            Downtime+=Time.deltaTime*unavailable;
            MaxParallelMachines=Mathf.Max(MaxParallelMachines,stations.Count(s=>s.State==FactoryStationState.Processing));
        }
        foreach(var station in stations)station.RefreshLamp();
    }
    public void StartOrder()
    {
        if(Running || Finished || Failed)return;
        Running=true;faults.ResetFaults(faultsEnabled);StartCoroutine(Schedule());
    }
    public void SetPaused(bool pause){if(!Running)return;Paused=pause;Time.timeScale=pause?0:1;if(pause)GetComponent<FactoryMachiningAudio>()?.Silence();}
    public void Restart()
    {
        Time.timeScale=1;
#if UNITY_EDITOR
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(gameObject.scene.path,new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
#else
        UnityEngine.SceneManagement.SceneManager.LoadScene(gameObject.scene.name);
#endif
    }
    public void Halt(string error){Failed=true;Running=false;Task="安全停止："+error;Time.timeScale=0;GetComponent<FactoryMachiningAudio>()?.Silence();Debug.LogError("[Factory] "+Task);}
    public FactoryWorkpiece CreatePart()
    {
        var go=new GameObject("工件 "+(++CreatedCount).ToString("D3"));go.transform.SetParent(partsRoot,false);
        var sourceStock=stock!=null && CreatedCount<=stock.Length?stock[CreatedCount-1]:null;
        if(stock!=null && CreatedCount<=stock.Length)stock[CreatedCount-1].SetActive(false);
        go.transform.SetPositionAndRotation(feedStart,blankRotation);
        if(directStockPickup && sourceStock)go.transform.SetPositionAndRotation(sourceStock.transform.position,sourceStock.transform.rotation);
        var part=go.AddComponent<FactoryWorkpiece>();part.Id="EP-"+CreatedCount.ToString("D3");part.State=FactoryPartState.Supply;
        part.blank=Instantiate(blankTemplate,go.transform);part.blank.SetActive(true);part.blank.transform.localPosition=Vector3.zero;part.blank.transform.localRotation=Quaternion.identity;
        part.shaft=Instantiate(shaftTemplate,go.transform);part.shaft.transform.localPosition=Vector3.zero;part.shaft.transform.rotation=outputRotation;part.shaft.SetActive(false);
        foreach(var body in go.GetComponentsInChildren<Rigidbody>(true)){body.isKinematic=true;Destroy(body);}
        foreach(var collider in go.GetComponentsInChildren<Collider>(true))Destroy(collider);
        parts.Add(part);return part;
    }
    IEnumerator Deliver(FactoryWorkpiece part)
    {
        if(directStockPickup){Task="机械臂直接前往原料架取料 "+part.Id;yield break;}
        Task="原料 "+part.Id+" 正在送料";
        Vector3 start=feedStart;
        FactoryAlarm alarm=null;
        for(float t=0;t<2.8f;)
        {
            if(t>=1 && alarm==null)alarm=faults.Trigger(FactoryFaultKind.FeedJam,CreatedCount);
            if(alarm!=null && alarm.active){Task="送料暂停：请处理输送带卡料";yield return null;continue;}
            t+=WorkflowTiming.DeltaTime;part.transform.position=Vector3.Lerp(start,pickup,Mathf.SmoothStep(0,1,t/2.8f));
            for(int i=0;i<treads.Length;i++)if(treads[i])
            {
                float distance=Mathf.Repeat(t*.9f+Vector3.Dot(treadStarts[i]-feedStart,Vector3.forward),2.95f);
                treads[i].position=new Vector3(treadStarts[i].x,treadStarts[i].y,feedStart.z+distance);
            }
            yield return null;
        }
        part.transform.position=pickup;
        for(float t=0;t<.15f;t+=WorkflowTiming.DeltaTime)yield return null;
    }
    IEnumerator Schedule()
    {
        while(DeliveredCount<Target && !Failed)
        {
            if(Paused || mover.robot.IsDamaged){yield return null;continue;}
            var ready=stations.Where(s=>s.State==FactoryStationState.Ready && !s.HasFault).OrderBy(s=>s.waitSince).ThenBy(s=>Mathf.Abs(s.railPosition-mover.RailPosition)).FirstOrDefault();
            if(ready)
            {
                Task="取出机床 "+(ready.id+1)+" 的成品";
                yield return mover.Unload(ready,outputSlots[DeliveredCount]);
                if(Failed)yield break;
                ready.CompletedCount++;DeliveredCount++;audioSource.PlayOneShot(tone);
                Debug.Log("[Factory] OUTPUT "+DeliveredCount+" station="+ready.id);
                continue;
            }
            var idle=stations.Where(s=>s.State==FactoryStationState.Idle && s.StartedCount<2 && !s.HasFault).OrderBy(s=>s.waitSince).ThenBy(s=>Mathf.Abs(s.railPosition-mover.RailPosition)).FirstOrDefault();
            if(idle && CreatedCount<Target)
            {
                idle.State=FactoryStationState.Loading;
                var part=CreatePart();yield return Deliver(part);
                yield return mover.Load(idle,part);
                if(Failed)yield break;
                idle.BeginProcessing();
            }
            else yield return null;
        }
        if(Failed)yield break;
        Running=false;Finished=true;Task="订单完成：12 件合格阶梯轴已入库";audioSource.PlayOneShot(tone);
        Debug.Log("[Factory] ORDER COMPLETE count="+DeliveredCount+" parallel="+MaxParallelMachines+" score="+Score);
    }
    void OnDestroy(){Time.timeScale=1;if(tone)Destroy(tone);}
}

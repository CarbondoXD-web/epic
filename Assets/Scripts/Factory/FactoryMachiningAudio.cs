using UnityEngine;

// One loop per running spindle; normalize the mix when several machines run.
public class FactoryMachiningAudio : MonoBehaviour
{
    public FactoryProductionLine line;
    public AudioClip machiningLoop;
    [Range(0,1)] public float volume=.14f;
    AudioSource[] sources;
    public int AudibleMachines { get; private set; }
    public float MixVolume { get; private set; }
    void Start()
    {
        sources=new AudioSource[line.stations.Length];
        for(int i=0;i<sources.Length;i++)
        {
            var station=line.stations[i];
            var emitter=new GameObject("机床加工声");
            emitter.transform.SetParent(station.machine.toolHead?station.machine.toolHead:station.machine.transform,false);
            var source=emitter.AddComponent<AudioSource>();
            source.clip=machiningLoop;source.loop=true;source.playOnAwake=false;
            source.spatialBlend=1f;source.minDistance=2;source.maxDistance=32;
            source.rolloffMode=AudioRolloffMode.Custom;source.spread=60;
            source.SetCustomCurve(AudioSourceCurveType.CustomRolloff,new AnimationCurve(
                new Keyframe(0,1),new Keyframe(.1f,.8f),new Keyframe(.3f,.4f),
                new Keyframe(.6f,.12f),new Keyframe(1,0)));
            source.dopplerLevel=0;source.volume=0;sources[i]=source;
        }
    }
    public void Silence()
    {
        AudibleMachines=0;MixVolume=0;
        if(sources==null)return;
        foreach(var source in sources){source.volume=0;source.Pause();}
    }
    void Update()
    {
        if(sources==null)return;
        bool stopped=line.Paused||line.Failed||!line.Running||Time.timeScale==0;
        int count=0;
        for(int i=0;i<sources.Length;i++)if(!stopped&&line.stations[i].State==FactoryStationState.Processing&&line.stations[i].machine.IsProcessing)count++;
        AudibleMachines=count;
        MixVolume=count>0?volume/Mathf.Sqrt(count):0;
        for(int i=0;i<sources.Length;i++)
        {
            var source=sources[i];bool active=!stopped&&line.stations[i].State==FactoryStationState.Processing&&line.stations[i].machine.IsProcessing;
            if(stopped){source.volume=0;source.Pause();continue;}
            if(active)
            {
                if(!source.isPlaying){source.UnPause();if(!source.isPlaying)source.Play();}
                source.pitch=Mathf.Clamp(Mathf.Sqrt(WorkflowTiming.PlaybackSpeed),.85f,1.2f);
            }
            source.volume=Mathf.MoveTowards(source.volume,active?MixVolume:0,Time.unscaledDeltaTime*.65f);
            if(!active&&source.volume<.0001f)source.Stop();
        }
    }
}

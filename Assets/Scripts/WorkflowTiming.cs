using UnityEngine;

// The player's speed applies to the existing production tempo only.
// Camera movement, click timing and pause remain independent.
public static class WorkflowTiming
{
    public const float SpeedMultiplier=1.3f;
    public static float PlaybackSpeed {get;private set;}=1f;
    public static float EffectiveSpeed=>SpeedMultiplier*PlaybackSpeed;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetPlaybackSpeed(){PlaybackSpeed=1f;}
    public static void SetPlaybackSpeed(float speed)
    {
        if(speed==.5f||speed==1f||speed==1.5f||speed==2f||speed==3f)PlaybackSpeed=speed;
    }
#if UNITY_EDITOR
    public static float ValidationStep;
    public static float DeltaTime=>Time.timeScale==0?0:(ValidationStep>0?ValidationStep:Time.deltaTime)*EffectiveSpeed;
#else
    public static float DeltaTime=>Time.deltaTime*EffectiveSpeed;
#endif
}

using UnityEngine;

// Applies an additional 30% speed increase to the machining workflow.
// Input click timing, camera controls and the global physics clock stay independent.
public static class WorkflowTiming
{
    public const float SpeedMultiplier = 1.3f;
    public static float DeltaTime => Time.deltaTime * SpeedMultiplier;
}
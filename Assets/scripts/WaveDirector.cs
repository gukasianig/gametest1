using System;
using UnityEngine;

public enum DirectorPhase { Build, Peak, Relief }
public enum DirectorEvent { None, Elite, Horde }

// Gives a run a rhythm: pressure rises (Build), holds at the full limit (Peak), then drops for a breather (Relief).
// It only scales the alive limit and spawn interval and picks an event for each peak; WaveManager does the spawning.
// The multiplier never exceeds 1, so the existing alive and absolute limits stay the real caps.
public class WaveDirector
{
    public float buildSeconds = 45f, peakSeconds = 25f, reliefSeconds = 15f;
    public float buildStartLimit = 0.6f, buildEndLimit = 0.9f, peakLimit = 1f, reliefLimit = 0.3f;
    public float peakInterval = 0.75f, reliefInterval = 1.8f;

    public DirectorPhase Phase { get; private set; }
    public int Cycle { get; private set; } = -1;
    public float PhaseProgress { get; private set; }
    public event Action<DirectorPhase, int> PhaseChanged;

    public float CycleSeconds { get { return buildSeconds + peakSeconds + reliefSeconds; } }

    public static DirectorEvent EventForCycle(int cycle)
    {
        if (cycle < 1) return DirectorEvent.None;
        return cycle % 2 == 1 ? DirectorEvent.Elite : DirectorEvent.Horde;
    }

    // Number of elites for the event of a cycle: grows slowly.
    public static int EliteCount(int cycle) { return Mathf.Clamp(1 + (cycle - 1) / 4, 1, 3); }
    public static int HordeCount(int cycle) { return Mathf.Clamp(8 + cycle * 2, 8, 18); }

    public void Evaluate(float elapsed)
    {
        float length = Mathf.Max(1f, CycleSeconds);
        elapsed = Mathf.Max(0f, elapsed);
        int cycle = Mathf.FloorToInt(elapsed / length);
        float t = elapsed - cycle * length;
        DirectorPhase phase;
        if (t < buildSeconds) { phase = DirectorPhase.Build; PhaseProgress = t / Mathf.Max(0.01f, buildSeconds); }
        else if (t < buildSeconds + peakSeconds) { phase = DirectorPhase.Peak; PhaseProgress = (t - buildSeconds) / Mathf.Max(0.01f, peakSeconds); }
        else { phase = DirectorPhase.Relief; PhaseProgress = (t - buildSeconds - peakSeconds) / Mathf.Max(0.01f, reliefSeconds); }
        if (phase == Phase && cycle == Cycle) return;
        Phase = phase;
        Cycle = cycle;
        if (PhaseChanged != null) PhaseChanged(phase, cycle);
    }

    public float LimitMultiplier
    {
        get
        {
            switch (Phase)
            {
                case DirectorPhase.Build: return Mathf.Lerp(buildStartLimit, buildEndLimit, PhaseProgress);
                case DirectorPhase.Peak: return peakLimit;
                default: return reliefLimit;
            }
        }
    }

    public float IntervalMultiplier
    {
        get { return Phase == DirectorPhase.Peak ? peakInterval : Phase == DirectorPhase.Relief ? reliefInterval : 1f; }
    }
}

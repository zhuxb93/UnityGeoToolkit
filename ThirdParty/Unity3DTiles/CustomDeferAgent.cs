using GLTFast;
using System.Threading.Tasks;
using UnityEngine;

public class CustomDeferAgent : IDeferAgent
{
    float frameTimeBudget = 0.02f;
    float lastTime;

    public CustomDeferAgent(float frameTimeBudget)
    {
        this.frameTimeBudget = frameTimeBudget;
        ResetLastTime();
    }

    public void Update()
    {
        ResetLastTime();
    }

    public async Task BreakPoint()
    {
        if (ShouldDefer())
        {
            await Task.Yield();
        }
    }

    public async Task BreakPoint(float duration)
    {
        if (ShouldDefer(duration))
        {
            await Task.Yield();
        }
    }

    public bool ShouldDefer()
    {
        return !FitInCurrentFrame(0);
    }

    public bool ShouldDefer(float duration)
    {
        return !FitInCurrentFrame(duration);
    }

    public void ResetLastTime()
    {
        lastTime = Time.realtimeSinceStartup;
    }

    private bool FitInCurrentFrame(float duration)
    {
        return duration <= frameTimeBudget - (Time.realtimeSinceStartup - lastTime);
    }
}

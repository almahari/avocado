namespace Avocado;

public enum MascotAnimationState { Still, Celebrating, Sleeping }

public static class MascotAnimationLogic
{
    public static MascotAnimationState GetState(bool enabled, bool quietHours, bool recentlyCompleted) =>
        !enabled ? MascotAnimationState.Still : quietHours ? MascotAnimationState.Sleeping :
        recentlyCompleted ? MascotAnimationState.Celebrating : MascotAnimationState.Still;
}

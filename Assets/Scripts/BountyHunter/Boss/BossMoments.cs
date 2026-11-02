using System;

namespace BountyHunter.Thieves
{
    // The moments of the boss's moves that can be heard (BossSfx plays them), raised by each move as
    // it happens. One per boss (BossController.Moments).
    public sealed class BossMoments
    {
        // It lighting a smoke bomb it has taken out to throw (not when warping away from a touch).
        public event Action SmokeLit;
        // Its smoke bomb bursting where it stands (thrown, or warping away from a touch).
        public event Action SmokeBurst;
        // It puffing up in its smoke somewhere (its entrance, or the far end of a warp).
        public event Action Appeared;
        // The water gun raised, and the water shot up out of it (the warning circles show).
        public event Action GunRaised;
        public event Action WaterFired;
        // The water coming down on the circles.
        public event Action WaterStruck;
        // It wrapping itself in its cape, passing for a passer-by, and throwing the cape off again.
        public event Action CapeWrapped;
        public event Action Disguised;
        public event Action CapeThrownOff;

        public void RaiseSmokeLit() => SmokeLit?.Invoke();

        public void RaiseSmokeBurst() => SmokeBurst?.Invoke();

        public void RaiseAppeared() => Appeared?.Invoke();

        public void RaiseGunRaised() => GunRaised?.Invoke();

        public void RaiseWaterFired() => WaterFired?.Invoke();

        public void RaiseWaterStruck() => WaterStruck?.Invoke();

        public void RaiseCapeWrapped() => CapeWrapped?.Invoke();

        public void RaiseDisguised() => Disguised?.Invoke();

        public void RaiseCapeThrownOff() => CapeThrownOff?.Invoke();
    }
}

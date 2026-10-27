namespace BountyHunter.Thieves
{
    // One thing the boss is doing (running about, shooting, a smoke bomb, a disguise, hiding, running
    // off). BossController starts one with its own start method and moves it on every frame until it
    // is over, then the boss runs about again.
    public interface IBossAction
    {
        // Moves it on a frame; false once it is over.
        bool Tick();

        // Whether a player can touch the boss now.
        bool Touchable { get; }

        // Whether a touch now catches it (false while it passes for a passer-by: the touch only
        // makes it warp away).
        bool CountsTouches { get; }

        // Cut short (a warp, the end of the chase): puts back whatever it took.
        void Stop();
    }
}

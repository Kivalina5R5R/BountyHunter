namespace BountyHunter.Thieves
{
    // Takes a thief a player has reached instead of it being caught outright (PoliceCuffs).
    public interface IThiefCuffs
    {
        // A player within reach of the thief with a hand free cuffs it; false when none has (it
        // slips past).
        bool TryCuff(ThiefController thief, float reach);
    }
}

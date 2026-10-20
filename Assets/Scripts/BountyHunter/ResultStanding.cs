namespace BountyHunter.Thieves
{
    // One character's place in the final standings: who it is and the bounty they ended with.
    public readonly struct ResultStanding
    {
        public ResultStanding(string character, int score)
        {
            Character = character;
            Score = score;
        }

        public string Character { get; }
        public int Score { get; }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // Clears the stage's wall for a boss phase and puts it back after: the stage's wall pieces are put
    // away (the board, the posters, the dogs' pictures; the clock and the score tags stay), parts that
    // must stay but go quiet are switched off (a dog's own picture, whose score tag is a child of it),
    // and with the dogs' pictures away the score tags stand in one straight row, level with the
    // highest of them (each keeps its place across), each with its character's face by the name.
    // Only what was showing is put away, so PutBack restores exactly that.
    public sealed class BossStaging
    {
        private readonly GameObject[] hide;
        private readonly Behaviour[] switchOff;
        private readonly PlayerScoreTag[] scoreTags;
        private readonly List<GameObject> putAway = new List<GameObject>();
        private readonly List<Behaviour> switchedOff = new List<Behaviour>();
        // The score tags lined up, with where each stood before.
        private readonly List<(PlayerScoreTag Tag, Vector3 Was)> linedUp = new List<(PlayerScoreTag Tag, Vector3 Was)>();

        // Any may be null or empty.
        public BossStaging(GameObject[] hide, Behaviour[] switchOff, PlayerScoreTag[] scoreTags)
        {
            this.hide = hide;
            this.switchOff = switchOff;
            this.scoreTags = scoreTags;
        }

        public void Show()
        {
            putAway.Clear();
            if (hide != null)
            {
                foreach (GameObject piece in hide)
                {
                    if (piece == null || !piece.activeSelf) continue;
                    piece.SetActive(false);
                    putAway.Add(piece);
                }
            }

            switchedOff.Clear();
            if (switchOff != null)
            {
                foreach (Behaviour part in switchOff)
                {
                    if (part == null || !part.enabled) continue;
                    part.enabled = false;
                    switchedOff.Add(part);
                }
            }

            LineUpScoreTags();
        }

        public void PutBack()
        {
            foreach (GameObject piece in putAway)
            {
                if (piece != null) piece.SetActive(true);
            }

            putAway.Clear();
            foreach (Behaviour part in switchedOff)
            {
                if (part != null) part.enabled = true;
            }

            switchedOff.Clear();
            foreach ((PlayerScoreTag tag, Vector3 was) in linedUp)
            {
                if (tag == null) continue;
                tag.transform.position = was;
                tag.ShowFace(false);
            }

            linedUp.Clear();
        }

        private void LineUpScoreTags()
        {
            linedUp.Clear();
            if (scoreTags == null) return;

            float top = float.NegativeInfinity;
            foreach (PlayerScoreTag tag in scoreTags)
            {
                if (tag != null && tag.InPlay) top = Mathf.Max(top, tag.transform.position.y);
            }

            foreach (PlayerScoreTag tag in scoreTags)
            {
                if (tag == null || !tag.InPlay) continue;
                Transform place = tag.transform;
                linedUp.Add((tag, place.position));
                place.position = new Vector3(place.position.x, top, place.position.z);
                tag.ShowFace(true);
            }
        }
    }
}

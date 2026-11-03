using System.Collections.Generic;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // Clears the stage's wall for a boss phase and puts it back after: the stage's wall pieces are put
    // away (the board, the posters, the dogs' pictures; the clock and the score tags stay), parts that
    // must stay but go quiet are switched off (a dog's own picture, whose score tag is a child of it),
    // and with the dogs' pictures away the score tags move to their spots on the Boss Wall
    // (BossScoreTagSpots), taking each spot's place and size and its spot for the leader's star,
    // each with its character's face by the name. A tag with no spot stays across where it is, level with the highest tag.
    // Only what was showing is put away, so PutBack restores exactly that.
    public sealed class BossStaging
    {
        private readonly GameObject[] hide;
        private readonly Behaviour[] switchOff;
        private readonly PlayerScoreTag[] scoreTags;
        private readonly BossScoreTagSpots tagSpots;
        private readonly List<GameObject> putAway = new List<GameObject>();
        private readonly List<Behaviour> switchedOff = new List<Behaviour>();
        // The score tags lined up, with where each stood before and its size there.
        private readonly List<(PlayerScoreTag Tag, Vector3 Was, Vector3 Size)> linedUp = new List<(PlayerScoreTag Tag, Vector3 Was, Vector3 Size)>();

        // Any may be null or empty.
        public BossStaging(GameObject[] hide, Behaviour[] switchOff, PlayerScoreTag[] scoreTags, BossScoreTagSpots tagSpots)
        {
            this.hide = hide;
            this.switchOff = switchOff;
            this.scoreTags = scoreTags;
            this.tagSpots = tagSpots;
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
            foreach ((PlayerScoreTag tag, Vector3 was, Vector3 size) in linedUp)
            {
                if (tag == null) continue;
                tag.transform.position = was;
                tag.transform.localScale = size;
                tag.PlaceBadgeOn(null);
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

            for (int i = 0; i < scoreTags.Length; i++)
            {
                PlayerScoreTag tag = scoreTags[i];
                if (tag == null || !tag.InPlay) continue;
                Transform place = tag.transform;
                linedUp.Add((tag, place.position, place.localScale));
                if (tagSpots != null && tagSpots.TryGetSpot(i, out RectTransform spot, out RectTransform star))
                {
                    StandOn(place, spot);
                    tag.PlaceBadgeOn(star);
                }
                else
                {
                    place.position = new Vector3(place.position.x, top, place.position.z);
                }

                tag.ShowFace(true);
            }
        }

        // The tag takes the spot's place and its size on the wall.
        private static void StandOn(Transform place, RectTransform spot)
        {
            place.position = spot.TransformPoint(spot.rect.center);
            float size = place.lossyScale.x;
            if (size > 0.000001f) place.localScale *= spot.lossyScale.x / size;
        }
    }
}

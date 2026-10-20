using System;
using Fortal.Display;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BountyHunter.Thieves
{
    // Switches between maps with the N key: each map has its own floor painting, hiding spot art
    // (bushes, bins, ...) and the bits thrown out when a thief dives in (leaves, trash). A switch
    // lays out new hiding spots and starts the next stage from round 1 (BountyRounds). The first map is applied before anything spawns, so it decides what Play starts with.
    // N on the last map, once the game is under way, ends the game and brings up the result screen.
    [DefaultExecutionOrder(-100)]
    public sealed class MapSwitcher : MonoBehaviour
    {
        [Serializable]
        private sealed class MapTheme
        {
            [SerializeField] private string name = "Map";
            [SerializeField] private Texture2D floor;
            [SerializeField] private Sprite[] hidingSpots;
            // Random turn of each hiding spot either way: 180 for top-down bushes, small for upright bins.
            [SerializeField, Range(0f, 180f)] private float maxTiltDegrees = 180f;
            // Size as a multiple of the player width on the art's narrowest side, picked in this range.
            [SerializeField] private Vector2 sizeVsPlayer = new Vector2(1.15f, 2.3f);
            // Thrown out when a thief dives in or runs out; leave empty for none.
            [SerializeField] private Sprite[] burstSprites;

            public string Name => name;
            public Texture2D Floor => floor;
            public Sprite[] HidingSpots => hidingSpots;
            public float MaxTiltDegrees => maxTiltDegrees;
            public Vector2 SizeVsPlayer => sizeVsPlayer;
            public Sprite[] BurstSprites => burstSprites;
        }

        [SerializeField] private RawImage floorImage;
        [SerializeField] private BushScatter hidingSpots;
        [SerializeField] private ThiefSpawner thieves;
        // Puts up the posters, spawns their thieves and restarts the timer for every new stage.
        [SerializeField] private BountyRounds rounds;
        // Ends the game when N is pressed on the last map.
        [SerializeField] private ResultSequence result;
        [SerializeField] private MapTheme[] maps;

        private int current;

        private void Awake()
        {
            if (maps == null || maps.Length == 0) return;
            Apply(maps[0]);
        }

        private void Update()
        {
            if (Keyboard.current == null || !Keyboard.current.nKey.wasPressedThisFrame) return;
            if (maps == null || maps.Length < 2) return;
            if (result != null && result.Ended) return;
            if (current == maps.Length - 1 && result != null && result.TryEndGame()) return;

            current = (current + 1) % maps.Length;
            SwitchTo(maps[current]);
        }

        private void SwitchTo(MapTheme map)
        {
            if (thieves != null) thieves.RemoveThieves();
            Apply(map);
            if (hidingSpots != null) hidingSpots.Scatter();
            if (rounds != null) rounds.StartStage(current + 1);
            Debug.Log($"[MapSwitcher] Switched to {map.Name}", this);
        }

        private void Apply(MapTheme map)
        {
            if (floorImage != null && map.Floor != null)
            {
                floorImage.texture = map.Floor;
                AspectRatioFitter fitter = floorImage.GetComponent<AspectRatioFitter>();
                if (fitter != null) fitter.aspectRatio = (float)map.Floor.width / map.Floor.height;
            }

            if (hidingSpots != null) hidingSpots.UseSprites(map.HidingSpots, map.MaxTiltDegrees, map.SizeVsPlayer);
            if (thieves != null) thieves.UseBurstSprites(map.BurstSprites);
        }
    }
}

using System;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // The parts of the game with their own mechanics, taught in the rest before each. The numbers are
    // saved in the scene, so they never change (0 was the How to play page, now the HOW TO PLAY board).
    public enum TutorialPart
    {
        Stage1 = 1,
        Stage2 = 2,
        Stage3 = 3,
        BossVault = 4,
        BossChase = 5,
    }

    // One rule on the wall: an optional item icon and its line in Thai and in English (one shows at
    // a time). Thai has no spaces between words, so the Thai lines carry zero-width spaces (U+200B)
    // where a line may break; without them a long Thai phrase breaks in the middle of a word.
    [Serializable]
    public sealed class TutorialLine
    {
        [SerializeField] private Sprite icon;
        [SerializeField] private string thai;
        [SerializeField] private string english;

        public TutorialLine(string thai, string english)
        {
            this.thai = thai;
            this.english = english;
        }

        public Sprite Icon => icon;
        public string Thai => thai;
        public string English => english;
    }

    // What one part of the game teaches: its heading (Thai and English) and its new mechanics only,
    // shown in the rest before it.
    [Serializable]
    public sealed class TutorialPage
    {
        [SerializeField] private TutorialPart part;
        [SerializeField] private string thaiHeading;
        [SerializeField] private string englishHeading;
        [SerializeField] private TutorialLine[] lines;

        public TutorialPage(TutorialPart part, string thaiHeading, string englishHeading, params TutorialLine[] lines)
        {
            this.part = part;
            this.thaiHeading = thaiHeading;
            this.englishHeading = englishHeading;
            this.lines = lines;
        }

        public TutorialPart Part => part;
        public string ThaiHeading => thaiHeading;
        public string EnglishHeading => englishHeading;
        public TutorialLine[] Lines => lines;

        // The design team's wording (Balance Note v2, C1), used until the scene sets its own.
        public static TutorialPage[] Defaults()
        {
            return new[]
            {
                new TutorialPage(TutorialPart.Stage1, "ด่าน 1: ถนน", "STAGE 1: STREET",
                    new TutorialLine("แตะโจร​เพื่อ​รับเงิน", "Touch a thief to claim the bounty"),
                    new TutorialLine("ยืนบน​ที่ซ่อน 2 วินาที เพื่อ​ไล่โจร​ออก", "Stand on a hiding spot for 2 seconds to flush a thief out"),
                    new TutorialLine("ระวัง​ชน​ประชาชน​และ​ระเบิด", "Watch out for civilians and bombs")),
                new TutorialPage(TutorialPart.Stage2, "ด่าน 2: สวน", "STAGE 2: PARK",
                    new TutorialLine("เข้าใกล้​โจร แล้ว​ปืน​จะ​ยิงเอง", "Get close to a thief and your stun gun fires by itself"),
                    new TutorialLine("โคลน​ทำให้​โจร​ช้าลง แต่​ปืน​ก็​ล็อก​ช้าลง", "Mud slows thieves, but it also slows your stun gun lock"),
                    new TutorialLine("ยืนที่​กล่องเทป เพื่อ​ปิด​ที่ซ่อน", "Stand on the tape box to seal hiding spots")),
                new TutorialPage(TutorialPart.Stage3, "ด่าน 3: ตลาด", "STAGE 3: MARKET",
                    new TutorialLine("แตะโจร แล้ว​พาไป​ส่งที่​รถตำรวจ", "Touch a thief, then bring them to a police car"),
                    new TutorialLine("ส่ง 2 ตัว​พร้อมกัน ได้เงิน x1.5", "Deliver 2 at once for x1.5 money"),
                    new TutorialLine("ยืนที่​กล่อง​กลางสนาม เพื่อ​หมุน​วงล้อ", "Stand on the center box to spin the wheel")),
                new TutorialPage(TutorialPart.BossVault, "บอส ช่วง 1: ห้องนิรภัย", "BOSS PHASE 1: VAULT",
                    new TutorialLine("กัน​บอส​ไม่ให้​ยืน​บน​เพชร", "Keep the boss off the diamond"),
                    new TutorialLine("ไฟดับ​แล้ว ไป​ยืนที่​สวิตช์", "When the lights go out, stand on a switch")),
                new TutorialPage(TutorialPart.BossChase, "บอส ช่วง 2: ไล่ล่า", "BOSS PHASE 2: CHASE",
                    new TutorialLine("แตะ​บอส​ตัวจริง​ให้​ครบ 3 ครั้ง", "Touch the real boss 3 times"),
                    new TutorialLine("หลบ​วงน้ำ​ของ​บอส", "Dodge the boss's water circles")),
            };
        }

        // The page of stage (1 first); the last stage's page for any stage past it.
        public static TutorialPart StagePart(int stage)
        {
            switch (stage)
            {
                case 1: return TutorialPart.Stage1;
                case 2: return TutorialPart.Stage2;
                default: return TutorialPart.Stage3;
            }
        }
    }
}

# Fortal Arena Remote: ให้หน้าเว็บ Launcher เปิด Tutorial ในเกมได้

ชุดนี้ทำให้ปุ่ม **Tutorial** และ **TH/EN** บนหน้าเว็บ Fortal Launcher สั่งเกมของคุณได้ ส่วนการเปิด/ปิดตัวเกมไม่ต้องใช้ชุดนี้ แค่เพิ่มเกมในแอป Game Launcher ก็พอ

```
หน้าเว็บ Launcher  --UDP-->  Game Launcher (เครื่องเกม)  --127.0.0.1:19790-->  เกม (สคริปต์นี้)
หน้าเว็บ Launcher  <--UDP--  Game Launcher               <--127.0.0.1:19780--  เกมรายงานสถานะ
```

ไม่ต้องตั้งค่า IP หรือพอร์ตในเกม ไม่ต้องติดตั้ง package เพิ่ม และไม่ต้องเรียกใช้สคริปต์หลักเอง มันเริ่มทำงานพร้อมเกม

## ติดตั้ง
1. คัดลอกโฟลเดอร์ `FortalArenaRemote` ไปไว้ใน `Assets/` ของโปรเจกต์
2. ในฉากที่มี Tutorial สร้าง GameObject แล้ว Add Component **Fortal > Arena Remote Bridge**
3. ตั้งค่าตามหัวข้อด้านล่าง แล้วลองด้วย Tester

## ตั้งค่าแบบไม่ต้องเขียนโค้ด
| ช่อง | ใส่อะไร |
|---|---|
| **Tutorial Root** | Object ของหน้า Tutorial เปิด = SetActive(true), ปิด = SetActive(false) |
| **Allowed While Active** | หน้าเมนูของเกม Tutorial จะเปิดได้เฉพาะตอน Object นี้เปิดอยู่ ถ้าเว้นว่าง จะเปิดได้ตลอด รวมถึงกลางเกม |

**ถ้า Tutorial ไม่ใช่แค่เปิด/ปิด Object:** เว้น Tutorial Root ไว้ แล้วผูกฟังก์ชันของเกมใน `On Open Tutorial` และ `On Close Tutorial` แบบนี้ต้องเรียก `MarkTutorialClosed()` เมื่อ Tutorial จบเอง (เช่นกด Skip หรือถึงหน้าสุดท้าย) ไม่งั้นหน้าเว็บจะขึ้นว่ายังเปิดอยู่ และถ้าเกมเปิด Tutorial เอง ให้เรียก `MarkTutorialOpen()` ด้วย

**ถ้าเกมมีภาษาไทยและอังกฤษ:** ติ๊ก `Supports Language` ตั้ง `Current Language` เป็น `th` หรือ `en` แล้วผูก `On Thai` กับ `On English` ถ้าเกมมีปุ่มเปลี่ยนภาษาเอง ให้ปุ่มนั้นเรียก `MarkLanguage("th")` หรือ `MarkLanguage("en")` ด้วย ถ้าเกมมีภาษาเดียว ไม่ต้องตั้งอะไร

ผลลัพธ์บนหน้าเว็บ: ปุ่ม TH/EN ของเว็บจะสั่งเปลี่ยนภาษาในเกมทันที และตอนเปิดเกมใหม่ เกมจะได้ภาษาที่เว็บเลือกอยู่เอง ถ้าเกมมีปุ่มเปลี่ยนภาษาเองแล้วเรียก `MarkLanguage` หน้าเว็บจะเปลี่ยนตามเกม

## ตั้งค่าด้วยโค้ด (แทน Bridge)
```csharp
using Fortal.Arena;

void Awake()
{
    FortalArenaRemote.IsTutorialOpen  = () => tutorial.IsOpen;
    FortalArenaRemote.CanOpenTutorial = () => gameState == GameState.Menu;
    FortalArenaRemote.OpenTutorial    = tutorial.Open;
    FortalArenaRemote.CloseTutorial   = tutorial.Close;

    // เฉพาะเกมที่มี 2 ภาษา
    FortalArenaRemote.GetLanguage = () => Language.Current;   // "th" หรือ "en"
    FortalArenaRemote.SetLanguage = lang => Language.Set(lang);
}
```
ทุกฟังก์ชันถูกเรียกบน main thread ใช้ API ของ Unity ได้ตามปกติ

## ทดสอบ
**ใน Editor ไม่ต้องลงอะไรเพิ่ม:** เปิด **Tools > Fortal > Arena Remote Tester** กด Play แล้วกดปุ่ม ถ้า Game Launcher เปิดอยู่บนเครื่องเดียวกัน ให้ปิดก่อน เพราะมันใช้พอร์ต 19780

ต้องได้ผลตามนี้:
- กด Open: Tutorial เปิด และ `"tutorial":true` ภายใน 1 วินาที
- กด Open ซ้ำ: Tutorial ยังเปิดอยู่ ไม่ปิดเอง
- กด Close: Tutorial ปิด และ `"tutorial":false`
- ระหว่างเล่น (ไม่ใช่หน้าเมนู): `"tutorialAllowed":false` และกด Open แล้วไม่เกิดอะไร
- ปล่อยไว้เฉย ๆ: รายงานมาเรื่อย ๆ ทุก 2 วินาที

**ทดสอบจริง:** build เกม เพิ่มในแอป Game Launcher เปิดเกมจากหน้าเว็บ แล้วกดปุ่ม Tutorial

## ข้อกำหนดของ build
- **ชื่อ exe ต้องไม่ซ้ำกับเกมอื่น:** Game Launcher เช็กและปิดเกมจากชื่อ process
- **เกมอาจถูกปิดทันที (`taskkill /F`):** อย่าให้การบันทึกข้อมูลไปรอตอน `OnApplicationQuit`
- **`Run In Background`:** สคริปต์เปิดให้เองตอนเริ่มเกม ถ้าเกมมีโค้ดตั้งค่านี้เป็น false ให้เอาออก ไม่งั้นเกมจะไม่รับคำสั่งตอนหน้าต่างไม่ได้อยู่หน้าสุด

## แก้ปัญหา
| อาการ | สาเหตุที่พบบ่อย |
|---|---|
| เว็บขึ้น "เกมไม่ตอบ" | build ยังไม่มีสคริปต์นี้ ดูใน `Player.log` ว่ามี `[FortalArenaRemote] listening` ไหม |
| Log ขึ้น `UDP 19790 is already taken` | **ชนพอร์ต** ดูหัวข้อถัดไป หรือเปิดเกมซ้อนกันสองตัว |
| ปุ่ม Tutorial กดไม่ได้ | เกมรายงาน `tutorialAllowed:false` ตรวจ Allowed While Active |
| Tutorial จบแล้ว เว็บยังขึ้น "ปิด Tutorial" | ยังไม่ได้เรียก `MarkTutorialClosed()` |
| Tester ขึ้น port 19780 is taken | ปิด Game Launcher บนเครื่องนั้นก่อน |

## ชนพอร์ต: เกมที่มีระบบฟัง 19790 อยู่แล้ว
เกมที่ใช้ `com.fortal.network`, `InviteToast` หรือระบบ dual lane อาจฟังพอร์ต 19790 อยู่แล้ว และพอร์ตนี้มีโปรแกรมฟังได้ทีละตัว ให้ปิดการฟังของชุดนี้ แล้วส่งข้อความจากระบบเดิมเข้ามาแทน:

```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
static void UseExistingListener() => FortalArenaRemote.ListenOnPort = false;

// ในจุดที่ระบบเดิมรับข้อความ (ต้องอยู่บน main thread)
FortalArenaRemote.HandleMessage(action, payload);   // เช่น "tutorial", "{\"open\":true}"
```

## ทดสอบกับอะไรมาแล้ว
ทดสอบบน Unity 6000.3.8f1, Windows, โปรเจกต์เปล่าที่มีแค่ชุดนี้ build เป็นเกมแล้วสั่งผ่าน Game Launcher ตัวจริง ทั้งเกม 2 ภาษาและเกมภาษาเดียว ยังไม่ได้ลองกับ Unity รุ่นอื่น และยังไม่ได้ลองร่วมกับ `com.fortal.network` ถ้าเจอปัญหาให้แจ้งทีม Launcher พร้อมไฟล์ `Player.log`

## ข้อความที่ใช้ (สำหรับคนที่อยากรู้)
รูปแบบ: ตัวอักษร `RMOT` ตามด้วย `action` เว้นวรรค แล้วตามด้วย JSON

| ทิศทาง | ข้อความ |
|---|---|
| เข้าเกม `127.0.0.1:19790` | `RMOTtutorial {"open":true}` หรือ `{"open":false}` เป็นการตั้งสถานะ ไม่ใช่สลับ จึงส่งซ้ำได้ |
| เข้าเกม `127.0.0.1:19790` | `RMOTlanguage {"lang":"th"}` หรือ `"en"` |
| เข้าเกม `127.0.0.1:19790` | `RMOTarena_status` ขอให้รายงานทันที |
| ออกจากเกม `127.0.0.1:19780` | `RMOTgame_data {"type":"arena","data":{"scene":"Menu","tutorial":false,"tutorialAllowed":true,"lang":"th"}}` ไม่ต้องส่ง `lang` ถ้าเกมมีภาษาเดียว |

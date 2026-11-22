using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ChinaFishing
{
    [Serializable] public class InvEntry { public string id; public int count; }
    [Serializable] public class FishRecord { public string id; public int caught; public float maxWeight; public float maxLength; }

    [Serializable]
    public class SaveData
    {
        public int magic = Game.Magic;       // признак целого файла сохранения
        public int version = Game.SaveVersion;
        public int money = 300;
        public int level = 1;
        public int xp = 0;
        public float posX = 0, posY = 0, posZ = 0;
        public bool hasPos = false;
        public List<InvEntry> inventory = new List<InvEntry>();
        public List<string> owned = new List<string>();     // купленные снасти (не расходники)
        public List<FishRecord> collection = new List<FishRecord>();
        public string rod = "rod_start", reel = "reel_basic", line = "line_015", hook = "hook_s",
                      floatId = "float_basic", sinker = "sinker_s", rig = "rig_float", bait = "worm";
        public float volMaster = 0.8f, volMusic = 0.5f, volEffects = 0.8f;
        public int quality = 1; // 0 низкое, 1 среднее, 2 высокое
        public int totalCaught = 0;
        public int purchases = 0;
        public int casts = 0;           // всего забросов
        public int groundbaitLeft = 0;  // сколько забросов ещё действует прикормка
        public string groundbaitId = "";   // какая прикормка сейчас действует
        public string groundbaitSel = "";  // выбранная игроком прикормка
        public int[] skillCatches = new int[6]; // фидер, карп, пикер, спиннинг, поплавок, маркер
        public int crafted = 0;         // сколько раз изготавливал прикормку
    }

    public static class Game
    {
        public const int Magic = 20261122;
        public const int SaveVersion = 2;
        public static SaveData S;
        public static bool LoadedFromBackup;
        public static event Action Changed;

        static string Dir { get { return Application.persistentDataPath; } }
        static string MainPath { get { return Path.Combine(Dir, "china_fishing_save.json"); } }
        static string BakPath { get { return Path.Combine(Dir, "china_fishing_save.bak"); } }
        static string TmpPath { get { return Path.Combine(Dir, "china_fishing_save.tmp"); } }

        static readonly string[] Starter = { "rod_start", "reel_basic", "line_015", "hook_s", "float_basic", "sinker_s", "rig_float" };

        // ---------------- ЗАГРУЗКА ----------------
        static SaveData TryRead(string path, out bool existed)
        {
            existed = false;
            try
            {
                if (!File.Exists(path)) return null;
                existed = true;
                var txt = File.ReadAllText(path);
                if (string.IsNullOrEmpty(txt) || txt.Trim().Length < 10) return null;
                var d = JsonUtility.FromJson<SaveData>(txt);
                if (d == null || d.magic != Magic) return null;
                return d;
            }
            catch (Exception e) { Debug.LogWarning("Сохранение повреждено (" + path + "): " + e.Message); return null; }
        }

        static void KeepCorrupt(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Copy(path, Path.Combine(Dir, "china_fishing_save.corrupt_" + DateTime.UtcNow.Ticks + ".json"), false);
            }
            catch (Exception) { }
        }

        public static void Load()
        {
            if (S != null) return; // повторная загрузка не нужна
            bool exMain, exBak, exTmp;
            var d = TryRead(MainPath, out exMain);
            if (d == null && exMain) KeepCorrupt(MainPath);
            if (d == null)
            {
                d = TryRead(BakPath, out exBak);
                if (d != null) LoadedFromBackup = true;
                else if (exBak) KeepCorrupt(BakPath);
            }
            if (d == null)
            {
                d = TryRead(TmpPath, out exTmp);
                if (d != null) LoadedFromBackup = true;
            }
            bool fresh = d == null;
            S = d ?? new SaveData();
            Sanitize(S);
            if (fresh) GiveStarterItems(S);
            Save();
        }

        static void GiveStarterItems(SaveData s)
        {
            foreach (var id in Starter) if (!s.owned.Contains(id)) s.owned.Add(id);
            AddTo(s, "worm", 15); AddTo(s, "bread", 10);
        }

        static void AddTo(SaveData s, string id, int n)
        {
            foreach (var e in s.inventory) if (e.id == id) { e.count += n; return; }
            s.inventory.Add(new InvEntry { id = id, count = n });
        }

        // Приводит любые данные (в т. ч. из старых версий) к допустимому виду
        static void Sanitize(SaveData s)
        {
            if (s.inventory == null) s.inventory = new List<InvEntry>();
            if (s.owned == null) s.owned = new List<string>();
            if (s.collection == null) s.collection = new List<FishRecord>();
            s.money = Mathf.Max(0, s.money);
            s.level = Mathf.Clamp(s.level, 1, 99);
            s.xp = Mathf.Max(0, s.xp);
            s.quality = Mathf.Clamp(s.quality, 0, 2);
            s.volMaster = Clamp01(s.volMaster, 0.8f); s.volMusic = Clamp01(s.volMusic, 0.5f); s.volEffects = Clamp01(s.volEffects, 0.8f);
            s.totalCaught = Mathf.Max(0, s.totalCaught); s.purchases = Mathf.Max(0, s.purchases); s.casts = Mathf.Max(0, s.casts);
            s.groundbaitLeft = Mathf.Clamp(s.groundbaitLeft, 0, 50); s.crafted = Mathf.Max(0, s.crafted);
            if (s.skillCatches == null || s.skillCatches.Length != 6) s.skillCatches = new int[6];
            for (int k = 0; k < 6; k++) s.skillCatches[k] = Mathf.Max(0, s.skillCatches[k]);
            if (s.groundbaitId == null || GameData.GetItem(s.groundbaitId) == null) s.groundbaitId = "";
            if (s.groundbaitSel == null || GameData.GetItem(s.groundbaitSel) == null) s.groundbaitSel = "";
            if (!IsFinite(s.posX) || !IsFinite(s.posY) || !IsFinite(s.posZ) || Mathf.Abs(s.posX) > 400f || Mathf.Abs(s.posZ) > 400f) s.hasPos = false;

            // инвентарь: только известные предметы, без дублей и отрицательных значений
            var inv = new List<InvEntry>();
            foreach (var e in s.inventory)
            {
                if (e == null || string.IsNullOrEmpty(e.id) || GameData.GetItem(e.id) == null || e.count <= 0) continue;
                var ex = inv.Find(x => x.id == e.id);
                if (ex != null) ex.count += e.count; else inv.Add(new InvEntry { id = e.id, count = e.count });
            }
            s.inventory = inv;
            var own = new List<string>();
            foreach (var id in s.owned) if (!string.IsNullOrEmpty(id) && GameData.GetItem(id) != null && !own.Contains(id)) own.Add(id);
            foreach (var id in Starter) if (!own.Contains(id)) own.Add(id); // стартовые снасти нельзя потерять
            s.owned = own;

            var col = new List<FishRecord>();
            foreach (var r in s.collection)
            {
                if (r == null || GameData.GetFish(r.id) == null) continue;
                var ex = col.Find(x => x.id == r.id);
                if (ex != null) { ex.caught += Mathf.Max(0, r.caught); ex.maxWeight = Mathf.Max(ex.maxWeight, r.maxWeight); ex.maxLength = Mathf.Max(ex.maxLength, r.maxLength); }
                else col.Add(new FishRecord { id = r.id, caught = Mathf.Max(1, r.caught), maxWeight = Mathf.Max(0, r.maxWeight), maxLength = Mathf.Max(0, r.maxLength) });
            }
            s.collection = col;

            // надетое: должно существовать, быть своей категории и находиться в собственности
            s.rod = Valid(s, s.rod, ItemCategory.Удочки, "rod_start");
            s.reel = Valid(s, s.reel, ItemCategory.Катушки, "reel_basic");
            s.line = Valid(s, s.line, ItemCategory.Лески, "line_015");
            s.hook = Valid(s, s.hook, ItemCategory.Крючки, "hook_s");
            s.floatId = Valid(s, s.floatId, ItemCategory.Поплавки, "float_basic");
            s.sinker = Valid(s, s.sinker, ItemCategory.Грузила, "sinker_s");
            s.rig = Valid(s, s.rig, ItemCategory.Оснастки, "rig_float");
            var b = GameData.GetItem(s.bait);
            if (b == null || (b.cat != ItemCategory.Наживки && b.cat != ItemCategory.Приманки) || !OwnsIn(s, b)) s.bait = FirstBait(s);
            s.version = SaveVersion; s.magic = Magic;
        }

        static float Clamp01(float v, float def) { return float.IsNaN(v) || float.IsInfinity(v) ? def : Mathf.Clamp01(v); }
        static bool IsFinite(float v) { return !float.IsNaN(v) && !float.IsInfinity(v); }

        static bool OwnsIn(SaveData s, ItemDef d)
        {
            if (d.consumable) { foreach (var e in s.inventory) if (e.id == d.id && e.count > 0) return true; return false; }
            return s.owned.Contains(d.id);
        }

        static string Valid(SaveData s, string id, ItemCategory cat, string def)
        {
            var d = GameData.GetItem(id);
            return d != null && d.cat == cat && OwnsIn(s, d) ? id : def;
        }

        static string FirstBait(SaveData s)
        {
            foreach (var it in GameData.Items)
                if ((it.cat == ItemCategory.Наживки || it.cat == ItemCategory.Приманки) && OwnsIn(s, it)) return it.id;
            return "worm";
        }

        // ---------------- СОХРАНЕНИЕ (временный файл + резервная копия) ----------------
        public static void Save()
        {
            if (S == null) return;
            try
            {
                S.magic = Magic; S.version = SaveVersion;
                var json = JsonUtility.ToJson(S);
                File.WriteAllText(TmpPath, json);
                bool ex;
                if (TryRead(TmpPath, out ex) == null) { Debug.LogWarning("Сохранение не прошло проверку, файл не заменён."); return; }
                if (File.Exists(MainPath)) File.Copy(MainPath, BakPath, true);
                File.Copy(TmpPath, MainPath, true);
            }
            catch (Exception e) { Debug.LogWarning("Ошибка сохранения: " + e.Message); }
        }

        // Сброс прогресса: прежнее сохранение остаётся на устройстве копией
        public static void ResetProgress()
        {
            try { if (File.Exists(MainPath)) File.Copy(MainPath, Path.Combine(Dir, "china_fishing_save.before_reset_" + DateTime.UtcNow.Ticks + ".json"), false); }
            catch (Exception) { }
            int keepQ = S != null ? S.quality : 1;
            S = new SaveData { quality = keepQ };
            Sanitize(S); GiveStarterItems(S);
            Save(); Notify();
        }

        // для запуска в редакторе без перезагрузки домена: очистить статическое состояние
        public static void ResetStatics() { S = null; Changed = null; LoadedFromBackup = false; }

        public static void Notify() { if (Changed != null) Changed(); }

        // ---------------- ИНВЕНТАРЬ ----------------
        public static int Count(string id)
        {
            if (S == null) return 0;
            foreach (var e in S.inventory) if (e.id == id) return e.count;
            return 0;
        }

        public static void AddItem(string id, int n)
        {
            if (n <= 0 || GameData.GetItem(id) == null) return;
            foreach (var e in S.inventory) if (e.id == id) { e.count = (int)Math.Min((long)e.count + n, 9999L); return; }
            S.inventory.Add(new InvEntry { id = id, count = Math.Min(n, 9999) });
        }

        public static bool UseItem(string id, int n = 1)
        {
            if (n <= 0) return false;
            for (int i = 0; i < S.inventory.Count; i++)
            {
                var e = S.inventory[i];
                if (e.id != id) continue;
                if (e.count < n) return false;
                e.count -= n;
                if (e.count <= 0) S.inventory.RemoveAt(i);
                return true;
            }
            return false;
        }

        public static bool Owns(string id)
        {
            var d = GameData.GetItem(id);
            if (d == null || S == null) return false;
            return OwnsIn(S, d);
        }

        // ---------------- ПОКУПКИ ----------------
        // Возвращает текст ошибки или null, если покупка прошла
        public static string Buy(ItemDef d)
        {
            if (d == null) return "Предмет не найден";
            if (S.level < d.minLevel) return "Нужен уровень " + d.minLevel;
            if (!d.consumable && Owns(d.id)) return "Уже куплено";
            if (d.price < 0) return "Предмет недоступен";
            if (S.money < d.price) return "Не хватает монет";
            S.money -= d.price;
            if (S.money < 0) S.money = 0;
            if (d.consumable) AddItem(d.id, Mathf.Max(1, d.packSize));
            else { if (!S.owned.Contains(d.id)) S.owned.Add(d.id); Equip(d); }
            S.purchases++;
            Save(); Notify();
            return null;
        }


        // Выбранная прикормка (если её нет в запасе — первая попавшаяся из имеющихся)
        public static ItemDef SelectedGroundbait()
        {
            var d = GameData.GetItem(S.groundbaitSel);
            if (d != null && d.cat == ItemCategory.Прикормки && Count(d.id) > 0) return d;
            foreach (var it in GameData.Items) if (it.cat == ItemCategory.Прикормки && Count(it.id) > 0) return it;
            return null;
        }

        // Какой навык развивает текущая снасть: -1, если ни один
        public static int SkillIndexFor(string rigId, string baitId)
        {
            if (rigId == "rig_spin") return 3;
            if (rigId == "rig_float") return 4;
            if (rigId == "rig_bottom")
            {
                var b = GameData.GetItem(baitId);
                return b != null && (b.id == "boilie" || b.family == "boilie") ? 1 : 0;
            }
            return -1;
        }

        public static float SkillPct(int idx)
        {
            if (S == null || idx < 0 || idx >= S.skillCatches.Length) return 0f;
            return (S.skillCatches[idx] / 5) * GameData.SkillStepPct;
        }

        public static float CookPct() { return S == null ? 0f : (S.crafted / 6) * GameData.SkillStepPct; }

        // Изготовление по рецепту. Возвращает текст ошибки или null.
        public static string Craft(RecipeDef r)
        {
            if (r == null) return "Рецепт не найден";
            if (S.level < r.minLevel) return "Нужен уровень " + r.minLevel;
            for (int i = 0; i < r.ids.Length; i++)
                if (Count(r.ids[i]) < r.counts[i]) { var m = GameData.GetItem(r.ids[i]); return "Не хватает: " + (m != null ? m.name : r.ids[i]); }
            for (int i = 0; i < r.ids.Length; i++) UseItem(r.ids[i], r.counts[i]);
            AddItem(r.outId, r.outCount);
            S.crafted++;
            Save(); Notify();
            return null;
        }

        public static void Equip(ItemDef d)
        {
            if (d == null || !Owns(d.id)) return;
            switch (d.cat)
            {
                case ItemCategory.Удочки: S.rod = d.id; break;
                case ItemCategory.Катушки: S.reel = d.id; break;
                case ItemCategory.Лески: S.line = d.id; break;
                case ItemCategory.Крючки: S.hook = d.id; break;
                case ItemCategory.Поплавки: S.floatId = d.id; break;
                case ItemCategory.Грузила: S.sinker = d.id; break;
                case ItemCategory.Оснастки: S.rig = d.id; break;
                case ItemCategory.Наживки:
                case ItemCategory.Приманки: S.bait = d.id; break;
                case ItemCategory.Прикормки: S.groundbaitSel = d.id; break;
                default: return;
            }
            Save(); Notify();
        }

        public static bool IsEquipped(string id)
        {
            return S.rod == id || S.reel == id || S.line == id || S.hook == id || S.floatId == id ||
                   S.sinker == id || S.rig == id || S.bait == id || S.groundbaitSel == id;
        }

        // ---------------- ДЕНЬГИ, ОПЫТ, УЛОВ ----------------
        public static void AddMoney(int v)
        {
            if (v <= 0) return;
            S.money = (int)Math.Min((long)S.money + v, 2000000000L);
        }

        // true, если получен новый уровень (награда выдаётся здесь)
        public static bool AddXp(int xp)
        {
            if (xp <= 0) return false;
            S.xp += xp;
            bool up = false;
            int guard = 0;
            while (S.level < 99 && S.xp >= GameData.XpForLevel(S.level) && guard++ < 50)
            {
                S.xp -= GameData.XpForLevel(S.level);
                S.level++;
                AddMoney(100 * S.level);
                up = true;
            }
            if (S.level >= 99) S.xp = 0;
            return up;
        }

        public static FishRecord Record(string id)
        {
            foreach (var r in S.collection) if (r.id == id) return r;
            return null;
        }

        // Записывает улов в коллекцию. Возвращает true, если вид пойман впервые.
        public static bool RecordCatch(FishDef f, float weight, float length, out bool newRecord)
        {
            newRecord = false;
            var rec = Record(f.id);
            bool first = rec == null;
            if (first) { rec = new FishRecord { id = f.id }; S.collection.Add(rec); }
            rec.caught++;
            if (weight > rec.maxWeight) { newRecord = !first; rec.maxWeight = weight; }
            if (length > rec.maxLength) rec.maxLength = length;
            S.totalCaught++;
            int si = SkillIndexFor(S.rig, S.bait);
            if (si >= 0 && si < S.skillCatches.Length) S.skillCatches[si]++;   // считаются только пойманные рыбы
            return first;
        }

        public static int SellValue(string fishId, float weight)
        {
            var f = GameData.GetFish(fishId);
            if (f == null) return 0;
            return Mathf.Max(1, Mathf.RoundToInt(f.pricePerKg * weight));
        }

        // ---------------- КАЧЕСТВО ГРАФИКИ ----------------
        public static void ApplyQuality()
        {
            int q = Mathf.Clamp(S.quality, 0, 2);
            var rp = Resources.Load<UnityEngine.Rendering.RenderPipelineAsset>(q == 0 ? "URP_Low" : q == 1 ? "URP_Medium" : "URP_High");
            if (rp != null) QualitySettings.renderPipeline = rp;
            QualitySettings.vSyncCount = 0;
            QualitySettings.lodBias = q == 0 ? 0.6f : q == 1 ? 1f : 1.5f;
            QualitySettings.anisotropicFiltering = q == 2 ? AnisotropicFiltering.Enable : AnisotropicFiltering.Disable;
            Application.targetFrameRate = q == 0 ? 30 : q == 1 ? 45 : 60;
            var wb = WorldBuilder.I;
            if (wb != null && wb.terrain != null) wb.ApplyTerrainQuality();
            Notify();
        }

        public static float ViewDistance { get { return S == null ? 220f : S.quality == 0 ? 130f : S.quality == 1 ? 220f : 350f; } }
    }
}

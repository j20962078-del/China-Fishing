using System.Collections.Generic;
using UnityEngine;

namespace ChinaFishing
{
    public enum Rarity { Обычная, Необычная, Редкая, Легендарная }
    public enum ItemCategory { Удочки, Катушки, Лески, Крючки, Поплавки, Грузила, Оснастки, Наживки, Приманки, Прикормки, Материалы, Прочее }

    public class FishDef
    {
        public string id, name, desc, habitat;
        public float minW, maxW;          // кг
        public float minL, maxL;          // см
        public Rarity rarity;
        public float resistance;          // сила рыбы
        public float aggression;          // частота рывков в сторону
        public float depthMin, depthMax;  // предпочтительная глубина, м
        public string[] baits;            // id любимых наживок и приманок
        public int pricePerKg;
        public int xp;
        public Color color, belly;
        public Vector3 bodyScale;         // длина, высота, ширина модели
        public float spawnWeight;
    }

    public class ItemDef
    {
        public string id, name, desc, statText;
        public ItemCategory cat;
        public int price;
        public float power, stat2;
        public bool consumable;
        public int packSize = 1;
        public int minLevel = 1;
        public Color color = Color.gray;
        public string sub;      // подкатегория (секция в магазине), например «Черви». Если не задана — секция «Другое»
        public string maker;    // производитель; null — без производителя
        public string compat;   // текст совместимости для карточки товара
        public string[] targets; // для прикормки: id рыб, которых она привлекает особенно сильно
        public string family;   // семейство: рыба, любящая id семейства, клюёт и на этот товар (например «worm»)
    }

    // Навык: каждые perStep успехов даёт +0,3%
    public class SkillDef
    {
        public int index;            // индекс счётчика в SaveData.skillCatches; -1 — навык по числу изготовлений
        public string name, how, note;
        public int perStep;
    }

    public class RecipeDef
    {
        public string outId; public int outCount, minLevel;
        public string[] ids; public int[] counts;
    }

    // Секция магазина: необязательная группа (крупный заголовок) + название секции
    public class SectionDef
    {
        public string group, name;
        public SectionDef(string group, string name) { this.group = group; this.name = name; }
    }

    public static class GameData
    {
        public static readonly List<FishDef> Fish = new List<FishDef>();
        public static readonly List<ItemDef> Items = new List<ItemDef>();
        public static readonly List<RecipeDef> Recipes = new List<RecipeDef>();
        public const float SkillStepPct = 0.3f;
        public static readonly SkillDef[] Skills =
        {
            new SkillDef { index = 0, name = "Фидер", perStep = 5, how = "Каждые 5 пойманных рыб на донную снасть (не бойл) — +0,3%", note = "Бонус к клёву на донной оснастке." },
            new SkillDef { index = 1, name = "Карпфишинг", perStep = 5, how = "Каждые 5 пойманных рыб на донную снасть с бойлом/пеллетсом — +0,3%", note = "Бонус к клёву на карповой снасти." },
            new SkillDef { index = 2, name = "Пикер", perStep = 5, how = "Каждые 5 пойманных рыб на пикер — +0,3%", note = "Пикерных удилищ в игре пока нет — навык не растёт." },
            new SkillDef { index = 3, name = "Спиннинг", perStep = 5, how = "Каждые 5 пойманных рыб на спиннинг — +0,3%", note = "Бонус к клёву при ловле на приманки." },
            new SkillDef { index = 4, name = "Поплавочная ловля", perStep = 5, how = "Каждые 5 пойманных рыб на поплавочную снасть — +0,3%", note = "Бонус к клёву на поплавочной оснастке." },
            new SkillDef { index = 5, name = "Маркер", perStep = 5, how = "Развивается за исследование дна и глубины", note = "Маркерного удилища в игре пока нет — навык не растёт." },
            new SkillDef { index = -1, name = "Приготовление прикормки", perStep = 6, how = "Каждые 6 успешных замесов — +0,3%", note = "Усиливает действие прикормки." },
        };
        static readonly Dictionary<string, FishDef> fishMap = new Dictionary<string, FishDef>();
        static readonly Dictionary<string, ItemDef> itemMap = new Dictionary<string, ItemDef>();

        static Color C(int r, int g, int b) { return new Color(r / 255f, g / 255f, b / 255f); }

        static void F(string id, string name, string desc, string habitat, float minW, float maxW, float minL, float maxL,
            Rarity r, float res, float aggr, float d0, float d1, string[] baits, int pkg, int xp, Color c, Color belly, Vector3 sc, float spawn)
        {
            var f = new FishDef
            {
                id = id, name = name, desc = desc, habitat = habitat, minW = minW, maxW = maxW, minL = minL, maxL = maxL,
                rarity = r, resistance = res, aggression = aggr, depthMin = d0, depthMax = d1, baits = baits, pricePerKg = pkg, xp = xp,
                color = c, belly = belly, bodyScale = sc, spawnWeight = spawn
            };
            Fish.Add(f); fishMap[id] = f;
        }

        static void I(string id, string name, ItemCategory cat, int price, float power, float stat2, string statText, string desc, Color col,
            bool consumable = false, int pack = 1, int minLevel = 1, string sub = null, string maker = null, string compat = null)
        {
            var it = new ItemDef
            {
                id = id, name = name, cat = cat, price = price, power = power, stat2 = stat2, statText = statText, desc = desc,
                color = col, consumable = consumable, packSize = pack, minLevel = minLevel, sub = sub, maker = maker, compat = compat
            };
            Items.Add(it); itemMap[id] = it;
        }


        // Готовая прикормка (5 порций; каждая действует 5 забросов)
        static void G(string id, string name, string sub, int price, float power, string desc, Color col, int lvl, params string[] targets)
        {
            I(id, name, ItemCategory.Прикормки, price, power, 0f, "Поклёвки чаще на " + Mathf.RoundToInt((power - 1f) * 100f) + "% на 5 забросов", desc, col, true, 5, lvl, sub);
            Items[Items.Count - 1].targets = targets;
        }

        // Материал для крафта (упаковка 10 единиц)
        static void M(string id, string name, string sub, int price, Color col, string desc)
        {
            I(id, name, ItemCategory.Материалы, price, 0f, 0f, "Ингредиент · используется в крафте", desc, col, true, 10, 1, sub);
        }

        // Рецепт: выход, количество, уровень, затем пары «id материала, сколько»
        static void R(string outId, int outCount, int lvl, params object[] pairs)
        {
            var r = new RecipeDef { outId = outId, outCount = outCount, minLevel = lvl, ids = new string[pairs.Length / 2], counts = new int[pairs.Length / 2] };
            for (int i = 0; i < r.ids.Length; i++) { r.ids[i] = (string)pairs[i * 2]; r.counts[i] = (int)pairs[i * 2 + 1]; }
            Recipes.Add(r);
        }

        static string Eff(float p) { return p >= 1.3f ? "очень высокая" : p >= 1.1f ? "высокая" : p >= 0.9f ? "средняя" : "низкая"; }

        // Расходуемая наживка. family — id базовой наживки, чьи предпочтения у рыб наследуются.
        static void N(string id, string name, string sub, int price, float power, string family, Color c, string desc, int pack = 10, int lvl = 1)
        {
            I(id, name, ItemCategory.Наживки, price, power, 0f, "Эффективность: " + Eff(power), desc, c, true, pack, lvl, sub);
            Items[Items.Count - 1].family = family;
        }

        // Многоразовая приманка
        static void L(string id, string name, string sub, int price, float power, string family, Color c, string stat, string desc, int lvl)
        {
            I(id, name, ItemCategory.Приманки, price, power, 0f, stat, desc, c, false, 1, lvl, sub);
            Items[Items.Count - 1].family = family;
        }

        // Рыба клюёт на товар, если он (или его семейство) есть в списке любимых
        public static bool Likes(FishDef f, ItemDef b)
        {
            if (f == null || b == null || f.baits == null) return false;
            return System.Array.IndexOf(f.baits, b.id) >= 0 || (b.family != null && System.Array.IndexOf(f.baits, b.family) >= 0);
        }

        static GameData()
        {
            // ---------- РЫБЫ ----------
            F("white_amur", "Белый амур", "Крупная растительноядная рыба. Любит прибрежные заросли камыша и мелководье.",
                "Заросли камыша, мелководье", 1.5f, 18f, 40, 110, Rarity.Необычная, 1.0f, 0.5f, 1, 4,
                new[] { "corn", "dough", "lure_spinner_s" }, 90, 30, C(150, 160, 130), C(235, 235, 225), new Vector3(1.0f, 0.22f, 0.16f), 7);
            F("black_amur", "Чёрный амур", "Мощная рыба с тёмной чешуёй. Питается моллюсками, держится у дна.",
                "Глубина у дна, ямы", 2f, 25f, 50, 120, Rarity.Редкая, 1.4f, 0.7f, 3, 7,
                new[] { "worm", "corn", "lure_jig" }, 130, 55, C(60, 65, 70), C(120, 125, 125), new Vector3(1.0f, 0.23f, 0.17f), 4);
            F("beluga", "Белуга", "Легендарный гигант. Встречается крайне редко и сражается до конца.",
                "Самые глубокие ямы", 30f, 150f, 150, 300, Rarity.Легендарная, 2.0f, 0.9f, 7, 12,
                new[] { "fish_piece", "lure_big" }, 420, 400, C(80, 90, 100), C(220, 220, 215), new Vector3(1.4f, 0.2f, 0.2f), 0.4f);
            F("sturgeon", "Осётр", "Древняя рыба с костными пластинами на спине. Ищет пищу на дне.",
                "Дно глубоких участков", 5f, 60f, 80, 200, Rarity.Легендарная, 1.8f, 0.7f, 6, 11,
                new[] { "worm", "fish_piece" }, 300, 220, C(95, 95, 85), C(210, 205, 190), new Vector3(1.3f, 0.17f, 0.15f), 0.8f);
            F("pike", "Щука", "Хищница-засадчица. Бросается на блесну и живца из зарослей.",
                "Кромка камыша, кувшинки", 1f, 20f, 40, 130, Rarity.Редкая, 1.2f, 1.0f, 1, 5,
                new[] { "lure_spinner", "lure_wobbler", "minnow" }, 110, 70, C(80, 110, 60), C(220, 215, 170), new Vector3(1.2f, 0.15f, 0.12f), 3);
            F("crucian", "Карась", "Самая частая добыча. Охотно берёт червя и хлеб, сопротивляется слабо.",
                "Мелководье, у кувшинок", 0.1f, 2.5f, 12, 40, Rarity.Обычная, 0.35f, 0.3f, 0.5f, 3,
                new[] { "worm", "bread", "dough", "corn" }, 40, 8, C(190, 160, 70), C(230, 210, 130), new Vector3(0.7f, 0.4f, 0.12f), 22);
            F("ruffe", "Ёрш", "Колючий мелкий хищник. Клюёт на червя у самого дна.",
                "Дно, ближе к берегу", 0.03f, 0.35f, 6, 18, Rarity.Обычная, 0.25f, 0.4f, 1, 5,
                new[] { "worm", "maggot" }, 30, 4, C(100, 100, 70), C(200, 190, 140), new Vector3(0.75f, 0.3f, 0.12f), 18);
            F("catfish", "Сом", "Огромный усатый хищник. Ночной охотник, держится в ямах и под корягами.",
                "Глубокие ямы, коряги", 3f, 90f, 70, 250, Rarity.Легендарная, 1.9f, 0.8f, 5, 10,
                new[] { "worm", "fish_piece", "lure_big" }, 150, 180, C(60, 60, 50), C(150, 150, 130), new Vector3(1.3f, 0.2f, 0.2f), 1);
            F("perch", "Окунь", "Полосатый хищник. Стайный, хватает блёсны и червей.",
                "Свалы глубины, у мостов", 0.1f, 2.5f, 15, 45, Rarity.Обычная, 0.5f, 0.8f, 1.5f, 6,
                new[] { "worm", "maggot", "lure_spinner_s", "minnow" }, 60, 14, C(90, 120, 60), C(230, 190, 90), new Vector3(0.8f, 0.34f, 0.11f), 14);
            F("zander", "Судак", "Осторожный хищник с клыкастой пастью. Предпочитает глубину и чистую воду.",
                "Глубина, течение у ям", 1f, 15f, 40, 100, Rarity.Редкая, 1.1f, 0.8f, 4, 9,
                new[] { "minnow", "lure_jig", "lure_wobbler" }, 120, 60, C(110, 115, 100), C(220, 220, 205), new Vector3(1.1f, 0.2f, 0.13f), 3);
            F("carp", "Сазан", "Сильная и хитрая рыба. Лучшая цель для настоящего вываживания.",
                "Средние глубины, илистое дно", 1f, 30f, 30, 100, Rarity.Необычная, 1.3f, 0.6f, 2, 7,
                new[] { "corn", "dough", "boilie", "worm" }, 85, 45, C(170, 130, 60), C(235, 215, 150), new Vector3(0.95f, 0.35f, 0.17f), 8);

            // ---------- УДОЧКИ ----------
            I("rod_start", "Бамбуковая удочка", ItemCategory.Удочки, 0, 5f, 8f, "Нагрузка до 5 кг, заброс 8 м", "Простая удочка для начинающих.", C(160, 130, 70));
            I("rod_fiber", "Стеклопластиковая удочка", ItemCategory.Удочки, 600, 12f, 14f, "Нагрузка до 12 кг, заброс 14 м", "Лёгкая и прочная.", C(60, 120, 160), false, 1, 2);
            I("rod_carbon", "Карбоновый спиннинг", ItemCategory.Удочки, 2200, 25f, 22f, "Нагрузка до 25 кг, заброс 22 м", "Чувствительный бланк, дальний заброс.", C(30, 30, 35), false, 1, 5);
            I("rod_pro", "Карповое удилище Про", ItemCategory.Удочки, 7500, 60f, 30f, "Нагрузка до 60 кг, заброс 30 м", "Для самой крупной рыбы озера.", C(120, 30, 30), false, 1, 9);

            // ---------- КАТУШКИ ----------
            I("reel_basic", "Катушка «Ручеёк»", ItemCategory.Катушки, 0, 1.0f, 0.4f, "Скорость 1,0 · тормоз 0,4", "Простая безынерционная катушка.", C(150, 150, 150));
            I("reel_mid", "Катушка «Волга-2»", ItemCategory.Катушки, 450, 1.4f, 0.6f, "Скорость 1,4 · тормоз 0,6", "Надёжная катушка с мягким фрикционом.", C(180, 140, 60), false, 1, 2);
            I("reel_pro", "Катушка «Титан»", ItemCategory.Катушки, 2800, 2.0f, 0.85f, "Скорость 2,0 · тормоз 0,85", "Металлический корпус, плавный тормоз.", C(60, 60, 70), false, 1, 6);

            // ---------- ЛЕСКИ ----------
            I("line_015", "Леска 0,15 мм", ItemCategory.Лески, 0, 3f, 1f, "Прочность 3 кг · незаметность 100%", "Тонкая, малозаметная леска: рыба клюёт охотнее.", C(220, 220, 230));
            I("line_025", "Леска 0,25 мм", ItemCategory.Лески, 180, 10f, 0.85f, "Прочность 10 кг · незаметность 85%", "Универсальная монофильная леска.", C(210, 230, 210), false, 1, 2);
            I("line_035", "Плетёнка 0,35 мм", ItemCategory.Лески, 900, 30f, 0.7f, "Прочность 30 кг · незаметность 70%", "Нерастяжимый шнур для крупной рыбы.", C(230, 120, 60), false, 1, 5);

            // ---------- КРЮЧКИ ----------
            I("hook_s", "Крючок №4", ItemCategory.Крючки, 0, 0.8f, 1.0f, "Мелкий: держит рыбу до 3 кг", "Для ерша, карася и окуня.", C(190, 190, 200));
            I("hook_m", "Крючок №8", ItemCategory.Крючки, 120, 1.0f, 1.0f, "Средний: держит рыбу до 15 кг", "Подходит для большинства рыб.", C(200, 200, 210), false, 1, 2);
            I("hook_l", "Крючок №12", ItemCategory.Крючки, 300, 1.25f, 1.0f, "Крупный: для любой рыбы, в т. ч. гигантов", "Для щуки, сома и сазана.", C(210, 210, 220), false, 1, 4);

            // ---------- ПОПЛАВКИ ----------
            I("float_basic", "Поплавок «Пуля»", ItemCategory.Поплавки, 0, 1.0f, 1f, "Чувствительность 1,0", "Простой пенопластовый поплавок.", C(230, 60, 50));
            I("float_sens", "Поплавок «Антенна»", ItemCategory.Поплавки, 200, 1.4f, 1f, "Чувствительность 1,4", "Тонкая антенна показывает малейшую поклёвку.", C(250, 200, 30), false, 1, 2);
            I("float_night", "Светящийся поплавок", ItemCategory.Поплавки, 520, 1.8f, 1f, "Чувствительность 1,8", "Виден издалека, хорош в сумерках.", C(120, 250, 120), false, 1, 4);

            // ---------- ГРУЗИЛА ----------
            I("sinker_s", "Грузило 5 г", ItemCategory.Грузила, 0, 5f, 1f, "Вес 5 г · достаёт до 3 м · заброс ближе", "Лёгкое грузило.", C(110, 110, 115));
            I("sinker_m", "Грузило 15 г", ItemCategory.Грузила, 90, 15f, 1f, "Вес 15 г · достаёт до 7 м", "Универсальное.", C(100, 100, 105), false, 1, 2);
            I("sinker_l", "Грузило 40 г", ItemCategory.Грузила, 220, 40f, 1f, "Вес 40 г · достаёт до 19 м · заброс дальше", "Тяжёлое, быстро тонет.", C(90, 90, 95), false, 1, 4);

            // ---------- ОСНАСТКИ ----------
            I("rig_float", "Поплавочная оснастка", ItemCategory.Оснастки, 0, 1f, 0f, "Верхние слои и мелководье", "Классика: поплавок, грузило и крючок.", C(120, 200, 230));
            I("rig_bottom", "Донная оснастка", ItemCategory.Оснастки, 350, 1f, 1f, "Донная рыба: осётр, сом, сазан", "Груз на дне, наживка у грунта.", C(160, 120, 80), false, 1, 3);
            I("rig_spin", "Спиннинговая проводка", ItemCategory.Оснастки, 500, 1f, 2f, "Приманки и хищники", "Ведение приманки по воде.", C(220, 90, 90), false, 1, 3);

            // ---------- НАЖИВКИ ----------
            I("worm", "Червь", ItemCategory.Наживки, 25, 1.0f, 0f, "Эффективность: высокая", "Универсальная наживка. Любят все.", C(200, 110, 110), true, 10, sub: "Черви");
            I("maggot", "Опарыш", ItemCategory.Наживки, 20, 0.9f, 0f, "Эффективность: средняя", "Мелкая рыба берёт жадно.", C(240, 230, 170), true, 10, sub: "Личинки");
            I("bread", "Хлеб", ItemCategory.Наживки, 10, 0.7f, 0f, "Эффективность: низкая", "Дешёвая наживка для карася.", C(230, 200, 150), true, 10, sub: "Натуральные насадки");
            I("corn", "Кукуруза", ItemCategory.Наживки, 30, 1.0f, 0f, "Эффективность: высокая", "Сладкая кукуруза для сазана и амура.", C(250, 220, 60), true, 10, sub: "Натуральные насадки");
            I("dough", "Тесто", ItemCategory.Наживки, 35, 0.95f, 0f, "Эффективность: средняя", "Ароматное тесто.", C(240, 230, 200), true, 10, sub: "Тесто");
            I("boilie", "Бойлы", ItemCategory.Наживки, 120, 1.3f, 0f, "Эффективность: очень высокая", "Протеиновые шарики для сазана.", C(180, 80, 60), true, 5, 3, sub: "Тонущий бойл");
            I("fish_piece", "Кусочек рыбы", ItemCategory.Наживки, 70, 1.2f, 0f, "Эффективность: высокая для хищников и гигантов", "Резкий запах привлекает сомов и осетров.", C(210, 120, 110), true, 5, 3, sub: "Натуральные насадки");
            I("minnow", "Живец", ItemCategory.Наживки, 60, 1.2f, 0f, "Эффективность: высокая для хищников", "Живая рыбка-приманка.", C(190, 200, 220), true, 5, 2, sub: "Живцы");

            // ---------- ПРИМАНКИ ----------
            I("lure_spinner_s", "Блесна-вертушка малая", ItemCategory.Приманки, 150, 1.0f, 0f, "Окунь и амур · многоразовая", "Вращающийся лепесток.", C(210, 210, 220), false, 1, 2, sub: "Вращающиеся блёсны");
            I("lure_spinner", "Блесна-вертушка", ItemCategory.Приманки, 260, 1.2f, 0f, "Щука и окунь · многоразовая", "Классика для хищника.", C(230, 190, 50), false, 1, 3, sub: "Вращающиеся блёсны");
            I("lure_wobbler", "Воблер", ItemCategory.Приманки, 480, 1.3f, 0f, "Щука и судак · многоразовый", "Имитирует раненую рыбку.", C(60, 180, 90), false, 1, 4, sub: "Воблеры");
            I("lure_jig", "Джиг с силиконом", ItemCategory.Приманки, 320, 1.25f, 0f, "Судак и чёрный амур · многоразовый", "Силиконовая приманка для донной проводки.", C(180, 60, 160), false, 1, 4, sub: "Виброхвосты");
            I("lure_big", "Глубинная приманка", ItemCategory.Приманки, 1400, 1.5f, 0f, "Белуга, сом, щука · многоразовая", "Тяжёлая приманка для трофеев.", C(40, 80, 200), false, 1, 7, sub: "Воблеры");


            // ---------- НАЖИВКИ: расширенный ассортимент ----------
            N("live_bleak", "Уклейка", "Живцы", 55, 1.15f, "minnow", C(200, 210, 230), "Живая уклейка для хищника.", 5, 2);
            N("live_roach", "Плотва", "Живцы", 60, 1.2f, "minnow", C(210, 130, 110), "Живая плотва — любимая добыча щуки и судака.", 5, 2);
            N("live_gudgeon", "Пескарь", "Живцы", 65, 1.2f, "minnow", C(170, 170, 150), "Донный живец для судака и сома.", 5, 3);
            N("live_crucian", "Карась (живец)", "Живцы", 50, 1.1f, "minnow", C(190, 160, 70), "Небольшой карась как живец.", 5, 2);
            N("worm_dung", "Навозный червь", "Черви", 22, 0.95f, "worm", C(190, 90, 90), "Яркий червь для карася и ерша.");
            N("worm_earth", "Дождевой червь", "Черви", 28, 1.0f, "worm", C(200, 110, 110), "Классическая наживка.");
            N("worm_night", "Выползок", "Черви", 35, 1.1f, "worm", C(170, 90, 90), "Крупный ночной червь.", 10, 2);
            N("worm_big", "Крупный червь", "Черви", 45, 1.15f, "worm", C(190, 100, 100), "Для крупного сазана и амура.", 10, 3);
            N("leech", "Пиявка", "Черви", 60, 1.2f, "worm", C(50, 50, 55), "Привлекает сома и чёрного амура.", 5, 3);
            N("bloodworm", "Мотыль", "Личинки", 25, 1.0f, "maggot", C(190, 40, 40), "Мелкая красная личинка.");
            N("maggot_red", "Красный опарыш", "Личинки", 24, 0.95f, "maggot", C(220, 80, 70), "Опарыш красного цвета.");
            N("maggot_white", "Белый опарыш", "Личинки", 22, 0.9f, "maggot", C(245, 240, 215), "Белый опарыш.");
            N("caster", "Кастер", "Личинки", 40, 1.1f, "maggot", C(150, 100, 50), "Куколка опарыша: отсеивает мелочь.", 10, 2);
            N("caddis", "Ручейник", "Личинки", 35, 1.05f, "maggot", C(140, 120, 80), "Личинка ручейника в домике.", 10, 2);
            N("cricket", "Сверчок", "Жуки", 40, 1.0f, "worm", C(90, 70, 40), "Живое насекомое.", 10, 2);
            N("grasshopper", "Кузнечик", "Жуки", 40, 1.0f, "worm", C(110, 170, 70), "Летняя наживка для окуня.", 10, 2);
            N("mayfly", "Подёнка", "Жуки", 35, 0.95f, "maggot", C(220, 220, 180), "Нежная наживка для мелкой рыбы.", 10, 2);
            N("cockchafer", "Личинка майского жука", "Жуки", 50, 1.1f, "worm", C(235, 225, 190), "Жирная личинка для крупной рыбы.", 10, 3);
            string[] doughNames = { "Кукурузное", "Творожное", "Яичное", "Чесночное", "Медовое", "Овсяное", "Гороховое", "Манное", "Ванильное", "Клубничное" };
            for (int i = 0; i < doughNames.Length; i++)
                N("dough_" + i, "Тесто " + doughNames[i].ToLower(), "Тесто", 35 + (i % 3) * 5, 0.9f + (i % 4) * 0.05f, "dough", C(240 - i * 5, 225 - i * 4, 190 - i * 6), "Ароматное тесто со вкусом: " + doughNames[i].ToLower() + ".");
            N("nat_pea", "Горох", "Натуральные насадки", 18, 0.9f, "corn", C(130, 180, 80), "Распаренный горох.");
            N("nat_wheat", "Пшеница", "Натуральные насадки", 15, 0.9f, "corn", C(220, 190, 110), "Пареная пшеница.");
            N("nat_barley", "Перловка", "Натуральные насадки", 16, 0.85f, "corn", C(225, 215, 180), "Варёная перловка.");
            N("nat_hemp", "Конопля", "Натуральные насадки", 20, 1.0f, "corn", C(110, 90, 60), "Запаренная конопля.");
            N("nat_tiger", "Тигровый орех", "Натуральные насадки", 40, 1.2f, "corn", C(190, 150, 100), "Выбирает крупного сазана.", 10, 3);
            N("nat_boiled_corn", "Варёная кукуруза", "Натуральные насадки", 28, 1.05f, "corn", C(250, 215, 70), "Мягкая варёная кукуруза.");
            N("nat_potato", "Картофель", "Натуральные насадки", 14, 0.85f, "bread", C(225, 205, 140), "Варёный картофель.");
            N("nat_cabbage", "Капустный лист", "Натуральные насадки", 8, 0.6f, "bread", C(150, 200, 120), "Для белого амура.");
            N("nat_crayfish", "Мясо рака", "Натуральные насадки", 80, 1.25f, "fish_piece", C(220, 120, 90), "Сильный запах для трофейной рыбы.", 5, 3);
            N("nat_liver", "Кубики печени", "Натуральные насадки", 55, 1.15f, "fish_piece", C(120, 40, 40), "Кровяной запах привлекает сома.", 5, 3);
            string[] boil = { "Белый шоколад", "Банан", "Чёрная смородина", "Карамель", "Кокос и сливки", "Краб и мидии", "Крем-брюле", "Свежая клюква", "Весенняя трава", "Клубника", "Краб", "Крилль", "Ананас" };
            for (int i = 0; i < boil.Length; i++)
                N("boil_" + i, "Бойл: " + boil[i], "Тонущий бойл", 120 + (i % 3) * 10, 1.3f, "boilie", C(180 - i * 6, 80 + i * 4, 60 + i * 8), "Тонущий бойл, аромат: " + boil[i].ToLower() + ".", 5, 3);
            string[] pop = { "Банан", "Ежевика", "Чёрная смородина", "Карамель", "Кокос и сливки", "Свежая малина", "Мидии с чесноком", "Весенняя трава", "Краб", "Крилль", "Ананас" };
            for (int i = 0; i < pop.Length; i++)
                N("pop_" + i, "Pop-Up: " + pop[i], "Всплывающий бойл (Pop-Up)", 130 + (i % 3) * 10, 1.3f, "boilie", C(250 - i * 8, 170 - i * 6, 90 + i * 9), "Всплывающий бойл, аромат: " + pop[i].ToLower() + ".", 5, 4);
            string[] pel = { "Банан", "Ежевика", "Чёрная смородина", "Кокос и сливки", "Карамель", "Краб и рыба", "Свежая клюква", "Травяной десерт", "Малина", "Лососевая икра" };
            for (int i = 0; i < pel.Length; i++)
                N("pellet_" + i, "Пеллетс: " + pel[i], "Пеллеты", 90, 1.2f, "boilie", C(140 + i * 6, 100 + i * 4, 70), "Пеллетс, аромат: " + pel[i].ToLower() + ".", 10, 3);
            string[] fc = { "Банан", "Кокос и сливки", "Клюква", "Мидии", "Малина", "Красная икра", "Клубника", "Тутти-фрутти", "Жёлтая икра", "Натуральная", "Флуоресцентная" };
            for (int i = 0; i < fc.Length; i++)
                N("fakecorn_" + i, "Искусственная кукуруза: " + fc[i].ToLower(), "Искусственная кукуруза", 60, 1.1f, "corn", C(250, 230 - i * 8, 70 + i * 12), "Плавающая кукуруза, аромат: " + fc[i].ToLower() + ".", 10, 3);

            // ---------- ПРИМАНКИ: расширенный ассортимент ----------
            L("lure_wobbler_minnow", "Воблер Minnow", "Минноу", 420, 1.25f, "lure_wobbler", C(180, 200, 220), "Щука и судак · многоразовый", "Узкий воблер, имитирует мальков.", 3);
            L("lure_wobbler_shad", "Воблер Shad", "Шэды", 450, 1.25f, "lure_wobbler", C(200, 170, 90), "Щука и судак · многоразовый", "Высокотелый воблер.", 3);
            L("lure_wobbler_crank", "Воблер Crank", "Крэнки", 460, 1.3f, "lure_wobbler", C(220, 110, 60), "Щука и окунь · многоразовый", "Короткий круглый воблер.", 4);
            L("lure_wobbler_fat", "Воблер Fat", "Воблеры", 480, 1.3f, "lure_wobbler", C(240, 180, 60), "Щука · многоразовый", "Пухлый воблер для активной рыбы.", 4);
            L("lure_wobbler_deep", "Воблер Deep", "Воблеры", 620, 1.35f, "lure_wobbler", C(60, 120, 180), "Судак и сом · глубокий", "Заглубляется глубже обычных.", 5);
            L("lure_wobbler_troll", "Воблер Trolling", "Воблеры", 760, 1.4f, "lure_wobbler", C(50, 90, 140), "Крупный хищник · трофейный", "Для проводки на большой глубине.", 6);
            L("lure_wobbler_jerk", "Джеркбейт", "Джеркбейты", 520, 1.3f, "lure_wobbler", C(120, 200, 170), "Щука · многоразовый", "Рваная проводка.", 4);
            L("lure_wobbler_popper", "Поппер", "Попперы", 400, 1.2f, "lure_wobbler", C(230, 90, 90), "Щука · поверхностный", "Булькает на поверхности.", 4);
            L("lure_wobbler_walker", "Волкер", "Волкеры", 410, 1.2f, "lure_wobbler", C(210, 210, 90), "Щука · поверхностный", "Идёт «зигзагом» по воде.", 4);
            L("lure_spinner_compact", "Вертушка компактная", "Вращающиеся блёсны", 140, 1.0f, "lure_spinner_s", C(200, 200, 210), "Окунь · многоразовая", "Маленькая вертушка.", 1);
            L("lure_spinner_mid", "Вертушка средняя", "Вращающиеся блёсны", 240, 1.15f, "lure_spinner", C(220, 190, 60), "Щука и окунь", "Универсальный размер.", 2);
            L("lure_spinner_big", "Вертушка большая", "Вращающиеся блёсны", 340, 1.25f, "lure_spinner", C(230, 170, 50), "Щука · крупная", "Большой лепесток.", 4);
            L("lure_spinner_narrow", "Вертушка с узким лепестком", "Вращающиеся блёсны", 260, 1.2f, "lure_spinner", C(200, 200, 200), "Судак и щука", "Глубже и быстрее.", 3);
            L("lure_spinner_wide", "Вертушка с широким лепестком", "Вращающиеся блёсны", 260, 1.2f, "lure_spinner", C(230, 210, 120), "Щука · медленная проводка", "Яркая игра на малой скорости.", 3);
            L("lure_spoon_narrow", "Колеблющаяся блесна узкая", "Колеблющиеся блёсны", 300, 1.2f, "lure_spinner", C(190, 190, 200), "Щука и судак", "Узкая колебалка.", 3);
            L("lure_spoon_wide", "Колеблющаяся блесна широкая", "Колеблющиеся блёсны", 310, 1.2f, "lure_spinner", C(220, 200, 100), "Щука и окунь", "Широкая колебалка.", 3);
            L("lure_spoon_light", "Колеблющаяся блесна лёгкая", "Колеблющиеся блёсны", 280, 1.1f, "lure_spinner_s", C(210, 210, 220), "Окунь", "Лёгкая, для мелководья.", 2);
            L("lure_spoon_heavy", "Колеблющаяся дальнобойная", "Колеблющиеся блёсны", 480, 1.3f, "lure_spinner", C(150, 150, 170), "Хищник на дальней дистанции", "Тяжёлая, летит далеко.", 5);
            L("lure_castmaster", "Кастмастер", "Кастмастеры", 560, 1.35f, "lure_spinner", C(230, 230, 240), "Судак и щука", "Тяжёлая блесна для дальнего заброса.", 5);
            L("lure_twister", "Твистер", "Твистеры", 220, 1.2f, "lure_jig", C(240, 140, 60), "Окунь и судак", "Мягкий хвост-«червь».", 2);
            L("lure_slug", "Слаг", "Слаги", 240, 1.2f, "lure_jig", C(220, 220, 200), "Щука и судак", "Силиконовая «сосиска».", 3);
            L("lure_siliworm", "Силиконовый червь", "Силиконовые черви", 200, 1.15f, "lure_jig", C(200, 90, 90), "Окунь и чёрный амур", "Имитирует крупного червя.", 2);
            L("lure_creature", "Креатура", "Креатуры", 340, 1.3f, "lure_jig", C(90, 140, 70), "Судак и чёрный амур", "Рачок/личинка с клешнями.", 4);
            L("lure_frog", "Лягушка", "Лягушки", 360, 1.25f, "lure_wobbler", C(80, 170, 80), "Щука в траве", "Незацепляйка для зарослей.", 4);
            L("lure_spinnerbait", "Спиннербейт", "Спиннербейты", 440, 1.3f, "lure_wobbler", C(200, 180, 70), "Щука в коряжнике", "Блесна с юбкой.", 5);
            L("lure_chatterbait", "Чаттербейт", "Чаттербейты", 460, 1.3f, "lure_wobbler", C(70, 70, 90), "Щука и судак", "Вибрирующая пластина.", 5);
            L("lure_fly", "Мушка", "Мушки", 120, 0.95f, "lure_spinner_s", C(230, 120, 70), "Окунь · многоразовая", "Лёгкая приманка-насекомое.", 2);


            // ---------- ПРИКОРМКИ (готовые) ----------
            G("gb_carp", "Прикормка карповая", "Карповая", 90, 1.5f, "Кукуруза и конопля с мелассой: тянет сазана и амуров.", C(200, 160, 70), 2, "carp", "white_amur", "black_amur");
            G("gb_feeder", "Прикормка фидерная", "Фидерная", 80, 1.4f, "Липкая смесь для донной ловли.", C(150, 120, 80), 2, "carp", "crucian", "ruffe");
            G("gb_bream", "Прикормка лещовая", "Лещовая", 75, 1.4f, "Сладкая ванильная смесь (леща в игре пока нет — работает на сазана и карася).", C(210, 190, 120), 2, "carp", "crucian");
            G("gb_crucian", "Прикормка карасевая", "Карасевая", 60, 1.45f, "Овсяная с мёдом: для карася.", C(220, 200, 140), 1, "crucian");
            G("gb_roach", "Прикормка плотвиная", "Плотвиная", 60, 1.35f, "Лёгкая смесь для мелкой рыбы.", C(190, 190, 160), 1, "ruffe", "perch");
            G("gb_river", "Прикормка речная", "Речная", 85, 1.4f, "Тяжёлая смесь с рыбной мукой: для судака и окуня.", C(110, 120, 100), 3, "zander", "perch", "carp");
            G("gb_lake", "Прикормка озёрная", "Озёрная", 75, 1.4f, "Семена и перловка: для озёрной рыбы.", C(170, 150, 90), 2, "crucian", "carp", "white_amur");
            G("gb_cold", "Прикормка для холодной воды", "Для холодной воды", 95, 1.4f, "Рыбная мука и чеснок.", C(120, 130, 140), 3, "pike", "perch", "zander");
            G("gb_warm", "Прикормка для тёплой воды", "Для тёплой воды", 85, 1.4f, "Сладкая кукурузная смесь.", C(230, 190, 90), 2, "carp", "crucian", "white_amur");

            // ---------- МАТЕРИАЛЫ ----------
            M("mat_barley", "Перловка (сырая)", "Зерновые", 30, C(225, 215, 180), "Для прикормок. Упаковка — 10 единиц.");
            M("mat_wheat", "Пшеница (зерно)", "Зерновые", 28, C(220, 190, 110), "Зерно для прикормок.");
            M("mat_pea", "Горох сухой", "Зерновые", 30, C(150, 190, 90), "Сухой горох.");
            M("mat_oat", "Овсяные хлопья", "Зерновые", 28, C(235, 225, 200), "Основа карасевой прикормки.");
            M("mat_sunflower", "Семена подсолнечника", "Семена", 32, C(70, 60, 50), "Жареные молотые семена.");
            M("mat_hemp", "Семена конопли", "Семена", 36, C(110, 90, 60), "Классика карповой прикормки.");
            M("mat_rape", "Рапс", "Семена", 32, C(120, 110, 60), "Семена рапса.");
            M("mat_cornflour", "Кукурузная мука", "Мука", 32, C(250, 225, 110), "Лёгкая сладкая основа.");
            M("mat_wheatflour", "Пшеничная мука", "Мука", 26, C(240, 235, 220), "Связующая добавка.");
            M("mat_fishmeal", "Рыбная мука", "Мука", 52, C(160, 140, 110), "Резкий запах для хищников.");
            M("mat_crumbs", "Молотые сухари", "Ингредиенты для прикормки", 26, C(220, 190, 140), "Основа почти любой прикормки.");
            M("mat_bran", "Ржаные отруби", "Ингредиенты для прикормки", 26, C(170, 140, 100), "Рассыпчатая основа.");
            M("mat_cake", "Жмых", "Ингредиенты для прикормки", 34, C(120, 90, 60), "Прессованный жмых семян.");
            M("mat_milk", "Сухое молоко", "Ингредиенты для прикормки", 44, C(245, 245, 235), "Делает смесь «молочной».");
            M("mat_honey", "Медовый сироп", "Жидкие ароматизаторы", 52, C(230, 170, 50), "Сладкий жидкий ароматизатор.");
            M("mat_molasses", "Меласса", "Жидкие ароматизаторы", 56, C(70, 40, 20), "Густая патока для карповых смесей.");
            M("mat_fishext", "Рыбный экстракт", "Жидкие ароматизаторы", 70, C(180, 140, 100), "Запах рыбы.");
            M("mat_vanilla", "Ваниль", "Сухие ароматизаторы", 40, C(240, 230, 190), "Сухой ароматизатор.");
            M("mat_banana", "Банановый ароматизатор", "Сухие ароматизаторы", 40, C(250, 230, 100), "Сухой ароматизатор.");
            M("mat_garlic", "Чеснок сушёный", "Сухие ароматизаторы", 36, C(235, 225, 200), "Чесночный запах.");

            // ---------- РЕЦЕПТЫ ПРИКОРМКИ ----------
            R("gb_carp", 5, 2, "mat_cornflour", 2, "mat_hemp", 2, "mat_molasses", 1);
            R("gb_feeder", 5, 2, "mat_crumbs", 2, "mat_sunflower", 2, "mat_cornflour", 1);
            R("gb_bream", 5, 2, "mat_crumbs", 2, "mat_bran", 2, "mat_vanilla", 1);
            R("gb_crucian", 5, 1, "mat_oat", 2, "mat_honey", 1, "mat_wheat", 1);
            R("gb_roach", 5, 1, "mat_crumbs", 2, "mat_hemp", 1, "mat_wheatflour", 1);
            R("gb_river", 5, 3, "mat_cake", 2, "mat_crumbs", 2, "mat_fishmeal", 1);
            R("gb_lake", 5, 2, "mat_sunflower", 2, "mat_barley", 2, "mat_banana", 1);
            R("gb_cold", 5, 3, "mat_fishmeal", 2, "mat_garlic", 1, "mat_crumbs", 1);
            R("gb_warm", 5, 2, "mat_cornflour", 2, "mat_honey", 1, "mat_milk", 1);

            // ---------- ПРОЧЕЕ ----------
            I("misc_net", "Подсак", ItemCategory.Прочее, 700, 1.15f, 0f, "Реже срывается на подходе", "Помогает вытащить крупную рыбу.", C(100, 160, 100), false, 1, 3);
            I("misc_groundbait", "Прикормка универсальная", ItemCategory.Прикормки, 80, 1.3f, 0f, "Поклёвки чаще и быстрее на 5 забросов", "Включается сама при забросе, пока есть в запасе.", C(150, 120, 80), true, 5, sub: "Универсальная");
        }


        // ---------------- СЕКЦИИ МАГАЗИНА (порядок заголовков) ----------------
        // Чтобы добавить новый раздел — допишите строку сюда. Чтобы добавить товар — задайте ему cat и sub в I(...):
        // он сам появится в нужной секции, интерфейс менять не нужно.
        static readonly Dictionary<ItemCategory, SectionDef[]> sections = new Dictionary<ItemCategory, SectionDef[]>
        {
            { ItemCategory.Наживки, Secs(null, "Живцы", "Черви", "Личинки", "Жуки", "Тесто", "Натуральные насадки", "Тонущий бойл",
                "Всплывающий бойл (Pop-Up)", "Нейтральной плавучести (Wafters)", "Искусственная кукуруза", "Пеллеты", "Искусственные насадки", "Другие насадки") },
            { ItemCategory.Приманки, Secs(null, "Воблеры", "Минноу", "Крэнки", "Шэды", "Джеркбейты", "Попперы", "Волкеры", "Колеблющиеся блёсны",
                "Вращающиеся блёсны", "Кастмастеры", "Спиннербейты", "Чаттербейты", "Виброхвосты", "Твистеры", "Слаги", "Силиконовые черви",
                "Креатуры", "Лягушки", "Мушки") },
            { ItemCategory.Прикормки, Join(
                Secs("Готовые прикормки", "Универсальная", "Карповая", "Фидерная", "Лещовая", "Карасевая", "Плотвиная", "Речная", "Озёрная", "Для холодной воды", "Для тёплой воды"),
                Secs("Основы и ингредиенты", "Семена", "Молотые семена", "Мука", "Сухари", "Зерновые", "Крупы", "Жмых", "Белковые компоненты"),
                Secs("Дополнительные смеси", "ПВА-стики", "ПВА-смеси", "ПВА-пакеты", "Методные смеси", "Зерновые смеси", "Ароматизаторы", "Добавки")) },
            { ItemCategory.Материалы, Secs(null, "Ингредиенты для прикормки", "Зерновые", "Семена", "Мука", "Жидкие ароматизаторы", "Сухие ароматизаторы",
                "Красители", "Добавки", "Материалы для изготовления бойлов", "Материалы для изготовления поводков и оснасток", "Материалы для изготовления приманок") },
        };

        public const string OtherSection = "Другое";

        static SectionDef[] Secs(string group, params string[] names)
        {
            var r = new SectionDef[names.Length];
            for (int i = 0; i < names.Length; i++) r[i] = new SectionDef(group, names[i]);
            return r;
        }

        static SectionDef[] Join(params SectionDef[][] parts)
        {
            var l = new List<SectionDef>();
            foreach (var p in parts) l.AddRange(p);
            return l.ToArray();
        }

        // Категории, предметы которых можно надеть / выбрать для рыбалки
        public static bool IsEquippable(ItemCategory c)
        {
            return c != ItemCategory.Прочее && c != ItemCategory.Материалы;
        }

        // Секции категории в порядке показа. Пустые не возвращаются. Товары с неизвестной подкатегорией попадают в «Другое».
        // Если задан maker (не пустой) — учитываются только товары этого производителя.
        public static List<KeyValuePair<SectionDef, List<ItemDef>>> SectionsFor(ItemCategory cat, string maker = null)
        {
            var result = new List<KeyValuePair<SectionDef, List<ItemDef>>>();
            var used = new HashSet<ItemDef>();
            SectionDef[] defs;
            if (!sections.TryGetValue(cat, out defs)) defs = new SectionDef[0];
            foreach (var sd in defs)
            {
                var l = new List<ItemDef>();
                foreach (var i in Items)
                    if (i.cat == cat && i.sub == sd.name && (string.IsNullOrEmpty(maker) || i.maker == maker)) { l.Add(i); used.Add(i); }
                if (l.Count > 0) result.Add(new KeyValuePair<SectionDef, List<ItemDef>>(sd, l));
            }
            var rest = new List<ItemDef>();
            foreach (var i in Items)
                if (i.cat == cat && !used.Contains(i) && (string.IsNullOrEmpty(maker) || i.maker == maker)) rest.Add(i);
            if (rest.Count > 0) result.Add(new KeyValuePair<SectionDef, List<ItemDef>>(new SectionDef(null, OtherSection), rest));
            return result;
        }

        // Производители товаров категории (для фильтра). Пусто — фильтр не показывается.
        public static List<string> MakersIn(ItemCategory cat)
        {
            var l = new List<string>();
            foreach (var i in Items) if (i.cat == cat && !string.IsNullOrEmpty(i.maker) && !l.Contains(i.maker)) l.Add(i.maker);
            l.Sort();
            return l;
        }

        public static string TypeName(ItemDef d)
        {
            return string.IsNullOrEmpty(d.sub) ? d.cat.ToString() : d.sub;
        }

        public static FishDef GetFish(string id) { FishDef f; return fishMap.TryGetValue(id, out f) ? f : null; }
        public static ItemDef GetItem(string id) { ItemDef i; return itemMap.TryGetValue(id, out i) ? i : null; }

        public static List<ItemDef> ItemsIn(ItemCategory c)
        {
            var l = new List<ItemDef>();
            foreach (var i in Items) if (i.cat == c) l.Add(i);
            return l;
        }

        public static int XpForLevel(int level) { return 100 + (level - 1) * 80 + (level - 1) * (level - 1) * 15; }
    }
}

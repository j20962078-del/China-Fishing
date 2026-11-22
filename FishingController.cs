using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ChinaFishing
{
    public enum FState { Нет, Готов, Замах, Заброс, Ожидание, Поклёвка, Вываживание, Итог }

    public class CatchResult
    {
        public FishDef fish; public float weight, length; public bool record, firstTime; public int value, xp; public bool levelUp;
    }

    // Цикл: Готов → Замах → Заброс → Ожидание → Поклёвка → (подсечка) → Вываживание → Итог → Готов
    public class FishingController : MonoBehaviour
    {
        public static FishingController I;
        public FState state = FState.Нет;
        public PlayerController player;
        public CameraRig cam;

        public System.Action<string> OnMessage;
        public System.Action<CatchResult> OnCatch;
        public System.Action OnStateChanged;

        public float power;
        public Vector3 target;
        public float castDistance;

        Transform bobber;
        LineRenderer line;
        Vector3 bobberPos, castStart;
        float castT, castDur = 0.9f;
        Coroutine castCo;

        float waitTimer, biteTimer, nibbleTimer;
        bool noFishHere, groundbaitActive, hookRisk;
        FishDef hooked; float hookedWeight, hookedLength;

        public float tension, stamina, dist, startDist;
        public int pullDir = 1;
        public bool reeling;
        public float pullBurst;
        public float biteWindowLeft;
        float dirTimer, burstTimer, overTension, idleTimer, fishAngle, fishForce, strength;
        GameObject heldFish;
        public CatchResult lastResult;
        bool resolved = true;

        public bool HasPendingCatch { get { return lastResult != null && !resolved; } }

        void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this;
            var b = new GameObject("Поплавок").transform;
            WorldBuilder.Prim(PrimitiveType.Sphere, b, Vector3.zero, new Vector3(0.09f, 0.12f, 0.09f), Mats.Lit(245, 245, 245, 0.5f));
            WorldBuilder.Prim(PrimitiveType.Sphere, b, new Vector3(0, 0.07f, 0), new Vector3(0.085f, 0.09f, 0.085f), Mats.Lit(230, 50, 40, 0.5f));
            WorldBuilder.Prim(PrimitiveType.Cylinder, b, new Vector3(0, 0.18f, 0), new Vector3(0.01f, 0.07f, 0.01f), Mats.Lit(250, 220, 50, 0.4f));
            bobber = b; b.gameObject.SetActive(false);
            line = new GameObject("Леска").AddComponent<LineRenderer>();
            line.positionCount = 12; line.startWidth = 0.014f; line.endWidth = 0.01f; line.numCapVertices = 2;
            line.material = Mats.Lit(240, 240, 240, 0.2f);
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.enabled = false;
        }

        void OnDestroy()
        {
            if (I == this) I = null;
            if (bobber != null) Destroy(bobber.gameObject);
            if (line != null) Destroy(line.gameObject);
            if (heldFish != null) Destroy(heldFish);
        }

        // ---------------- СНАСТИ ----------------
        public ItemDef Rod { get { return GameData.GetItem(Game.S.rod); } }
        public ItemDef Reel { get { return GameData.GetItem(Game.S.reel); } }
        public ItemDef Line_ { get { return GameData.GetItem(Game.S.line); } }
        public ItemDef Hook { get { return GameData.GetItem(Game.S.hook); } }
        public ItemDef Float_ { get { return GameData.GetItem(Game.S.floatId); } }
        public ItemDef Sinker { get { return GameData.GetItem(Game.S.sinker); } }
        public ItemDef Rig { get { return GameData.GetItem(Game.S.rig); } }
        public ItemDef Bait { get { return GameData.GetItem(Game.S.bait); } }

        // насколько глубоко опускается наживка (грузило 5 г → 3 м, 15 г → 7,7 м, 40 г → 19 м)
        float SinkReach { get { return 1f + Sinker.power * 0.45f; } }
        // самая тяжёлая рыба, которую уверенно держит крючок
        float HookMax { get { return Hook.power < 0.9f ? 3f : Hook.power < 1.1f ? 15f : 9999f; } }

        void Msg(string s) { if (OnMessage != null) OnMessage(s); }
        void SetState(FState s) { state = s; if (OnStateChanged != null) OnStateChanged(); }

        public bool CanFishHere()
        {
            var p = player.transform.position;
            for (int a = 0; a < 360; a += 30)
                for (float r = 3f; r <= 9f; r += 3f)
                {
                    var q = p + Quaternion.Euler(0, a, 0) * Vector3.forward * r;
                    if (WorldBuilder.H(q.x, q.z) < -0.3f) return true;
                }
            return false;
        }

        public void ToggleFishing()
        {
            if (state == FState.Нет)
            {
                if (!CanFishHere()) { Msg("Подойдите ближе к воде, чтобы ловить рыбу."); return; }
                if (!EnsureBait()) return;
                player.frozen = true; player.fishingPose = true; cam.fishingView = true;
                SetState(FState.Готов);
                Msg("Выберите направление свайпом и удерживайте «Заброс».");
            }
            else if (state == FState.Готов || state == FState.Ожидание || state == FState.Поклёвка)
            {
                StopFishing();
            }
        }

        // Проверяет наживку; если закончилась — выбирает другую из имеющихся
        bool EnsureBait()
        {
            var b = Bait;
            if (b != null && Game.Owns(b.id)) return true;
            foreach (var it in GameData.Items)
                if ((it.cat == ItemCategory.Наживки || it.cat == ItemCategory.Приманки) && Game.Owns(it.id))
                {
                    Game.Equip(it);
                    Msg("Наживка закончилась. Выбрано: " + it.name);
                    return true;
                }
            Msg("Нет наживки. Купите её в магазине.");
            return false;
        }

        public void StopFishing()
        {
            StopAllCoroutines(); castCo = null;
            if (HasPendingCatch) ResolveCatch(true); // улов нельзя потерять
            ClearHeldFish();
            HideTackle();
            reeling = false; hooked = null; lastResult = null;
            if (player != null) { player.frozen = false; player.fishingPose = false; player.castAnim = -1f; player.fightShake = 0f; }
            if (cam != null) cam.fishingView = false;
            AudioManager.I?.Reel(false);
            SetState(FState.Нет);
        }

        void HideTackle()
        {
            if (bobber != null) bobber.gameObject.SetActive(false);
            if (line != null) line.enabled = false;
        }

        // ---------------- ЗАБРОС ----------------
        public void BeginCharge()
        {
            if (state != FState.Готов) return;
            if (!EnsureBait()) return;
            power = 0f; SetState(FState.Замах);
        }

        public void ReleaseCast()
        {
            if (state != FState.Замах) return;
            float maxD = Rod.stat2 * (0.6f + 0.4f * Sinker.power / 40f);
            castDistance = Mathf.Lerp(3.5f, Mathf.Max(5f, maxD), Mathf.Clamp01(power));
            var fwd = Quaternion.Euler(0, cam.yaw, 0) * Vector3.forward;
            player.FaceYaw(cam.yaw);
            target = player.transform.position + fwd * castDistance;
            target.y = WorldBuilder.WaterLevel;
            bool inside = Mathf.Abs(target.x) < 150f && Mathf.Abs(target.z) < 150f;
            if (!inside || WorldBuilder.H(target.x, target.z) > -0.35f)
            {
                Msg("Сюда не достать воды. Бросьте дальше или выберите другое направление.");
                SetState(FState.Готов); return;
            }
            bool lure = Bait.cat == ItemCategory.Приманки;
            if (lure && Rig.id != "rig_spin") Msg("Приманки лучше работают со спиннинговой проводкой.");
            else if (!lure && Rig.id == "rig_spin") Msg("Для спиннинговой проводки лучше брать приманку.");
            player.castAnim = 0f;
            castCo = StartCoroutine(CastRoutine());
        }

        IEnumerator CastRoutine()
        {
            SetState(FState.Заброс);
            float t = 0f; bool launched = false; castT = 0f;
            while (t < 0.75f)
            {
                t += Time.deltaTime; player.castAnim = Mathf.Clamp01(t / 0.75f);
                if (!launched && t >= 0.3f)
                {
                    launched = true;
                    AudioManager.I?.Cast();
                    castStart = player.model.rodTip.position; castT = 0f;
                    bobber.gameObject.SetActive(true); line.enabled = true;
                    bobber.position = castStart;
                }
                if (launched) FlyBobber();
                yield return null;
            }
            player.castAnim = -1f;
            while (castT < 1f) { FlyBobber(); yield return null; }

            // приводнение: наживка списывается РОВНО один раз за заброс
            AudioManager.I?.Splash();
            bobberPos = target;
            var bait = Bait;
            if (bait != null && bait.consumable) Game.UseItem(bait.id, 1);
            Game.S.casts++;
            groundbaitActive = false;
            if (Game.S.groundbaitLeft <= 0)
            {
                var gb = Game.SelectedGroundbait();
                if (gb != null && Game.UseItem(gb.id, 1))
                {
                    Game.S.groundbaitId = gb.id; Game.S.groundbaitLeft = 5;
                    Msg("Прикормка высыпана: " + gb.name + " — поклёвки чаще на 5 забросов.");
                }
            }
            if (Game.S.groundbaitLeft > 0) { groundbaitActive = true; Game.S.groundbaitLeft--; }
            Game.Save(); Game.Notify();
            castCo = null;
            BeginWaiting();
        }

        void FlyBobber()
        {
            castT += Time.deltaTime / castDur;
            float t = Mathf.Clamp01(castT);
            var p = Vector3.Lerp(castStart, target, t);
            p.y += Mathf.Sin(t * Mathf.PI) * (2f + castDistance * 0.12f);
            bobber.position = p;
        }

        // ---------------- ОЖИДАНИЕ И ПОКЛЁВКА ----------------
        float Efficiency()
        {
            float eff = Bait.power * Float_.power * 0.6f + 0.4f;
            eff *= 0.6f + 0.4f * Line_.stat2;          // тонкая леска менее заметна
            eff *= 1f + Game.SkillPct(Game.SkillIndexFor(Game.S.rig, Game.S.bait)) / 100f;   // навык текущего вида ловли
            if (groundbaitActive) { var gbd = GameData.GetItem(Game.S.groundbaitId); float gp = gbd != null && gbd.power > 1f ? gbd.power : 1.35f; eff *= 1f + (gp - 1f) * (1f + Game.CookPct() / 100f); }  // прикормка
            return Mathf.Max(0.2f, eff);
        }

        void BeginWaiting()
        {
            hooked = PickFish(out hookedWeight, out hookedLength);
            noFishHere = hooked == null;
            if (noFishHere) { waitTimer = 18f; Msg("Пока тихо. Рыба не клюёт — подождите или смените место и наживку."); }
            else
            {
                bool pref = GameData.Likes(hooked, Bait);
                waitTimer = Random.Range(5f, 13f) / Efficiency() * (pref ? 0.7f : 1.4f);
                Msg("Ожидайте поклёвку…");
            }
            nibbleTimer = Random.Range(2f, 4f);
            SetState(FState.Ожидание);
        }

        FishDef PickFish(out float w, out float len)
        {
            w = 0; len = 0;
            float depth = -WorldBuilder.H(target.x, target.z);
            float depthEff = Mathf.Min(depth, SinkReach);   // грузило определяет, докуда дойдёт наживка
            float hookMax = HookMax;
            bool lure = Bait.cat == ItemCategory.Приманки;
            var cands = new List<FishDef>(); var wts = new List<float>(); float sum = 0f;
            foreach (var f in GameData.Fish)
            {
                float k = f.spawnWeight;
                if (depthEff < f.depthMin - 1.2f || depthEff > f.depthMax + 1.5f) k *= 0.08f;
                if (depth < f.depthMin - 1.2f) k *= 0.5f;            // слишком мелко для этого вида
                bool likes = GameData.Likes(f, Bait);
                k *= likes ? 3.2f : 0.35f;
                if (groundbaitActive) { var gbd = GameData.GetItem(Game.S.groundbaitId); if (gbd != null && gbd.targets != null && System.Array.IndexOf(gbd.targets, f.id) >= 0) k *= 1.6f; }
                if (lure && Rig.id != "rig_spin") k *= 0.15f;
                if (!lure && Rig.id == "rig_spin") k *= 0.3f;
                if (Rig.id == "rig_bottom") k *= f.depthMin >= 2.5f ? 1.5f : 0.5f;
                if (Rig.id == "rig_float") k *= f.depthMax <= 5.5f ? 1.3f : 0.4f;
                if (f.minW > hookMax * 1.5f) k *= 0.05f;             // крючок слишком мелкий для такой рыбы
                if (f.maxW < 1f && Hook.power > 1.1f) k *= 0.5f;     // крупный крючок для мелочи
                if (f.rarity == Rarity.Легендарная && Game.S.level < 3) k *= 0.3f;
                if (f.rarity != Rarity.Обычная) k *= 1f + Mathf.Min(0.6f, Game.S.level * 0.04f);
                if (k <= 0.0001f) continue;
                cands.Add(f); wts.Add(k); sum += k;
            }
            if (cands.Count == 0 || sum < (groundbaitActive ? 0.05f : 0.2f)) return null;
            float r = Random.value * sum, acc = 0f;
            FishDef pick = cands[cands.Count - 1];
            for (int i = 0; i < cands.Count; i++) { acc += wts[i]; if (r <= acc) { pick = cands[i]; break; } }
            float u = Mathf.Pow(Random.value, 2.6f);
            float wt = Mathf.Lerp(pick.minW, pick.maxW, u);
            wt = Mathf.Min(wt, Mathf.Max(pick.minW, hookMax));       // мелкий крючок не вытащит гиганта
            float uu = pick.maxW > pick.minW ? Mathf.Clamp01((wt - pick.minW) / (pick.maxW - pick.minW)) : 0f;
            w = wt;
            len = Mathf.Lerp(pick.minL, pick.maxL, Mathf.Pow(uu, 0.4f));
            hookRisk = wt > hookMax;
            return pick;
        }

        public void SetHook()
        {
            if (state != FState.Поклёвка) return;
            StartFight();
        }

        // ---------------- ВЫВАЖИВАНИЕ ----------------
        void StartFight()
        {
            var f = hooked;
            if (f == null) { SetState(FState.Ожидание); return; }
            float rodPow = Rod.power, linePow = Line_.power;
            fishForce = Mathf.Clamp(hookedWeight * (0.35f + 0.4f * f.resistance), 0.2f, 400f);
            strength = Mathf.Min(linePow, rodPow * 1.15f);
            startDist = Mathf.Max(4f, Vector3.Distance(player.transform.position, bobberPos));
            dist = startDist;
            tension = 0.35f; stamina = 1f; overTension = 0f; idleTimer = 0f;
            pullDir = Random.value < 0.5f ? -1 : 1; dirTimer = Random.Range(1.5f, 3f); burstTimer = Random.Range(0.8f, 2f);
            pullBurst = 0f; reeling = false; fishAngle = 0f;
            SetState(FState.Вываживание);
            Msg("Рыба на крючке! Удерживайте «Тянуть» и следите за натяжением.");
            AudioManager.I?.Splash();
        }

        public void SetReeling(bool on) { reeling = on && state == FState.Вываживание; }

        void UpdateFight()
        {
            var f = hooked;
            if (f == null) { SetState(FState.Готов); return; }
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            dirTimer -= dt;
            if (dirTimer <= 0f)
            {
                pullDir = Random.value < 0.5f ? -1 : 1;
                dirTimer = Random.Range(1.2f, 3.5f) / (0.6f + f.aggression);
                if (Random.value < 0.5f * f.aggression) burstTimer = 0.2f;
            }
            burstTimer -= dt;
            if (burstTimer <= 0f)
            {
                pullBurst = Random.Range(0.55f, 1f) * (0.5f + 0.5f * stamina) * Mathf.Min(1.2f, 0.6f + f.resistance * 0.4f);
                burstTimer = Random.Range(1.4f, 3.6f) / (0.5f + f.aggression);
                if (Random.value < 0.25f) pullDir = -pullDir;
            }
            pullBurst = Mathf.MoveTowards(pullBurst, 0.12f + 0.2f * stamina, dt * 0.7f);

            float ratio = Mathf.Clamp(fishForce / Mathf.Max(0.5f, strength), 0.15f, 2.4f);
            float counter = Mathf.Clamp(Inp.Move.x * -pullDir, -1f, 1f);
            float brake = Reel.stat2;
            float spike = pullBurst * (0.35f + 0.5f * Mathf.Min(1.5f, ratio)) * (1f - 0.35f * counter) * (1f - 0.4f * brake);
            float baseT = reeling ? 0.5f : 0.1f;
            float targetT = Mathf.Clamp(baseT + spike, 0f, 1.5f);
            tension = Mathf.Lerp(tension, targetT, 1f - Mathf.Exp(-5f * dt));

            if (reeling)
            {
                float speed = 1.3f * Reel.power * (1f - 0.65f * pullBurst) * (0.35f + 0.65f * (1f - stamina * 0.7f));
                speed *= 1f + 0.25f * counter;
                dist -= Mathf.Max(0f, speed) * dt;
                idleTimer = Mathf.Max(0f, idleTimer - dt * 2f);
            }
            else { dist += pullBurst * 1.6f * dt * (0.5f + f.resistance * 0.3f); idleTimer += dt; }

            bool good = tension > 0.25f && tension < 0.8f;
            float drain = (reeling && good ? 0.11f : 0.015f) / (0.6f + f.resistance * 0.6f) + pullBurst * 0.03f;
            stamina = Mathf.Clamp01(stamina - drain * dt);

            if (tension > 1.0f) overTension += dt; else overTension = Mathf.Max(0f, overTension - dt * 0.6f);
            float breakLimit = 0.7f + 0.4f / Mathf.Max(0.6f, ratio);
            if (overTension > breakLimit) { LoseFish("Леска оборвалась! Ослабляйте натяжение, когда рыба рвёт."); return; }
            if (idleTimer > 7f) { LoseFish("Вы долго не тянули — рыба сошла."); return; }
            if (dist > startDist + 25f) { LoseFish("Рыба ушла слишком далеко."); return; }

            fishAngle = Mathf.MoveTowards(fishAngle, pullDir * 40f * Mathf.Clamp01(pullBurst + 0.3f), dt * 60f);
            var fwd = Quaternion.Euler(0, cam.yaw + fishAngle, 0) * Vector3.forward;
            bobberPos = player.transform.position + fwd * Mathf.Max(dist, 0.5f);
            bobberPos.y = WorldBuilder.WaterLevel;
            player.fightShake = Mathf.Clamp01(tension);
            AudioManager.I?.Reel(reeling, 0.8f + tension * 0.7f);

            if (dist <= 2.2f)
            {
                if (hookRisk && Random.value < 0.5f) { LoseFish("Крючок слишком мелкий — он разогнулся, рыба ушла."); return; }
                float landChance = 1f;
                if (stamina > 0.55f) landChance = 0.55f + (Game.Owns("misc_net") ? 0.25f : 0f);
                if (Random.value > landChance) { LoseFish("Рыба сорвалась у самого берега!"); return; }
                Landed();
            }
        }

        void LoseFish(string why)
        {
            reeling = false;
            AudioManager.I?.Reel(false); AudioManager.I?.Fail();
            HideTackle();
            if (player != null) player.fightShake = 0f;
            hooked = null;
            SetState(FState.Готов);
            Msg(why);
        }

        void Landed()
        {
            if (state != FState.Вываживание || hooked == null) return;
            reeling = false;
            var f = hooked; hooked = null;
            SetState(FState.Итог);                  // сразу уходим из состояния боя — повторных наград не будет
            AudioManager.I?.Reel(false); AudioManager.I?.Catch();
            var r = new CatchResult { fish = f, weight = hookedWeight, length = hookedLength };
            bool newRec;
            r.firstTime = Game.RecordCatch(f, hookedWeight, hookedLength, out newRec);
            r.record = newRec;
            r.value = Game.SellValue(f.id, hookedWeight);
            r.xp = Mathf.RoundToInt(f.xp * (0.6f + 0.4f * hookedWeight / Mathf.Max(0.1f, f.maxW)) * (r.firstTime ? 2f : 1f)) + 3;
            r.levelUp = Game.AddXp(r.xp);
            if (r.levelUp) AudioManager.I?.LevelUp();
            Game.Save(); Game.Notify();
            lastResult = r; resolved = false;
            HideTackle();
            if (player != null) player.fightShake = 0f;
            ShowHeldFish(f, hookedLength / 100f);
            if (OnCatch != null) OnCatch(r);
        }

        void ShowHeldFish(FishDef f, float lenM)
        {
            ClearHeldFish();
            heldFish = FishModel.Build(f, Mathf.Clamp(lenM, 0.15f, 1.4f));
            heldFish.transform.SetParent(player.transform, false);
            heldFish.transform.localPosition = new Vector3(0.15f, 1.2f, 0.7f);
            heldFish.transform.localRotation = Quaternion.Euler(75f, 90f, 0f);
        }

        void ClearHeldFish() { if (heldFish != null) Destroy(heldFish); heldFish = null; }

        // Один улов — одно решение: после него lastResult помечен обработанным
        void ResolveCatch(bool sell)
        {
            if (lastResult == null || resolved) return;
            resolved = true;
            if (sell)
            {
                Game.AddMoney(lastResult.value); Game.Save(); Game.Notify();
                Msg("Рыба продана: +" + lastResult.value + " монет");
            }
            else Msg("Вы отпустили рыбу. Она уплыла.");
            lastResult = null;
        }

        public void SellCatch()
        {
            if (state != FState.Итог) return;
            ResolveCatch(true); ClearHeldFish(); SetState(FState.Готов);
        }

        public void ReleaseCatch()
        {
            if (state != FState.Итог) return;
            ResolveCatch(false); ClearHeldFish(); SetState(FState.Готов);
        }

        // ---------------- ОБНОВЛЕНИЕ ----------------
        void Update()
        {
            if (state == FState.Нет || player == null || cam == null) return;
            if (state == FState.Замах) power = Mathf.PingPong(Time.time * 0.9f, 1f);
            if (state == FState.Готов || state == FState.Замах) player.FaceYaw(cam.yaw);
            if (state == FState.Ожидание) UpdateWaiting();
            else if (state == FState.Поклёвка) UpdateBite();
            else if (state == FState.Вываживание) UpdateFight();

            if (bobber != null && bobber.gameObject.activeSelf)
            {
                if (state == FState.Ожидание || state == FState.Поклёвка || state == FState.Вываживание)
                {
                    float bob = Mathf.Sin(Time.time * 1.7f) * 0.012f;
                    float dip = 0f;
                    if (state == FState.Поклёвка) dip = -0.09f - Mathf.Abs(Mathf.Sin(Time.time * 11f)) * 0.04f;
                    if (state == FState.Вываживание) { dip = -0.03f; bob += Mathf.Sin(Time.time * 14f) * 0.02f * pullBurst; }
                    bobber.position = new Vector3(bobberPos.x, WorldBuilder.WaterLevel + 0.03f + bob + dip, bobberPos.z);
                }
                DrawLine();
            }
        }

        void UpdateWaiting()
        {
            waitTimer -= Time.deltaTime;
            nibbleTimer -= Time.deltaTime;
            if (nibbleTimer <= 0f && waitTimer > 1.5f && !noFishHere)
            {
                nibbleTimer = Random.Range(2.5f, 6f);
                bobberPos += new Vector3(Random.Range(-0.06f, 0.06f), 0, Random.Range(-0.06f, 0.06f));
            }
            if (waitTimer > 0f) return;
            if (noFishHere)
            {
                // рыба не нашлась — через время пробуем снова (рыба могла подойти)
                hooked = PickFish(out hookedWeight, out hookedLength);
                noFishHere = hooked == null;
                waitTimer = noFishHere ? 18f : Random.Range(3f, 8f) / Efficiency();
                return;
            }
            biteTimer = 1.3f + 0.45f * Float_.power;
            biteWindowLeft = biteTimer;
            AudioManager.I?.Bite();
            SetState(FState.Поклёвка);
            Msg("Клюёт! Быстро нажмите «Подсечка»!");
        }

        void UpdateBite()
        {
            biteTimer -= Time.deltaTime; biteWindowLeft = biteTimer;
            if (biteTimer > 0f) return;
            // поклёвка пропущена: наживка НЕ списывается повторно, ловля продолжается
            AudioManager.I?.Fail();
            hooked = null;
            BeginWaiting();
            Msg("Вы упустили поклёвку. Рыба ушла, но снасть осталась в воде.");
        }

        // Вытащить снасть без добычи (наживка за этот заброс уже потрачена)
        public void Reclaim()
        {
            if (state != FState.Ожидание && state != FState.Поклёвка) return;
            HideTackle(); hooked = null;
            SetState(FState.Готов);
            Msg("Снасть вытащена.");
        }

        void DrawLine()
        {
            if (line == null || !line.enabled || player == null || player.model == null) return;
            var a = player.model.rodTip.position; var b = bobber.position + Vector3.up * 0.1f;
            float sag = state == FState.Вываживание ? 0.03f + (1f - Mathf.Clamp01(tension)) * 0.35f : 0.4f;
            if (state == FState.Заброс) sag = 0.1f;
            int n = line.positionCount;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                var p = Vector3.Lerp(a, b, t);
                p.y -= Mathf.Sin(t * Mathf.PI) * sag;
                line.SetPosition(i, p);
            }
        }

        public string HintForState()
        {
            switch (state)
            {
                case FState.Готов: return "Свайпом выберите направление, удерживайте «Заброс»";
                case FState.Замах: return "Отпустите кнопку для заброса";
                case FState.Ожидание: return "Ожидание поклёвки…";
                case FState.Поклёвка: return "КЛЮЁТ! Нажмите «Подсечка»";
                case FState.Вываживание: return pullDir < 0 ? "Рыба тянет ВЛЕВО — отклоните джойстик вправо" : "Рыба тянет ВПРАВО — отклоните джойстик влево";
                default: return "";
            }
        }
    }
}

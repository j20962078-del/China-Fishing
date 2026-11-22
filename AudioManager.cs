using System.Collections.Generic;
using UnityEngine;

namespace ChinaFishing
{
    // Все звуки синтезируются в коде, чтобы проект не зависел от внешних файлов.
    // Позже их можно заменить настоящими записями (ветер, птицы, вода).
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager I;
        const int SR = 22050;
        AudioSource wind, water, music, fx, reel;
        AudioClip stepClip, whoosh, splash, bite, catchClip, failClip, click, levelUp;
        readonly List<AudioClip> birds = new List<AudioClip>();
        float birdTimer = 4f;
        System.Random rnd = new System.Random(7);

        public static AudioManager Create()
        {
            if (I != null) return I;           // повторно не создаём
            var go = new GameObject("Звук");
            DontDestroyOnLoad(go);
            var a = go.AddComponent<AudioManager>();
            I = a; a.Init();
            return a;
        }

        AudioSource Src(string n, bool loop, AudioClip c)
        {
            var s = new GameObject(n).AddComponent<AudioSource>();
            s.transform.SetParent(transform, false);
            s.loop = loop; s.playOnAwake = false; s.spatialBlend = 0f; s.clip = c;
            return s;
        }

        float R() { return (float)(rnd.NextDouble() * 2.0 - 1.0); }

        AudioClip Make(string name, float sec, System.Func<float, int, float> f)
        {
            int n = (int)(SR * sec);
            var d = new float[n];
            for (int i = 0; i < n; i++) d[i] = Mathf.Clamp(f(i / (float)SR, i), -1f, 1f);
            var c = AudioClip.Create(name, n, 1, SR, false);
            c.SetData(d, 0);
            return c;
        }

        AudioClip LoopNoise(string name, float sec, float lp, System.Func<float, float> amp)
        {
            int n = (int)(SR * sec), over = SR / 2;
            var raw = new float[n + over];
            float y = 0f;
            for (int i = 0; i < raw.Length; i++) { y += (R() - y) * lp; raw[i] = y; }
            var d = new float[n];
            for (int i = 0; i < n; i++) d[i] = raw[i];
            for (int i = 0; i < over; i++) { float t = i / (float)over; d[i] = Mathf.Lerp(raw[n + i], raw[i], t); }
            float mx = 0.0001f; foreach (var v in d) mx = Mathf.Max(mx, Mathf.Abs(v));
            for (int i = 0; i < n; i++) d[i] = d[i] / mx * amp(i / (float)n);
            var c = AudioClip.Create(name, n, 1, SR, false);
            c.SetData(d, 0);
            return c;
        }

        void Init()
        {
            // ветер и вода (бесшовные петли)
            wind = Src("Ветер", true, LoopNoise("wind", 8f, 0.015f, t => 0.35f + 0.25f * Mathf.Sin(t * Mathf.PI * 2f) * Mathf.Sin(t * Mathf.PI * 6f + 1f)));
            water = Src("Вода", true, LoopNoise("water", 6f, 0.07f, t => 0.3f + 0.15f * Mathf.Sin(t * Mathf.PI * 2f * 5f)));
            // музыка: мягкая пэд-подложка
            float len = 12f;
            Fx(new float[] { 220f, 261.63f, 329.63f, 392f });
            music = Src("Музыка", true, Make("pad", len, (t, i) =>
            {
                float s = 0f;
                foreach (var f0 in padNotes)
                {
                    float f = Mathf.Round(f0 * len) / len;
                    s += Mathf.Sin(2f * Mathf.PI * f * t) * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * (t / len) * 2f + f0));
                }
                return s * 0.08f;
            }));
            // эффекты
            stepClip = Make("шаг", 0.14f, (t, i) => Noise(0.25f, i) * Mathf.Exp(-t * 28f) * 0.7f);
            whoosh = Make("заброс", 0.55f, (t, i) => Noise(0.1f + 0.4f * t, i) * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.55f)) * 0.8f);
            splash = Make("плеск", 0.5f, (t, i) => (Noise(0.3f, i) * Mathf.Exp(-t * 9f) + Mathf.Sin(2f * Mathf.PI * (260f - 200f * t) * t) * Mathf.Exp(-t * 12f) * 0.5f) * 0.9f);
            bite = Make("поклёвка", 0.7f, (t, i) =>
                (Mathf.Sin(2f * Mathf.PI * 880f * t) * Mathf.Exp(-t * 6f) * 0.4f + Mathf.Sin(2f * Mathf.PI * 1320f * t) * Mathf.Exp(-t * 7f) * 0.25f
                + (t < 0.3f ? Noise(0.2f, i) * Mathf.Exp(-t * 14f) * 0.5f : 0f)));
            catchClip = Make("поимка", 1.1f, (t, i) => Arp(t, new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.14f));
            failClip = Make("сход", 0.8f, (t, i) => Mathf.Sin(2f * Mathf.PI * (400f - 220f * t) * t) * Mathf.Exp(-t * 4f) * 0.5f);
            click = Make("клик", 0.06f, (t, i) => Mathf.Sin(2f * Mathf.PI * 1200f * t) * Mathf.Exp(-t * 80f) * 0.4f);
            levelUp = Make("уровень", 1.3f, (t, i) => Arp(t, new[] { 392f, 523.25f, 659.25f, 783.99f, 1046.5f }, 0.12f));
            fx = Src("Эффекты", false, null);
            // катушка (щёлканье)
            reel = Src("Катушка", true, Make("катушка", 0.4f, (t, i) => (Mathf.Repeat(t, 0.05f) < 0.004f ? 0.5f * Noise(0.9f, i) : 0f) + Mathf.Sin(2f * Mathf.PI * 150f * t) * 0.03f));
            // птицы
            for (int k = 0; k < 4; k++) birds.Add(MakeBird(k));
            wind.Play(); water.Play(); music.Play();
            ApplyVolumes();
        }

        float[] padNotes;
        void Fx(float[] n) { padNotes = n; }

        float Noise(float lp, int i) { return R() * lp * 0.6f + R() * (1f - lp) * 0.25f; }

        float Arp(float t, float[] notes, float step)
        {
            float s = 0f;
            for (int k = 0; k < notes.Length; k++)
            {
                float tt = t - k * step;
                if (tt < 0f) continue;
                s += Mathf.Sin(2f * Mathf.PI * notes[k] * tt) * Mathf.Exp(-tt * 4.5f) * 0.28f;
            }
            return s;
        }

        AudioClip MakeBird(int k)
        {
            int chirps = 3 + k % 3; float baseF = 2400f + 450f * k;
            return Make("птица" + k, 1.2f, (t, i) =>
            {
                float s = 0f;
                for (int c = 0; c < chirps; c++)
                {
                    float tt = t - c * (0.17f + 0.02f * k);
                    if (tt < 0f || tt > 0.11f) continue;
                    float f = baseF + 1600f * tt * (c % 2 == 0 ? 1f : -1f) * 3f;
                    s += Mathf.Sin(2f * Mathf.PI * f * tt) * Mathf.Sin(Mathf.PI * tt / 0.11f) * 0.25f;
                }
                return s;
            });
        }

        public void ApplyVolumes()
        {
            if (Game.S == null) return;
            float m = Game.S.volMaster;
            if (wind != null) wind.volume = 0.5f * Game.S.volEffects * m;
            if (water != null) water.volume = 0.35f * Game.S.volEffects * m;
            if (music != null) music.volume = Game.S.volMusic * m * 0.8f;
            if (fx != null) fx.volume = Game.S.volEffects * m;
            if (reel != null) reel.volume = 0.5f * Game.S.volEffects * m;
        }

        void Update()
        {
            birdTimer -= Time.deltaTime;
            if (birdTimer <= 0f && fx != null && birds.Count > 0)
            {
                birdTimer = Random.Range(4f, 11f);
                P(birds[Random.Range(0, birds.Count)], 0.35f);
            }
        }

        void P(AudioClip c, float v) { if (fx != null && c != null) fx.PlayOneShot(c, v); }

        void OnDestroy()
        {
            if (I == this) I = null;
            if (reel != null) reel.Stop();
        }

        void OnApplicationPause(bool paused) { AudioListener.pause = paused; }

        public void Step(Vector3 p) { P(stepClip, 0.5f); }
        public void Cast() { P(whoosh, 0.7f); }
        public void Splash() { P(splash, 0.8f); }
        public void Bite() { P(bite, 1f); }
        public void Catch() { P(catchClip, 0.9f); }
        public void Fail() { P(failClip, 0.9f); }
        public void Click() { P(click, 0.7f); }
        public void LevelUp() { P(levelUp, 0.9f); }
        public void Reel(bool on, float pitch = 1f)
        {
            if (reel == null) return;
            if (on) { reel.pitch = Mathf.Clamp(pitch, 0.5f, 2f); if (!reel.isPlaying) reel.Play(); }
            else if (reel.isPlaying) reel.Stop();
        }
    }
}

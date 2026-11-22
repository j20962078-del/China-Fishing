using UnityEngine;

namespace ChinaFishing
{
    // Точка входа: единственный объект в сцене. Всё остальное создаётся отсюда в строгом порядке.
    public class GameManager : MonoBehaviour
    {
        public static GameManager I;
        public PlayerController player;
        public CameraRig rig;
        public FishingController fishing;
        public WorldBuilder world;
        public GameUI ui;
        public Camera cam;
        public bool InMenu = true;
        public bool Ready;
        float menuAngle = 200f, saveTimer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { I = null; Game.ResetStatics(); }

        void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; } // второй GameManager не нужен
            I = this;
            Application.runInBackground = false;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Screen.autorotateToPortrait = false; Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true; Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;
            Time.timeScale = 1f;
        }

        void OnDestroy() { if (I == this) I = null; }

        void Start()
        {
            if (I != this) return;
            try { Init(); }
            catch (System.Exception e) { Debug.LogError("China Fishing: ошибка запуска: " + e); }
        }

        // Порядок: сохранение → интерфейсный набор → звук → качество → мир → камера → игрок → рыбалка → интерфейс
        void Init()
        {
            Game.Load();
            UIKit.Init();
            AudioManager.Create();
            Game.ApplyQuality();

            world = new GameObject("Строитель мира").AddComponent<WorldBuilder>();
            world.Build();
            Game.ApplyQuality(); // теперь есть ландшафт — применить настройки травы

            cam = Camera.main;
            if (cam == null)
            {
                var cg = new GameObject("Камера"); cg.tag = "MainCamera";
                cam = cg.AddComponent<Camera>();
            }
            if (FindFirstObjectByType<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();
            cam.fieldOfView = 60f;
            rig = cam.gameObject.GetComponent<CameraRig>();
            if (rig == null) rig = cam.gameObject.AddComponent<CameraRig>();

            var pg = new GameObject("Игрок");
            pg.AddComponent<CharacterController>();
            player = pg.AddComponent<PlayerController>();
            player.cam = rig; rig.target = pg.transform;

            Physics.SyncTransforms();
            Vector3 start = world.spawn;
            var s = Game.S;
            Vector3 saved;
            if (s.hasPos && SafePosition(new Vector3(s.posX, s.posY, s.posZ), out saved)) start = saved;
            player.Teleport(start, 0f);
            rig.yaw = 0f; rig.Snap();

            fishing = new GameObject("Рыбалка").AddComponent<FishingController>();
            fishing.player = player; fishing.cam = rig;

            ui = new GameObject("Интерфейс").AddComponent<GameUI>();
            ui.Build();
            rig.enabled = false;
            Ready = true;
            ToMenu(true);
        }

        // Сохранённая позиция годится, только если под ногами твёрдая земля, не глубокая вода и нет коллизии
        bool SafePosition(Vector3 p, out Vector3 res)
        {
            res = p;
            if (Mathf.Abs(p.x) > 140f || Mathf.Abs(p.z) > 140f) return false;
            RaycastHit hit;
            if (!Physics.Raycast(new Vector3(p.x, p.y + 2f, p.z), Vector3.down, out hit, 10f)) return false;
            if (hit.collider is TerrainCollider && WorldBuilder.H(p.x, p.z) < -0.7f) return false;
            res = hit.point + Vector3.up * 0.15f;
            if (Physics.CheckCapsule(res + Vector3.up * 0.4f, res + Vector3.up * 1.4f, 0.3f)) return false;
            return true;
        }

        public void Play()
        {
            if (!Ready) return;
            InMenu = false;
            Time.timeScale = 1f;
            rig.enabled = true; rig.Snap();
            Inp.UiBlocking = false; Inp.LookDelta = Vector2.zero;
            ui.ShowHud();
        }

        public void ToMenu(bool first = false)
        {
            if (!Ready && !first) return;
            if (!first)
            {
                if (fishing != null && fishing.state == FState.Итог) return; // сначала решить судьбу улова
                if (fishing != null && fishing.state != FState.Нет) fishing.StopFishing();
                SavePosition();
            }
            Time.timeScale = 1f;
            InMenu = true; rig.enabled = false; Inp.UiBlocking = true; Inp.Move = Vector2.zero; Inp.LookDelta = Vector2.zero;
            ui.ShowMenu();
        }

        public void SavePosition()
        {
            if (player == null || Game.S == null) return;
            var p = player.transform.position;
            Game.S.posX = p.x; Game.S.posY = p.y; Game.S.posZ = p.z; Game.S.hasPos = true;
            Game.Save();
        }

        void Update()
        {
            if (!Ready) return;
            if (InMenu && cam != null && world != null)
            {
                menuAngle += Time.deltaTime * 3.5f;
                float a = menuAngle * Mathf.Deg2Rad;
                var c = new Vector3(0, 0, 45f);
                var p = c + new Vector3(Mathf.Sin(a) * 92f, 0, Mathf.Cos(a) * 62f);
                p.y = Mathf.Max(11f, world.Ground(p.x, p.z) + 7f);
                cam.transform.position = p;
                cam.transform.rotation = Quaternion.LookRotation(new Vector3(0, -1.5f, 40f) - p, Vector3.up);
            }
            saveTimer += Time.unscaledDeltaTime;
            if (saveTimer > 20f && !InMenu) { saveTimer = 0; SavePosition(); }

            // зум двумя пальцами: оба пальца вне зоны джойстика (левые 40% экрана)
            Inp.Pinching = false;
            if (!InMenu && !Inp.UiBlocking && Input.touchCount == 2)
            {
                var a = Input.GetTouch(0); var b = Input.GetTouch(1);
                float lim = Screen.width * 0.4f;
                if (a.position.x > lim && b.position.x > lim)
                {
                    Inp.Pinching = true;
                    float prev = ((a.position - a.deltaPosition) - (b.position - b.deltaPosition)).magnitude;
                    float cur = (a.position - b.position).magnitude;
                    Inp.Zoom += (cur - prev) / Mathf.Max(1f, Screen.dpi > 0 ? Screen.dpi : 300f) * 6f;
                }
            }

            // кнопка «Назад» на Android
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (ui != null && ui.HasPanel) ui.Back();
                else if (fishing != null && fishing.state == FState.Итог) { }
                else if (!InMenu) ToMenu();
                else Application.Quit();
            }
        }

        void OnApplicationPause(bool paused) { if (paused) SaveAll(); }
        void OnApplicationFocus(bool focus) { if (!focus) SaveAll(); }
        void OnApplicationQuit() { SaveAll(); }

        void SaveAll()
        {
            if (!Ready || Game.S == null) return;
            if (!InMenu) SavePosition(); else Game.Save();
        }
    }
}

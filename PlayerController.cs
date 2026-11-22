using UnityEngine;

namespace ChinaFishing
{
    // Общее состояние ввода (джойстик слева, свайп по экрану для камеры)
    public static class Inp
    {
        public static Vector2 Move;       // x: вправо, y: вперёд
        public static Vector2 LookDelta;  // пиксели за кадр, накапливается LookPad
        public static float Zoom;
        public static bool Pinching;      // идёт зум двумя пальцами — камеру не вращаем
        public static bool UiBlocking;    // открыто полноэкранное меню
    }

    // Процедурная модель персонажа с ногами, руками и удочкой
    public class PlayerModel
    {
        public Transform root, legL, legR, armL, armR, torso, head, rodTip, rod;
        public GameObject fishHold;

        static Transform Pivot(Transform parent, string n, Vector3 p)
        {
            var g = new GameObject(n).transform; g.SetParent(parent, false); g.localPosition = p; return g;
        }

        public static PlayerModel Build(Transform parent)
        {
            var m = new PlayerModel();
            m.root = new GameObject("Модель").transform; m.root.SetParent(parent, false);
            var skin = Mats.Lit(225, 180, 150, 0.2f);
            var jacket = Mats.Lit(70, 100, 60, 0.1f);
            var vest = Mats.Lit(190, 150, 70, 0.1f);
            var pants = Mats.Lit(60, 70, 95, 0.1f);
            var boot = Mats.Lit(45, 35, 28, 0.1f);
            var hat = Mats.Lit(200, 190, 140, 0.1f);

            m.torso = Pivot(m.root, "Торс", new Vector3(0, 1.28f, 0));
            WorldBuilder.Prim(PrimitiveType.Capsule, m.torso, Vector3.zero, new Vector3(0.40f, 0.30f, 0.26f), jacket);
            WorldBuilder.Prim(PrimitiveType.Cube, m.torso, new Vector3(0, -0.02f, 0.11f), new Vector3(0.40f, 0.36f, 0.05f), vest);
            WorldBuilder.Prim(PrimitiveType.Cube, m.torso, new Vector3(0, -0.3f, 0), new Vector3(0.38f, 0.22f, 0.24f), pants);
            m.head = Pivot(m.torso, "Голова", new Vector3(0, 0.42f, 0));
            WorldBuilder.Prim(PrimitiveType.Sphere, m.head, Vector3.zero, new Vector3(0.23f, 0.26f, 0.24f), skin);
            WorldBuilder.Prim(PrimitiveType.Cylinder, m.head, new Vector3(0, 0.10f, 0), new Vector3(0.27f, 0.045f, 0.27f), hat);
            WorldBuilder.Prim(PrimitiveType.Cylinder, m.head, new Vector3(0, 0.17f, 0), new Vector3(0.20f, 0.06f, 0.20f), hat);
            WorldBuilder.Prim(PrimitiveType.Sphere, m.head, new Vector3(-0.05f, 0.02f, 0.10f), new Vector3(0.03f, 0.03f, 0.03f), Mats.Lit(30, 30, 30, 0.3f));
            WorldBuilder.Prim(PrimitiveType.Sphere, m.head, new Vector3(0.05f, 0.02f, 0.10f), new Vector3(0.03f, 0.03f, 0.03f), Mats.Lit(30, 30, 30, 0.3f));

            foreach (int s in new[] { -1, 1 })
            {
                var leg = Pivot(m.root, s < 0 ? "Нога Л" : "Нога П", new Vector3(0.12f * s, 0.95f, 0));
                WorldBuilder.Prim(PrimitiveType.Capsule, leg, new Vector3(0, -0.42f, 0), new Vector3(0.17f, 0.44f, 0.17f), pants);
                WorldBuilder.Prim(PrimitiveType.Cube, leg, new Vector3(0, -0.88f, 0.05f), new Vector3(0.19f, 0.15f, 0.31f), boot);
                var arm = Pivot(m.root, s < 0 ? "Рука Л" : "Рука П", new Vector3(0.27f * s, 1.5f, 0));
                WorldBuilder.Prim(PrimitiveType.Capsule, arm, new Vector3(0, -0.28f, 0), new Vector3(0.11f, 0.29f, 0.11f), jacket);
                WorldBuilder.Prim(PrimitiveType.Sphere, arm, new Vector3(0, -0.60f, 0), new Vector3(0.1f, 0.1f, 0.1f), skin);
                if (s < 0) { m.legL = leg; m.armL = arm; } else { m.legR = leg; m.armR = arm; }
            }

            // удочка в правой руке
            m.rod = Pivot(m.armR, "Удочка", new Vector3(0, -0.6f, 0.02f));
            var rodGo = WorldBuilder.Prim(PrimitiveType.Cylinder, m.rod, Vector3.zero, new Vector3(0.022f, 1.1f, 0.022f), Mats.Lit(60, 45, 30, 0.3f));
            m.rod.localRotation = Quaternion.Euler(123f, 0, 0);
            rodGo.transform.localPosition = new Vector3(0, 0.9f, 0);
            WorldBuilder.Prim(PrimitiveType.Cylinder, m.rod, new Vector3(0, 0.25f, 0.05f), new Vector3(0.09f, 0.03f, 0.09f), Mats.Lit(150, 150, 160, 0.6f), false, new Vector3(0, 0, 90));
            m.rodTip = Pivot(m.rod, "Кончик удочки", new Vector3(0, 2.0f, 0));
            return m;
        }
    }

    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public static PlayerController I;
        public PlayerModel model;
        public CameraRig cam;
        CharacterController cc;
        float vy, walkPhase, speedSmooth;
        public bool frozen;           // во время рыбалки
        public bool fishingPose;
        public float castAnim = -1f;  // 0..1 во время заброса, -1 если нет
        public float fightShake;
        Vector3 faceDir = Vector3.forward;
        float footTimer;

        public const float WalkSpeed = 3.6f;

        void Awake()
        {
            I = this;
            cc = GetComponent<CharacterController>();
            cc.height = 1.8f; cc.radius = 0.32f; cc.center = new Vector3(0, 0.9f, 0);
            cc.stepOffset = 0.45f; cc.slopeLimit = 52f; cc.skinWidth = 0.04f;
            model = PlayerModel.Build(transform);
        }

        public void Teleport(Vector3 p, float yaw)
        {
            cc.enabled = false;
            transform.position = p;
            transform.rotation = Quaternion.Euler(0, yaw, 0);
            faceDir = transform.forward;
            cc.enabled = true;
            vy = 0;
        }

        public void FaceYaw(float yaw)
        {
            faceDir = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
        }

        bool WaterBlocked(Vector3 next)
        {
            if (WorldBuilder.H(next.x, next.z) > -0.85f) return false;
            RaycastHit hit;
            if (Physics.Raycast(new Vector3(next.x, transform.position.y + 1.5f, next.z), Vector3.down, out hit, 8f))
                return hit.collider is TerrainCollider; // мостки или мост — можно, дно — нельзя
            return true;
        }

        void Update()
        {
            if (WorldBuilder.I == null || WorldBuilder.I.terrain == null) return;
            Vector2 mv = (frozen || Inp.UiBlocking) ? Vector2.zero : Inp.Move;
            float yaw = cam != null ? cam.yaw : 0f;
            Vector3 dir = Quaternion.Euler(0, yaw, 0) * new Vector3(mv.x, 0, mv.y);
            float mag = Mathf.Clamp01(mv.magnitude);
            float targetSpeed = mag * WalkSpeed;
            speedSmooth = Mathf.MoveTowards(speedSmooth, targetSpeed, 14f * Time.deltaTime);

            Vector3 move = Vector3.zero;
            if (mag > 0.05f)
            {
                var d = dir.normalized;
                faceDir = Vector3.Slerp(faceDir, d, 1f - Mathf.Exp(-12f * Time.deltaTime));
                var mvDir = d;
                var here = transform.position;
                if (WaterBlocked(here + mvDir * 0.55f))
                {
                    // скольжение вдоль берега: пробуем только одну из осей
                    var dx = new Vector3(d.x, 0, 0); var dz = new Vector3(0, 0, d.z);
                    if (Mathf.Abs(d.x) > 0.2f && !WaterBlocked(here + dx.normalized * 0.55f)) mvDir = dx.normalized * Mathf.Abs(d.x);
                    else if (Mathf.Abs(d.z) > 0.2f && !WaterBlocked(here + dz.normalized * 0.55f)) mvDir = dz.normalized * Mathf.Abs(d.z);
                    else mvDir = Vector3.zero;
                }
                move = mvDir * speedSmooth;
            }
            else speedSmooth = Mathf.MoveTowards(speedSmooth, 0, 20f * Time.deltaTime);

            if (cc.isGrounded && vy < 0) vy = -2f; else vy -= 20f * Time.deltaTime;
            move.y = vy;
            cc.Move(move * Time.deltaTime);

            var p = transform.position;
            p.x = Mathf.Clamp(p.x, -145f, 145f); p.z = Mathf.Clamp(p.z, -145f, 145f);
            if (p.x != transform.position.x || p.z != transform.position.z) { cc.enabled = false; transform.position = p; cc.enabled = true; }
            float gy = WorldBuilder.I.Ground(p.x, p.z);
            if (p.y < gy - 0.8f && gy > -1.5f) { Teleport(new Vector3(p.x, gy + 0.6f, p.z), transform.eulerAngles.y); } // провалился под землю
            else if (p.y < -4f) Teleport(WorldBuilder.I.spawn, 0f);                                                          // упал в воду/пустоту

            if (faceDir.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(faceDir, Vector3.up);

            Animate(mag);
            if (speedSmooth > 0.5f)
            {
                footTimer -= Time.deltaTime * speedSmooth;
                if (footTimer <= 0f) { footTimer = 1.6f; AudioManager.I?.Step(transform.position); }
            }
        }

        void Animate(float mag)
        {
            var m = model;
            float sp = speedSmooth / WalkSpeed;
            walkPhase += Time.deltaTime * (4f + 6f * sp) * (sp > 0.05f ? 1f : 0f);
            float swing = Mathf.Sin(walkPhase) * 38f * sp;
            m.legL.localRotation = Quaternion.Euler(swing, 0, 0);
            m.legR.localRotation = Quaternion.Euler(-swing, 0, 0);
            m.torso.localPosition = new Vector3(0, 1.28f + Mathf.Abs(Mathf.Sin(walkPhase)) * 0.03f * sp + Mathf.Sin(Time.time * 1.6f) * 0.004f, 0);
            m.torso.localRotation = Quaternion.Euler(0, Mathf.Sin(walkPhase) * 4f * sp, 0);
            m.armL.localRotation = Quaternion.Euler(-swing * 0.8f, 0, 4f);

            float armA;
            if (castAnim >= 0f)
            {
                float t = castAnim;
                armA = t < 0.4f ? Mathf.Lerp(-70f, -150f, t / 0.4f) : Mathf.Lerp(-150f, -45f, (t - 0.4f) / 0.6f);
            }
            else if (fishingPose) armA = -62f + Mathf.Sin(Time.time * 2f) * 1.5f + Mathf.Sin(Time.time * 40f) * fightShake * 4f;
            else armA = -35f + swing * 0.8f;
            m.armR.localRotation = Quaternion.Euler(armA, 0, -4f);
            m.rod.gameObject.SetActive(true);
        }
    }

    // Камера от третьего лица: свайп вращает, не проходит сквозь объекты, не переворачивается
    public class CameraRig : MonoBehaviour
    {
        public Transform target;
        public float yaw = 0f, pitch = 22f, dist = 5.2f;
        float curDist = 5.2f;
        Camera cam;
        readonly RaycastHit[] hits = new RaycastHit[12];
        Vector3 smoothPivot;
        public bool fishingView;

        void Awake()
        {
            cam = GetComponent<Camera>();
            cam.nearClipPlane = 0.15f;
            cam.farClipPlane = Game.ViewDistance;
            cam.clearFlags = RenderSettings.skybox != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.55f, 0.72f, 0.9f);
        }

        public void Snap()
        {
            smoothPivot = target.position + Vector3.up * 1.55f;
        }

        void LateUpdate()
        {
            if (target == null) return;
            cam.farClipPlane = Game.ViewDistance;
            var d = (Inp.UiBlocking || Inp.Pinching) ? Vector2.zero : Inp.LookDelta;
            Inp.LookDelta = Vector2.zero;
            float sens = 0.16f * (Screen.dpi > 0 ? Mathf.Clamp(300f / Screen.dpi, 0.6f, 1.6f) : 1f);
            yaw += d.x * sens;
            pitch = Mathf.Clamp(pitch - d.y * sens, -8f, 62f);
            if (Mathf.Abs(Inp.Zoom) > 0.001f) { dist = Mathf.Clamp(dist - Inp.Zoom, 2.6f, 9f); Inp.Zoom = 0; }

            var pivotTarget = target.position + Vector3.up * 1.55f;
            smoothPivot = Vector3.Lerp(smoothPivot, pivotTarget, 1f - Mathf.Exp(-18f * Time.deltaTime));
            var rot = Quaternion.Euler(pitch, yaw, 0);
            var back = rot * Vector3.back;
            float want = fishingView ? Mathf.Min(dist, 4.2f) : dist;
            float allowed = want;
            int n = Physics.SphereCastNonAlloc(smoothPivot, 0.22f, back, hits, want, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var h = hits[i];
                if (h.collider.transform.IsChildOf(target)) continue;
                if (h.distance < allowed) allowed = Mathf.Max(0.6f, h.distance);
            }
            curDist = allowed < curDist ? allowed : Mathf.Lerp(curDist, allowed, 1f - Mathf.Exp(-6f * Time.deltaTime));
            var pos = smoothPivot + back * curDist;
            if (WorldBuilder.I != null && WorldBuilder.I.terrain != null)
            {
                float g = WorldBuilder.I.Ground(pos.x, pos.z) + 0.35f;
                if (pos.y < g) pos.y = g;
            }
            transform.SetPositionAndRotation(pos, Quaternion.LookRotation(smoothPivot - pos, Vector3.up));
        }
    }
}

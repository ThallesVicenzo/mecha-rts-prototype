using MechaRts.Sim;
using UnityEngine;

namespace MechaRts.Game
{
    /// <summary>
    /// Playable top-down combat slice. Press Play on any scene.
    /// Simulation is MechaRts.Sim.BattleWorld; this class only reads input and draws.
    /// </summary>
    public sealed class BattleDriver : MonoBehaviour
    {
        public const int DefaultSeed = 12345;

        BattleWorld _world;
        BattleView _view;
        Camera _camera;
        float _accumulator;
        float _fps = 60f;
        bool _paused;
        bool _stylesReady;
        GUIStyle _body;
        GUIStyle _title;
        GUIStyle _small;
        GUIStyle _center;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoStart()
        {
            if (FindAnyObjectByType<BattleDriver>() != null)
                return;
            var go = new GameObject("Battle");
            go.AddComponent<BattleDriver>();
        }

        void Awake()
        {
            Application.targetFrameRate = 60;
            _world = new BattleWorld(DefaultSeed);
            _view = new BattleView();
            _camera = _view.Build(_world);
            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] != _camera)
                    cameras[i].gameObject.SetActive(false);
            }
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                _paused = !_paused;
            if (Input.GetKeyDown(KeyCode.R))
            {
                _world.Reset(DefaultSeed);
                _paused = false;
                _accumulator = 0f;
            }
            if (Input.GetKeyDown(KeyCode.C))
                _world.ClearRally();

            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
                _camera.orthographicSize = Mathf.Clamp(_camera.orthographicSize - scroll * 0.8f, 8f, 16.5f);

            BattleCommand command = ReadCommand();
            bool simulate = !_paused && _world.Outcome == BattleOutcome.Ongoing;
            if (simulate)
            {
                _accumulator += Time.deltaTime;
                int steps = 0;
                while (_accumulator >= BattleWorld.TickDt && steps < 5)
                {
                    _world.Tick(BattleWorld.TickDt, command);
                    command.Dash = false;
                    command.DeployRifle = false;
                    command.DeployHeavy = false;
                    _accumulator -= BattleWorld.TickDt;
                    steps++;
                }
            }

            _view.Sync(_world, Time.deltaTime);
            _fps = Mathf.Lerp(_fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f), 0.08f);
        }

        BattleCommand ReadCommand()
        {
            float x = Input.GetAxisRaw("Horizontal");
            float y = Input.GetAxisRaw("Vertical");
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) x = -1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x = 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) y = -1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) y = 1f;
            float mag = Mathf.Sqrt(x * x + y * y);
            if (mag > 1f)
            {
                x /= mag;
                y /= mag;
            }

            Vector3 mouse = Input.mousePosition;
            mouse.z = 10f;
            Vector3 world = _camera.ScreenToWorldPoint(mouse);

            if (Input.GetMouseButtonDown(1))
                _world.SetRally(world.x, world.y);

            return new BattleCommand
            {
                MoveX = x,
                MoveY = y,
                AimX = world.x,
                AimY = world.y,
                Fire = Input.GetMouseButton(0) || Input.GetKey(KeyCode.Space),
                Dash = Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift),
                DeployRifle = Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1),
                DeployHeavy = Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)
            };
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint)
                return;
            EnsureStyles();

            float w = Screen.width;
            float h = Screen.height;
            DrawRect(new Rect(0f, 0f, w, 86f), new Color(0f, 0f, 0f, 0.28f));
            GUI.Label(new Rect(0f, 10f, w, 34f), "PROTÓTIPO  ·  COMBATE EM TEMPO REAL", _title);
            GUI.Label(new Rect(0f, 44f, w, 24f), "Destrua o mecha hostil. As tropas lutam sozinhas — você escolhe quando mobilizar e para onde avançam.", _center);

            DrawForceCard(18f, 96f, true);
            DrawForceCard(w - 338f, 96f, false);
            DrawNotice();
            DrawOutcome(w, h);
            DrawHelp(w, h);
        }

        void DrawForceCard(float x, float y, bool player)
        {
            Mecha mecha = player ? _world.Player : _world.Enemy;
            int rifles = 0;
            int heavies = 0;
            for (int i = 0; i < _world.Troops.Count; i++)
            {
                Troop troop = _world.Troops[i];
                if (troop.Team != mecha.Team)
                    continue;
                if (troop.Kind == TroopKind.Heavy) heavies++;
                else rifles++;
            }

            DrawRect(new Rect(x, y, 320f, player ? 184f : 118f), new Color(0.04f, 0.05f, 0.07f, 0.78f));
            string name = player ? "SEU MECHA" : "MECHA HOSTIL";
            GUI.Label(new Rect(x + 12f, y + 8f, 296f, 22f), name, _body);
            float pct = Mathf.Clamp01(mecha.Hp / BattleTuning.MechaHp);
            DrawRect(new Rect(x + 12f, y + 34f, 296f, 14f), new Color(0.12f, 0.13f, 0.16f, 1f));
            Color fill = player ? new Color(0.38f, 0.86f, 1f) : new Color(1f, 0.38f, 0.32f);
            DrawRect(new Rect(x + 12f, y + 34f, 296f * pct, 14f), fill);
            GUI.Label(new Rect(x + 12f, y + 50f, 296f, 20f), Mathf.CeilToInt(Mathf.Max(0f, mecha.Hp)) + " / " + Mathf.CeilToInt(BattleTuning.MechaHp), _small);
            GUI.Label(new Rect(x + 12f, y + 70f, 296f, 20f), "Infantaria " + rifles + "     Pesada " + heavies, _small);

            if (!player)
                return;

            GUI.Label(new Rect(x + 12f, y + 92f, 296f, 20f), "Mobilização  " + Mathf.FloorToInt(mecha.DeployPoints) + " / " + Mathf.FloorToInt(BattleTuning.MaxDeploy), _small);
            DrawRect(new Rect(x + 12f, y + 114f, 296f, 8f), new Color(0.12f, 0.13f, 0.16f, 1f));
            DrawRect(new Rect(x + 12f, y + 114f, 296f * Mathf.Clamp01(mecha.DeployPoints / BattleTuning.MaxDeploy), 8f), new Color(0.95f, 0.78f, 0.28f));
            string rifle = "[1] Infantaria " + Mathf.FloorToInt(BattleTuning.RifleCost);
            string heavy = "[2] Pesada " + Mathf.FloorToInt(BattleTuning.HeavyCost);
            Color rifleColor = mecha.DeployPoints >= BattleTuning.RifleCost ? new Color(0.7f, 0.95f, 0.7f) : new Color(0.55f, 0.55f, 0.58f);
            Color heavyColor = mecha.DeployPoints >= BattleTuning.HeavyCost ? new Color(0.7f, 0.95f, 0.7f) : new Color(0.55f, 0.55f, 0.58f);
            var previous = GUI.color;
            GUI.color = rifleColor;
            GUI.Label(new Rect(x + 12f, y + 128f, 150f, 22f), rifle, _small);
            GUI.color = heavyColor;
            GUI.Label(new Rect(x + 160f, y + 128f, 150f, 22f), heavy, _small);
            GUI.color = previous;

            float dash = mecha.DashCooldown <= 0f ? 1f : 1f - Mathf.Clamp01(mecha.DashCooldown / BattleTuning.DashCooldown);
            GUI.color = previous;
            GUI.Label(new Rect(x + 12f, y + 146f, 200f, 18f), "Investida", _small);
            DrawRect(new Rect(x + 90f, y + 152f, 120f, 6f), new Color(0.12f, 0.13f, 0.16f, 1f));
            DrawRect(new Rect(x + 90f, y + 152f, 120f * dash, 6f), new Color(0.75f, 0.88f, 1f));
        }

        void DrawNotice()
        {
            if (_world.NoticeTime <= 0f || _world.Notice == BattleNotice.None)
                return;
            string text = _world.Notice == BattleNotice.NeedPoints ? "Mobilização insuficiente"
                : _world.Notice == BattleNotice.TroopCap ? "Limite de tropas no campo"
                : "Esquadrão mobilizado";
            var rect = new Rect(Screen.width * 0.5f - 180f, Screen.height - 132f, 360f, 28f);
            DrawRect(rect, new Color(0.04f, 0.05f, 0.07f, 0.8f));
            GUI.Label(rect, text, _center);
        }

        void DrawOutcome(float w, float h)
        {
            string banner = null;
            if (_paused && _world.Outcome == BattleOutcome.Ongoing)
                banner = "PAUSADO";
            else if (_world.Outcome == BattleOutcome.Victory)
                banner = "MECHA HOSTIL DESTRUÍDO";
            else if (_world.Outcome == BattleOutcome.Defeat)
                banner = "SEU MECHA CAIU";
            else if (_world.Outcome == BattleOutcome.Draw)
                banner = "OS DOIS MECHAS CAÍRAM";
            if (banner == null)
                return;

            DrawRect(new Rect(0f, h * 0.36f, w, 110f), new Color(0f, 0f, 0f, 0.72f));
            GUI.Label(new Rect(0f, h * 0.36f + 18f, w, 40f), banner, _title);
            string hint = _world.Outcome == BattleOutcome.Ongoing ? "Esc para continuar" : "R para lutar de novo";
            GUI.Label(new Rect(0f, h * 0.36f + 62f, w, 28f), hint, _center);
        }

        void DrawHelp(float w, float h)
        {
            DrawRect(new Rect(0f, h - 78f, w, 78f), new Color(0f, 0f, 0f, 0.55f));
            GUI.Label(new Rect(16f, h - 70f, w - 32f, 22f), "WASD mover    mouse mirar    clique esquerdo / espaço atirar    Shift investida", _center);
            GUI.Label(new Rect(16f, h - 46f, w - 32f, 22f), "Clique direito: avanço das tropas    C limpa    roda do mouse: zoom    Esc pausa    R reiniciar", _center);
            GUI.Label(new Rect(16f, h - 24f, w - 32f, 20f),
                Mathf.RoundToInt(_fps) + " fps    seed " + _world.Seed + "    " + _world.Time.ToString("0.0") + "s    tropas " + _world.CountTroops(0) + " / " + _world.CountTroops(1),
                _center);
        }

        void EnsureStyles()
        {
            if (_stylesReady)
                return;
            _body = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
            _body.normal.textColor = Color.white;
            _small = new GUIStyle(GUI.skin.label) { fontSize = 13 };
            _small.normal.textColor = new Color(0.82f, 0.84f, 0.88f);
            _title = new GUIStyle(_body) { fontSize = 26, alignment = TextAnchor.MiddleCenter };
            _center = new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter };
            _center.normal.textColor = new Color(0.9f, 0.92f, 0.94f);
            _stylesReady = true;
        }

        static void DrawRect(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}

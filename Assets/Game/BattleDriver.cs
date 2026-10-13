using MechaRts.Sim;
using UnityEngine;

namespace MechaRts.Game
{
    /// <summary>
    /// Playable combat slice. Ground layer is the RTS fight; hover lifts the mecha over cover.
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
        }

        void StartFight(BattleMode mode)
        {
            _view?.Dispose();
            _world = new BattleWorld(DefaultSeed, mode);
            _view = new BattleView();
            _camera = _view.Build(_world);
            Camera[] cameras = FindObjectsByType<Camera>();
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] != _camera)
                    cameras[i].gameObject.SetActive(false);
            }
            _paused = false;
            _accumulator = 0f;
        }

        void LeaveToMenu()
        {
            _view?.Dispose();
            _view = null;
            _world = null;
            _camera = null;
            _paused = false;
            _accumulator = 0f;
        }

        void Update()
        {
            if (_world == null)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                return;
            }

            bool rival = _world.Mode == BattleMode.Duel;
            bool lockCursor = rival && !_paused && _world.Outcome == BattleOutcome.Ongoing;
            Cursor.lockState = lockCursor ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !lockCursor;

            if (Input.GetKeyDown(KeyCode.M))
            {
                LeaveToMenu();
                return;
            }
            if (Input.GetKeyDown(KeyCode.Escape))
                _paused = !_paused;
            if (Input.GetKeyDown(KeyCode.R))
            {
                BattleMode mode = _world.Mode;
                _view.Dispose();
                _world = new BattleWorld(DefaultSeed, mode);
                _view = new BattleView();
                _camera = _view.Build(_world);
                _paused = false;
                _accumulator = 0f;
            }
            if (_world.Mode == BattleMode.Skirmish && Input.GetKeyDown(KeyCode.C))
                _world.ClearRally();

            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
                _view.AddZoom(-scroll * 0.45f);
            if (_world.Mode != BattleMode.Colossus)
            {
                if (Input.GetKey(KeyCode.Q))
                    _view.AddYaw(-70f * Time.deltaTime);
                if (Input.GetKey(KeyCode.E))
                    _view.AddYaw(70f * Time.deltaTime);
                if (Input.GetMouseButton(2))
                    _view.AddYaw(Input.GetAxis("Mouse X") * 5f);
            }
            if (_world.Mode == BattleMode.Duel)
                _view.AddYaw(Input.GetAxis("Mouse X") * 2.4f);

            _view.UpdateCamera(_world, Time.deltaTime);

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
                    command.ToggleHover = false;
                    command.Melee = false;
                    command.DeployRifle = false;
                    command.DeployHeavy = false;
                    command.DeployAt = false;
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

            bool skirmish = _world.Mode == BattleMode.Skirmish;
            bool duel = _world.Mode == BattleMode.Duel;
            bool colossus = _world.Mode == BattleMode.Colossus;
            _view.CameraPlanarAxes(out Vector3 right, out Vector3 ahead);
            Vector3 wish = colossus ? new Vector3(x, 0f, y) : right * x + ahead * y;
            float aimX;
            float aimY;
            if (duel)
            {
                aimX = _world.Player.Pos.X + ahead.x * 24f;
                aimY = _world.Player.Pos.Y + ahead.z * 24f;
            }
            else if (colossus)
                _view.TryPickHeight(Input.mousePosition, _world.Enemy.Altitude, out aimX, out aimY);
            else
                _view.TryPickGround(Input.mousePosition, out aimX, out aimY);

            if (skirmish && Input.GetMouseButtonDown(1))
                _world.SetRally(aimX, aimY);

            var command = new BattleCommand
            {
                MoveX = wish.x,
                MoveY = wish.z,
                AimX = aimX,
                AimY = aimY,
                Fire = Input.GetMouseButton(0) || (skirmish && Input.GetKey(KeyCode.Space)),
                Dash = Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift),
                ToggleHover = skirmish && Input.GetKeyDown(KeyCode.F),
                Melee = Input.GetKeyDown(KeyCode.V),
                Climb = duel || colossus
                    ? (Input.GetKey(KeyCode.Space) ? 1f : 0f) + (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) ? -1f : 0f)
                    : 0f
            };

            if (skirmish && _view.TryPickGround(Input.mousePosition, out float dropX, out float dropY) && TryDeployKey(out TroopKind kind))
            {
                command.DeployAt = true;
                command.DeployKind = kind;
                command.DeployX = dropX;
                command.DeployY = dropY;
            }

            return command;
        }

        static bool TryDeployKey(out TroopKind kind)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) { kind = TroopKind.Rifle; return true; }
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) { kind = TroopKind.Cannon; return true; }
            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) { kind = TroopKind.Vanguard; return true; }
            if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) { kind = TroopKind.Heavy; return true; }
            if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5)) { kind = TroopKind.Banner; return true; }
            if (Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Keypad6)) { kind = TroopKind.Medic; return true; }
            kind = TroopKind.Rifle;
            return false;
        }

        void OnGUI()
        {
            EnsureStyles();
            if (_world == null)
            {
                DrawMenu();
                return;
            }

            if (Event.current.type == EventType.Repaint)
            {
                float w = Screen.width;
                float h = Screen.height;
                if (_world.Mode == BattleMode.Skirmish)
                {
                    DrawRect(new Rect(0f, 0f, w, 86f), new Color(0f, 0f, 0f, 0.28f));
                    GUI.Label(new Rect(0f, 10f, w, 34f), "ESCARAMUÇA  ·  RTS", _title);
                    GUI.Label(new Rect(0f, 44f, w, 24f), "Aponte o cursor e aperte 1–6 para soltar o esquadrão. Q/E gira a câmera.", _center);
                    DrawForceCard(18f, 96f, true);
                    DrawForceCard(w - 358f, 96f, false);
                    DrawNotice();
                    DrawHelp(w, h);
                }
                else
                {
                    DrawDuelHud();
                }
            }

            DrawOutcome(Screen.width, Screen.height);
        }

        void DrawMenu()
        {
            float w = Screen.width;
            float h = Screen.height;
            DrawRect(new Rect(0f, 0f, w, h), new Color(0.05f, 0.06f, 0.08f, 1f));
            GUI.Label(new Rect(0f, h * 0.16f, w, 40f), "MECHA  ·  SELECIONE A LUTA", _title);
            GUI.Label(new Rect(w * 0.14f, h * 0.26f, w * 0.72f, 48f), "Escaramuça mistura tropa e mecha. O duelo voa entre os pilares. O colosso fica parado no alto do céu.", _center);

            float bw = 420f;
            float bh = 54f;
            float x = (w - bw) * 0.5f;
            float y = h * 0.4f;
            if (GUI.Button(new Rect(x, y, bw, bh), "Escaramuça RTS"))
                StartFight(BattleMode.Skirmish);
            if (GUI.Button(new Rect(x, y + 70f, bw, bh), "Duelo — Mecha rival"))
                StartFight(BattleMode.Duel);
            if (GUI.Button(new Rect(x, y + 140f, bw, bh), "Duelo — Colosso"))
                StartFight(BattleMode.Colossus);
        }

        void DrawDuelHud()
        {
            float w = Screen.width;
            bool giant = _world.Mode == BattleMode.Colossus;
            GUI.Label(new Rect(0f, 12f, w, 32f), giant ? "BOSS  ·  COLOSSO" : "DUELO  ·  MECHA RIVAL", _title);
            DrawBar(24f, 52f, 360f, _world.Player, "VOCÊ");
            DrawBar(w - 384f, 52f, 360f, _world.Enemy, giant ? "COLOSSO" : "RIVAL");
            if (giant)
                DrawParts(w - 384f, 96f);
            string help = giant
                ? "HD-2D no céu. Boss parado no alto. Acerte as partes brilhantes. WASD desvia, mouse mira."
                : "Altitude " + _world.Player.Altitude.ToString("0.0") + " m     espaço sobe    ctrl desce    V melee    mouse gira";
            GUI.Label(new Rect(0f, giant ? 210f : 92f, w, 22f), help, _center);
        }

        void DrawParts(float x, float y)
        {
            for (int i = 0; i < _world.Parts.Count; i++)
            {
                BossPart part = _world.Parts[i];
                float pct = part.MaxHp <= 0f ? 0f : Mathf.Clamp01(part.Hp / part.MaxHp);
                GUI.Label(new Rect(x, y + i * 22f, 220f, 18f), part.Name, _small);
                DrawRect(new Rect(x + 110f, y + i * 22f + 4f, 180f, 10f), new Color(0.12f, 0.13f, 0.16f, 0.9f));
                Color fill = part.Hp <= 0f ? new Color(0.35f, 0.35f, 0.38f) : new Color(1f, 0.78f, 0.28f);
                DrawRect(new Rect(x + 110f, y + i * 22f + 4f, 180f * pct, 10f), fill);
            }
        }

        void DrawBar(float x, float y, float width, Mecha mecha, string label)
        {
            float max = mecha.MaxHp <= 0f ? 1f : mecha.MaxHp;
            float pct = Mathf.Clamp01(mecha.Hp / max);
            GUI.Label(new Rect(x, y, width, 20f), label + "   " + Mathf.CeilToInt(Mathf.Max(0f, mecha.Hp)) + " / " + Mathf.CeilToInt(max), _small);
            DrawRect(new Rect(x, y + 22f, width, 16f), new Color(0.12f, 0.13f, 0.16f, 0.9f));
            Color fill = mecha.IsPlayer ? new Color(0.38f, 0.86f, 1f) : new Color(1f, 0.38f, 0.32f);
            DrawRect(new Rect(x, y + 22f, width * pct, 16f), fill);
        }

        void DrawForceCard(float x, float y, bool player)
        {
            Mecha mecha = player ? _world.Player : _world.Enemy;
            int[] counts = new int[6];
            for (int i = 0; i < _world.Troops.Count; i++)
            {
                Troop troop = _world.Troops[i];
                if (troop.Team == mecha.Team && troop.Hp > 0f)
                    counts[(int)troop.Kind]++;
            }

            DrawRect(new Rect(x, y, 340f, player ? 248f : 132f), new Color(0.04f, 0.05f, 0.07f, 0.78f));
            string stance = mecha.WantHover ? "VOO" : "CHÃO";
            string name = (player ? "SEU MECHA" : "MECHA HOSTIL") + "   " + stance;
            GUI.Label(new Rect(x + 12f, y + 8f, 316f, 22f), name, _body);
            float maxHp = mecha.MaxHp <= 0f ? BattleTuning.MechaHp : mecha.MaxHp;
            float pct = Mathf.Clamp01(mecha.Hp / maxHp);
            DrawRect(new Rect(x + 12f, y + 34f, 316f, 14f), new Color(0.12f, 0.13f, 0.16f, 1f));
            Color fill = player ? new Color(0.38f, 0.86f, 1f) : new Color(1f, 0.38f, 0.32f);
            DrawRect(new Rect(x + 12f, y + 34f, 316f * pct, 14f), fill);
            GUI.Label(new Rect(x + 12f, y + 50f, 316f, 20f), Mathf.CeilToInt(Mathf.Max(0f, mecha.Hp)) + " / " + Mathf.CeilToInt(maxHp), _small);
            GUI.Label(new Rect(x + 12f, y + 70f, 316f, 20f),
                "ATK " + (counts[0] + counts[1]) + "    TANK " + (counts[2] + counts[3]) + "    SUP " + (counts[4] + counts[5]),
                _small);

            if (!player)
                return;

            GUI.Label(new Rect(x + 12f, y + 94f, 316f, 20f), "Mobilização  " + Mathf.FloorToInt(mecha.DeployPoints) + " / " + Mathf.FloorToInt(BattleTuning.MaxDeploy), _small);
            DrawRect(new Rect(x + 12f, y + 116f, 316f, 8f), new Color(0.12f, 0.13f, 0.16f, 1f));
            DrawRect(new Rect(x + 12f, y + 116f, 316f * Mathf.Clamp01(mecha.DeployPoints / BattleTuning.MaxDeploy), 8f), new Color(0.95f, 0.78f, 0.28f));
            GUI.Label(new Rect(x + 12f, y + 130f, 316f, 18f), SquadLine(1, TroopKind.Rifle, mecha.DeployPoints) + "   " + SquadLine(2, TroopKind.Cannon, mecha.DeployPoints), _small);
            GUI.Label(new Rect(x + 12f, y + 148f, 316f, 18f), SquadLine(3, TroopKind.Vanguard, mecha.DeployPoints) + "   " + SquadLine(4, TroopKind.Heavy, mecha.DeployPoints), _small);
            GUI.Label(new Rect(x + 12f, y + 166f, 316f, 18f), SquadLine(5, TroopKind.Banner, mecha.DeployPoints) + "   " + SquadLine(6, TroopKind.Medic, mecha.DeployPoints), _small);

            float dash = mecha.DashCooldown <= 0f ? 1f : 1f - Mathf.Clamp01(mecha.DashCooldown / BattleTuning.DashCooldown);
            GUI.Label(new Rect(x + 12f, y + 190f, 200f, 18f), "Investida", _small);
            DrawRect(new Rect(x + 90f, y + 196f, 120f, 6f), new Color(0.12f, 0.13f, 0.16f, 1f));
            DrawRect(new Rect(x + 90f, y + 196f, 120f * dash, 6f), new Color(0.75f, 0.88f, 1f));
            GUI.Label(new Rect(x + 12f, y + 214f, 316f, 18f), "Cursor no chão + número. Alcance " + Mathf.FloorToInt(BattleTuning.DeployRange) + " m. V melee.", _small);
        }

        static string SquadLine(int key, TroopKind kind, float points)
        {
            TroopStats stats = TroopCatalog.Get(kind);
            string mark = points + 0.001f >= stats.Cost ? "" : "·";
            return mark + key + " " + stats.Name + " " + Mathf.FloorToInt(stats.Cost);
        }

        void DrawNotice()
        {
            if (_world.NoticeTime <= 0f || _world.Notice == BattleNotice.None)
                return;
            string text = _world.Notice == BattleNotice.NeedPoints ? "Mobilização insuficiente"
                : _world.Notice == BattleNotice.TroopCap ? "Limite de tropas no campo"
                : _world.Notice == BattleNotice.OutOfRange ? "Fora do alcance de deploy"
                : "Esquadrão mobilizado";
            var rect = new Rect(Screen.width * 0.5f - 180f, Screen.height - 138f, 360f, 28f);
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

            DrawRect(new Rect(0f, h * 0.36f, w, 150f), new Color(0f, 0f, 0f, 0.72f));
            GUI.Label(new Rect(0f, h * 0.36f + 18f, w, 40f), banner, _title);
            string hint = _world.Outcome == BattleOutcome.Ongoing ? "Esc para continuar" : "R para lutar de novo";
            GUI.Label(new Rect(0f, h * 0.36f + 62f, w, 28f), hint, _center);
            if (_world.Outcome != BattleOutcome.Ongoing && GUI.Button(new Rect(w * 0.5f - 90f, h * 0.36f + 98f, 180f, 32f), "Menu"))
                LeaveToMenu();
        }

        void DrawHelp(float w, float h)
        {
            DrawRect(new Rect(0f, h - 96f, w, 96f), new Color(0f, 0f, 0f, 0.55f));
            GUI.Label(new Rect(16f, h - 88f, w - 32f, 22f), "WASD relativo à câmera    Q/E ou botão do meio gira 360    F voo    V melee    Shift investida", _center);
            GUI.Label(new Rect(16f, h - 64f, w - 32f, 22f), "1 fuzil  2 canhão  3 vanguarda  4 baluarte  5 estandarte  6 médico    direito: avanço    C limpa", _center);
            GUI.Label(new Rect(16f, h - 42f, w - 32f, 20f), "ATK atira. TANK entra no corpo a corpo. SUP só buffa ataque ou cura. M volta ao menu.", _center);
            GUI.Label(new Rect(16f, h - 22f, w - 32f, 20f),
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

using System.Collections.Generic;
using MechaRts.Sim;
using UnityEngine;

namespace MechaRts.Game
{
    sealed class BattleView
    {
        sealed class Avatar
        {
            public Transform Root;
            public Transform Body;
            public Transform Shadow;
            public Transform Nose;
            public Transform BarFill;
            public SpriteRenderer BodyRenderer;
            public SpriteRenderer ShadowRenderer;
            public SpriteRenderer BarBg;
            public SpriteRenderer BarFillRenderer;
            public float Flash;
            public float LastHp;
            public float BarWidth;
            public float BarY;
            public float ShadowScale;
            public float Stand;
            public float Thruster;
            public Color Color;
        }

        sealed class Fade
        {
            public Transform Root;
            public SpriteRenderer Renderer;
            public float Life;
            public float Age;
            public Vector3 Velocity;
        }

        readonly Dictionary<int, Avatar> _avatars = new Dictionary<int, Avatar>();
        readonly List<Transform> _shotPool = new List<Transform>();
        readonly List<Fade> _fades = new List<Fade>();
        readonly List<int> _remove = new List<int>();
        readonly List<Transform> _skyBlocks = new List<Transform>();
        readonly List<Transform> _partMarks = new List<Transform>();

        Transform _rally;
        int _epoch = -1;
        Camera _camera;
        float _zoomBias;
        Transform _root;
        float _distance = 24f;
        float _pitch = 56f;
        float _yaw = 34f;
        BattleMode _mode = BattleMode.Skirmish;

        public Camera Build(BattleWorld world)
        {
            PixelKit.Ensure();
            _root = new GameObject("Presentation").transform;
            var cameraGo = new GameObject("BattleCamera");
            cameraGo.transform.SetParent(_root, false);
            _camera = cameraGo.AddComponent<Camera>();
            bool colossus = world.Mode == BattleMode.Colossus;
            _camera.orthographic = false;
            _camera.fieldOfView = world.Mode == BattleMode.Duel ? 46f : 32f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = colossus ? new Color(0.55f, 0.74f, 0.92f) : new Color(0.07f, 0.06f, 0.08f);
            _camera.nearClipPlane = 0.3f;
            _camera.farClipPlane = 80f;
            _camera.depth = 20f;
            _camera.transparencySortMode = TransparencySortMode.Perspective;
            cameraGo.AddComponent<AudioListener>();
            if (world.Mode == BattleMode.Colossus)
                UpdateColossusCamera(world, 1f);
            else if (world.Mode != BattleMode.Skirmish)
            {
                float dx = world.Enemy.Pos.X - world.Player.Pos.X;
                float dz = world.Enemy.Pos.Y - world.Player.Pos.Y;
                _yaw = Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
                UpdateBossCamera(world, 1f);
            }
            else
                SnapCamera(Vector2.zero, 0f, 1f);

            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(_root, false);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.05f;
            sun.color = new Color(1f, 0.9f, 0.78f);
            sunGo.transform.rotation = Quaternion.Euler(48f, _yaw - 20f, 0f);

            var ground = new GameObject("Arena");
            ground.transform.SetParent(_root, false);
            var groundRenderer = ground.AddComponent<SpriteRenderer>();
            groundRenderer.sprite = PixelKit.Ground;
            groundRenderer.sortingOrder = -400;
            ground.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            ground.transform.position = new Vector3(0f, 0f, 0f);
            if (colossus)
                ground.SetActive(false);

            Shader blockShader = Shader.Find("Unlit/Color");
            if (blockShader == null)
                blockShader = Shader.Find("Standard");
            var blockMaterial = new Material(blockShader);
            if (blockMaterial.HasProperty("_Color"))
                blockMaterial.color = new Color(0.34f, 0.36f, 0.42f);

            for (int i = 0; i < world.Obstacles.Count; i++)
            {
                Obstacle obstacle = world.Obstacles[i];
                float height = world.Mode == BattleMode.Skirmish ? 1.45f : 8f;
                var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                block.name = "Cover";
                block.transform.SetParent(_root, true);
                var collider = block.GetComponent<Collider>();
                if (collider != null)
                    Object.Destroy(collider);
                block.GetComponent<MeshRenderer>().sharedMaterial = blockMaterial;
                block.transform.position = new Vector3(obstacle.Center.X, height * 0.5f, obstacle.Center.Y);
                block.transform.localScale = new Vector3(obstacle.Half.X * 2f, height, obstacle.Half.Y * 2f);
            }

            if (colossus)
                BuildSkyLane(blockMaterial, world);

            _rally = new GameObject("Rally").transform;
            _rally.SetParent(_root, false);
            var rallyRenderer = _rally.gameObject.AddComponent<SpriteRenderer>();
            rallyRenderer.sprite = PixelKit.Disc;
            rallyRenderer.color = new Color(0.95f, 0.82f, 0.28f, 0.9f);
            rallyRenderer.sortingOrder = -30;
            _rally.rotation = Quaternion.Euler(90f, 0f, 0f);
            _rally.localScale = new Vector3(0.7f, 0.7f, 1f);
            _rally.gameObject.SetActive(false);
            return _camera;
        }

        public bool TryPickGround(Vector3 screen, out float simX, out float simY)
        {
            return TryPickHeight(screen, 0f, out simX, out simY);
        }

        public bool TryPickHeight(Vector3 screen, float height, out float simX, out float simY)
        {
            Ray ray = _camera.ScreenPointToRay(screen);
            simX = 0f;
            simY = 0f;
            if (Mathf.Abs(ray.direction.y) < 0.0001f)
                return false;
            float t = (height - ray.origin.y) / ray.direction.y;
            if (t < 0f)
                return false;
            Vector3 hit = ray.origin + ray.direction * t;
            simX = hit.x;
            simY = hit.z;
            return true;
        }

        public void CameraPlanarAxes(out Vector3 right, out Vector3 ahead)
        {
            ahead = _camera.transform.forward;
            ahead.y = 0f;
            if (ahead.sqrMagnitude < 0.0001f)
                ahead = Vector3.forward;
            ahead.Normalize();
            right = _camera.transform.right;
            right.y = 0f;
            right.Normalize();
        }

        public void AddYaw(float delta)
        {
            _yaw += delta;
        }

        public void Dispose()
        {
            foreach (var pair in _avatars)
            {
                if (pair.Value.Root != null)
                    Object.Destroy(pair.Value.Root.gameObject);
            }
            _avatars.Clear();
            if (_root != null)
                Object.Destroy(_root.gameObject);
            _root = null;
            _camera = null;
        }

        public void AddZoom(float delta)
        {
            _zoomBias = Mathf.Clamp(_zoomBias + delta, -1.6f, 1.8f);
        }

        public void UpdateCamera(BattleWorld world, float dt)
        {
            float alt = world.Player != null ? world.Player.Altitude : 0f;
            SimVec2 player = world.Player.Pos;
            SimVec2 enemy = world.Enemy.Pos;
            float playerWeight = Mathf.Lerp(0.62f, 0.5f, alt);
            var focus = new Vector2(
                player.X * playerWeight + enemy.X * (1f - playerWeight),
                player.Y * playerWeight + enemy.Y * (1f - playerWeight));
            focus = Vector2.Lerp(focus, Vector2.zero, alt * 0.55f);
            focus.x = Mathf.Clamp(focus.x, -8f, 8f);
            focus.y = Mathf.Clamp(focus.y, -6f, 6f);
            float follow = 1f - Mathf.Exp(-3.2f * dt);
            if (world.Mode == BattleMode.Skirmish)
                SnapCamera(focus, alt, follow);
            else if (world.Mode == BattleMode.Colossus)
                UpdateColossusCamera(world, dt);
            else
                UpdateBossCamera(world, dt);
        }

        void UpdateColossusCamera(BattleWorld world, float dt)
        {
            float follow = dt >= 1f ? 1f : 1f - Mathf.Exp(-6f * dt);
            _camera.orthographic = false;
            _camera.fieldOfView = 48f;
            _camera.farClipPlane = 140f;
            Quaternion rot = Quaternion.Euler(18f, 0f, 0f);
            Vector3 desired = new Vector3(0f, 14f + _zoomBias * 0.4f, -20f - _zoomBias * 0.8f);
            _camera.transform.rotation = rot;
            _camera.transform.position = Vector3.Lerp(_camera.transform.position, desired, follow);
            if (dt < 1f)
                ScrollSky(dt);
        }

        void BuildSkyLane(Material pillarMaterial, BattleWorld world)
        {
            var plateMat = new Material(pillarMaterial);
            if (plateMat.HasProperty("_Color"))
                plateMat.color = new Color(0.33f, 0.46f, 0.34f);
            for (int ix = -2; ix <= 2; ix++)
            {
                for (int iz = 0; iz < 8; iz++)
                {
                    float x = ix * 6.4f;
                    float z = -22f + iz * 6.3f;
                    var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    plate.name = "SkyPlate";
                    plate.transform.SetParent(_root, false);
                    Object.Destroy(plate.GetComponent<Collider>());
                    plate.GetComponent<MeshRenderer>().sharedMaterial = plateMat;
                    plate.transform.localScale = new Vector3(6.7f, 0.18f, 6.6f);
                    plate.transform.localPosition = new Vector3(x, 0.09f, z);
                    _skyBlocks.Add(plate.transform);

                    if ((ix + iz) % 2 != 0)
                        continue;
                    float height = 1.5f + ((ix * 3 + iz) & 3) * 0.55f;
                    var pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    pillar.name = "SkyPillar";
                    pillar.transform.SetParent(_root, false);
                    Object.Destroy(pillar.GetComponent<Collider>());
                    pillar.GetComponent<MeshRenderer>().sharedMaterial = pillarMaterial;
                    pillar.transform.localScale = new Vector3(1.15f, height, 1.15f);
                    pillar.transform.localPosition = new Vector3(x, height * 0.5f, z);
                    _skyBlocks.Add(pillar.transform);
                }
            }

            for (int i = 0; i < world.Parts.Count; i++)
            {
                var mark = new GameObject("BossPart");
                mark.transform.SetParent(_root, false);
                var renderer = mark.AddComponent<SpriteRenderer>();
                renderer.sprite = PixelKit.Disc;
                renderer.sortingOrder = 900;
                mark.transform.localScale = new Vector3(1.35f, 1.35f, 1f);
                _partMarks.Add(mark.transform);
            }
        }

        void ScrollSky(float dt)
        {
            const float speed = 8.5f;
            const float span = 8f * 6.3f;
            for (int i = 0; i < _skyBlocks.Count; i++)
            {
                Transform block = _skyBlocks[i];
                Vector3 pos = block.localPosition;
                pos.z -= speed * dt;
                if (pos.z < -26f)
                    pos.z += span;
                block.localPosition = pos;
            }
        }

        void SyncParts(BattleWorld world)
        {
            if (world.Mode != BattleMode.Colossus || world.Enemy == null)
                return;
            Mecha boss = world.Enemy;
            int count = Mathf.Min(_partMarks.Count, world.Parts.Count);
            for (int i = 0; i < count; i++)
            {
                BossPart part = world.Parts[i];
                Transform mark = _partMarks[i];
                mark.position = new Vector3(boss.Pos.X + part.Offset.X, boss.Altitude + 0.45f, boss.Pos.Y + part.Offset.Y);
                mark.rotation = _camera.transform.rotation;
                var renderer = mark.GetComponent<SpriteRenderer>();
                bool alive = part.Hp > 0f;
                float pulse = alive ? 1f + Mathf.Sin(Time.time * 5f + i) * 0.08f : 0.55f;
                mark.localScale = new Vector3(pulse, pulse, 1f) * (alive ? 1.45f : 0.7f);
                renderer.color = !alive
                    ? new Color(0.25f, 0.25f, 0.28f, 0.45f)
                    : PartColor(i);
            }
        }

        static Color PartColor(int index)
        {
            switch (index)
            {
                case 0: return new Color(1f, 0.86f, 0.28f);
                case 1: return new Color(0.45f, 0.95f, 1f);
                default: return new Color(1f, 0.48f, 0.28f);
            }
        }

        void UpdateBossCamera(BattleWorld world, float dt)
        {
            Mecha player = world.Player;
            float follow = dt >= 1f ? 1f : 1f - Mathf.Exp(-5f * dt);
            Vector3 focus = new Vector3(player.Pos.X, 1.15f + player.Altitude, player.Pos.Y);
            Quaternion rot = Quaternion.Euler(18f, _yaw, 0f);
            float dist = Mathf.Clamp(12.2f + _zoomBias * 1.4f, 8f, 24f);
            Vector3 desired = focus - rot * Vector3.forward * dist;
            _camera.transform.rotation = Quaternion.Slerp(_camera.transform.rotation, rot, follow);
            _camera.transform.position = Vector3.Lerp(_camera.transform.position, desired, follow);
        }

        void SnapCamera(Vector2 focus, float altitude, float follow)
        {
            float targetDist = Mathf.Lerp(22f, 31f, altitude) + _zoomBias * 2.6f;
            targetDist = Mathf.Clamp(targetDist, 16f, 42f);
            float targetPitch = Mathf.Lerp(58f, 50f, altitude);
            _distance = Mathf.Lerp(_distance, targetDist, follow);
            _pitch = Mathf.Lerp(_pitch, targetPitch, follow);
            Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 look = new Vector3(focus.x, 0.4f, focus.y);
            Vector3 desired = look - rot * Vector3.forward * _distance;
            _camera.transform.rotation = rot;
            _camera.transform.position = Vector3.Lerp(_camera.transform.position, desired, follow);
        }

        public void Sync(BattleWorld world, float dt)
        {
            if (world.Epoch != _epoch)
            {
                _epoch = world.Epoch;
                foreach (var pair in _avatars)
                    Object.Destroy(pair.Value.Root.gameObject);
                _avatars.Clear();
            }

            _mode = world.Mode;
            for (int i = 0; i < world.Mechas.Count; i++)
                SyncMecha(world.Mechas[i], dt);
            for (int i = 0; i < world.Troops.Count; i++)
                SyncTroop(world.Troops[i], dt);

            _remove.Clear();
            foreach (var pair in _avatars)
            {
                if (!StillAlive(world, pair.Key))
                    _remove.Add(pair.Key);
            }
            for (int i = 0; i < _remove.Count; i++)
            {
                Object.Destroy(_avatars[_remove[i]].Root.gameObject);
                _avatars.Remove(_remove[i]);
            }

            SyncShots(world);
            SyncParts(world);
            SyncFx(world, dt);
            _rally.gameObject.SetActive(world.HasRally);
            if (world.HasRally)
            {
                float pulse = 0.4f + Mathf.Sin(Time.time * 6f) * 0.06f;
                _rally.localScale = new Vector3(pulse, pulse, 1f);
                _rally.position = new Vector3(world.Rally.X, 0.08f, world.Rally.Y);
            }
        }

        void SyncMecha(Mecha mecha, float dt)
        {
            Avatar avatar = GetAvatar(mecha.Id, PixelKit.Mecha, mecha.IsPlayer ? PlayerColor : EnemyColor, 1.8f, 1.15f, 1.55f, 1.35f);
            float lift = _mode == BattleMode.Skirmish ? mecha.Altitude * BattleTuning.HoverVisualLift : mecha.Altitude;
            Paint(avatar, mecha.Pos, mecha.Facing, mecha.Hp, mecha.MaxHp, dt, mecha.Hp <= 0f, lift, mecha.BodyScale);
            float flameAt = _mode == BattleMode.Skirmish ? 0.18f : 0.8f;
            if (mecha.Hp <= 0f || mecha.Altitude < flameAt)
                return;
            avatar.Thruster -= dt;
            if (avatar.Thruster > 0f)
                return;
            avatar.Thruster = 0.07f;
            Color flame = mecha.IsPlayer ? new Color(0.55f, 0.92f, 1f) : new Color(1f, 0.5f, 0.28f);
            SpawnFade(mecha.Pos, 0.25f, flame, 0.42f, 0.18f, Vector3.zero);
        }

        void SyncTroop(Troop troop, float dt)
        {
            TroopVisual(troop.Kind, troop.Team, out Sprite sprite, out Color color, out float shadow, out float barY, out float barW, out float stand);
            Avatar avatar = GetAvatar(troop.Id, sprite, color, shadow, barY, barW, stand);
            Paint(avatar, troop.Pos, troop.Facing, troop.Hp, troop.MaxHp, dt, false, 0f, 1f);
        }

        static void TroopVisual(TroopKind kind, int team, out Sprite sprite, out Color color, out float shadow, out float barY, out float barW, out float stand)
        {
            Color teamTint = team == 0 ? PlayerColor : EnemyColor;
            switch (kind)
            {
                case TroopKind.Cannon:
                    sprite = PixelKit.Heavy; color = teamTint * 0.85f; shadow = 1.05f; barY = 0.7f; barW = 0.95f; stand = 0.58f; break;
                case TroopKind.Vanguard:
                    sprite = PixelKit.Rifle; color = team == 0 ? new Color(0.72f, 0.86f, 0.95f) : new Color(0.95f, 0.55f, 0.42f); shadow = 0.9f; barY = 0.55f; barW = 0.8f; stand = 0.5f; break;
                case TroopKind.Heavy:
                    sprite = PixelKit.Heavy; color = teamTint * 0.62f; shadow = 1.2f; barY = 0.75f; barW = 1.05f; stand = 0.66f; break;
                case TroopKind.Banner:
                    sprite = PixelKit.Rifle; color = new Color(0.95f, 0.82f, 0.28f); shadow = 0.8f; barY = 0.55f; barW = 0.75f; stand = 0.48f; break;
                case TroopKind.Medic:
                    sprite = PixelKit.Rifle; color = new Color(0.45f, 0.95f, 0.55f); shadow = 0.75f; barY = 0.5f; barW = 0.7f; stand = 0.45f; break;
                default:
                    sprite = PixelKit.Rifle; color = teamTint; shadow = 0.72f; barY = 0.5f; barW = 0.7f; stand = 0.42f; break;
            }
        }

        void Paint(Avatar avatar, SimVec2 pos, float facing, float hp, float maxHp, float dt, bool dead, float lift, float bodyScale)
        {
            if (hp < avatar.LastHp - 0.1f)
                avatar.Flash = 0.09f;
            avatar.LastHp = hp;
            if (avatar.Flash > 0f)
                avatar.Flash -= dt;

            float snapX = Mathf.Round(pos.X * 8f) / 8f;
            float snapZ = Mathf.Round(pos.Y * 8f) / 8f;
            avatar.Root.position = new Vector3(snapX, 0f, snapZ);
            avatar.Body.localPosition = new Vector3(0f, avatar.Stand + lift, 0f);
            avatar.Body.rotation = _camera.transform.rotation;
            float reach = avatar.Stand + 0.45f;
            avatar.Nose.localPosition = new Vector3(Mathf.Cos(facing) * reach, 0.07f, Mathf.Sin(facing) * reach);
            float grow = 1f + lift * 0.35f;
            float footprint = avatar.ShadowScale * bodyScale * grow;
            avatar.Shadow.localScale = new Vector3(footprint, footprint, 1f);
            float shade = Mathf.Lerp(0.45f, 0.16f, Mathf.Clamp01(lift / 4f));
            avatar.ShadowRenderer.color = new Color(0f, 0f, 0f, shade);
            avatar.Body.localScale = new Vector3(bodyScale, bodyScale, 1f);
            float flash = avatar.Flash > 0f ? avatar.Flash / 0.09f : 0f;
            Color color = dead ? avatar.Color * 0.28f : Color.Lerp(avatar.Color, Color.white, flash);
            avatar.BodyRenderer.color = color;
            int order = 800 - Mathf.RoundToInt(snapZ * 8f) + Mathf.RoundToInt(lift * 4f);
            avatar.BodyRenderer.sortingOrder = order;

            float pct = maxHp <= 0f ? 0f : Mathf.Clamp01(hp / maxHp);
            bool showBar = !dead && pct < 0.995f;
            avatar.BarBg.enabled = showBar;
            avatar.BarFillRenderer.enabled = showBar;
            if (!showBar)
                return;
            avatar.BarBg.sortingOrder = order + 2;
            avatar.BarFillRenderer.sortingOrder = order + 3;
            float width = avatar.BarWidth * pct;
            avatar.BarFill.localScale = new Vector3(Mathf.Max(0.04f, width), 0.1f, 1f);
            avatar.BarBg.transform.localPosition = new Vector3(0f, avatar.BarY, 0f);
            avatar.BarFill.localPosition = new Vector3(-avatar.BarWidth * 0.5f + width * 0.5f, avatar.BarY, 0f);
            avatar.BarFillRenderer.color = pct > 0.45f ? new Color(0.45f, 0.9f, 0.45f) : new Color(0.95f, 0.42f, 0.28f);
        }

        Avatar GetAvatar(int id, Sprite sprite, Color color, float shadowScale, float barY, float barWidth, float stand)
        {
            if (_avatars.TryGetValue(id, out Avatar existing))
                return existing;

            var root = new GameObject("Unit");
            root.transform.SetParent(_root, false);
            var shadowGo = new GameObject("Shadow");
            shadowGo.transform.SetParent(root.transform, false);
            shadowGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            shadowGo.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            shadowGo.transform.localScale = new Vector3(shadowScale, shadowScale, 1f);
            var shadow = shadowGo.AddComponent<SpriteRenderer>();
            shadow.sprite = PixelKit.Disc;
            shadow.color = new Color(0f, 0f, 0f, 0.35f);
            shadow.sortingOrder = -20;

            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(root.transform, false);
            var body = bodyGo.AddComponent<SpriteRenderer>();
            body.sprite = sprite;

            var noseGo = new GameObject("Nose");
            noseGo.transform.SetParent(root.transform, false);
            noseGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            noseGo.transform.localScale = new Vector3(0.28f, 0.28f, 1f);
            var nose = noseGo.AddComponent<SpriteRenderer>();
            nose.sprite = PixelKit.Disc;
            nose.color = color;
            nose.sortingOrder = -15;

            var barBgGo = new GameObject("BarBg");
            barBgGo.transform.SetParent(bodyGo.transform, false);
            barBgGo.transform.localScale = new Vector3(barWidth + 0.08f, 0.16f, 1f);
            barBgGo.transform.localPosition = new Vector3(0f, barY, 0f);
            var barBg = barBgGo.AddComponent<SpriteRenderer>();
            barBg.sprite = PixelKit.Square;
            barBg.color = new Color(0.05f, 0.05f, 0.07f, 0.9f);

            var barGo = new GameObject("Bar");
            barGo.transform.SetParent(bodyGo.transform, false);
            var bar = barGo.AddComponent<SpriteRenderer>();
            bar.sprite = PixelKit.Square;

            var avatar = new Avatar
            {
                Root = root.transform,
                Body = bodyGo.transform,
                Shadow = shadowGo.transform,
                Nose = noseGo.transform,
                BarFill = barGo.transform,
                BodyRenderer = body,
                ShadowRenderer = shadow,
                BarBg = barBg,
                BarFillRenderer = bar,
                LastHp = float.MaxValue,
                BarWidth = barWidth,
                BarY = barY,
                ShadowScale = shadowScale,
                Stand = stand,
                Color = color
            };
            _avatars.Add(id, avatar);
            return avatar;
        }

        void SyncShots(BattleWorld world)
        {
            while (_shotPool.Count < world.Shots.Count)
            {
                var go = new GameObject("Shot");
                go.transform.SetParent(_root, false);
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = PixelKit.Square;
                renderer.sortingOrder = 40;
                _shotPool.Add(go.transform);
            }

            for (int i = 0; i < _shotPool.Count; i++)
            {
                bool active = i < world.Shots.Count;
                _shotPool[i].gameObject.SetActive(active);
                if (!active)
                    continue;
                Shot shot = world.Shots[i];
                var renderer = _shotPool[i].GetComponent<SpriteRenderer>();
                renderer.color = shot.Team == 0 ? new Color(1f, 0.92f, 0.55f) : new Color(1f, 0.55f, 0.35f);
                float height = shot.IsBeam ? 0.55f : (shot.Height > 0.05f ? shot.Height : 0.95f);
                Vector3 a = new Vector3(shot.Origin.X, height, shot.Origin.Y);
                Vector3 b = new Vector3(shot.Pos.X, height, shot.Pos.Y);
                Vector3 mid = (a + b) * 0.5f;
                Vector3 screenA = _camera.WorldToScreenPoint(a);
                Vector3 screenB = _camera.WorldToScreenPoint(b);
                float angle = Mathf.Atan2(screenB.y - screenA.y, screenB.x - screenA.x) * Mathf.Rad2Deg;
                _shotPool[i].position = shot.IsBeam ? mid : b;
                _shotPool[i].rotation = _camera.transform.rotation * Quaternion.Euler(0f, 0f, angle);
                float length = shot.IsBeam ? Mathf.Max(0.08f, Vector3.Distance(a, b)) : 0.55f;
                float thickness = shot.IsBeam ? 0.07f : 0.16f;
                _shotPool[i].localScale = new Vector3(length, thickness, 1f);
            }
        }

        void SyncFx(BattleWorld world, float dt)
        {
            for (int i = 0; i < world.Events.Count; i++)
            {
                SimEvent e = world.Events[i];
                if (e.Type == SimEventType.Muzzle)
                    SpawnFade(e.Pos, 1.1f, e.Team == 0 ? new Color(1f, 0.95f, 0.7f) : new Color(1f, 0.65f, 0.4f), 0.55f, 0.08f, Vector3.zero);
                else if (e.Type == SimEventType.Hit)
                    SpawnFade(e.Pos, 0.8f, new Color(1f, 0.85f, 0.55f), 0.28f, 0.12f, Vector3.zero);
                else if (e.Type == SimEventType.Death)
                {
                    for (int k = 0; k < 4; k++)
                    {
                        float ang = k * Mathf.PI * 0.5f;
                        var vel = new Vector3(Mathf.Cos(ang), 0.6f, Mathf.Sin(ang)) * 2.2f;
                        SpawnFade(e.Pos, 0.7f, e.Team == 0 ? PlayerColor : EnemyColor, 0.22f, 0.28f, vel);
                    }
                }
            }
            world.ClearEvents();

            for (int i = _fades.Count - 1; i >= 0; i--)
            {
                Fade fade = _fades[i];
                fade.Age += dt;
                if (fade.Age >= fade.Life)
                {
                    Object.Destroy(fade.Root.gameObject);
                    _fades.RemoveAt(i);
                    continue;
                }
                float t = 1f - fade.Age / fade.Life;
                fade.Root.position += fade.Velocity * dt;
                fade.Root.rotation = _camera.transform.rotation;
                fade.Root.localScale = Vector3.one * (0.15f + 0.45f * t);
                Color color = fade.Renderer.color;
                color.a = t;
                fade.Renderer.color = color;
            }
        }

        void SpawnFade(SimVec2 pos, float height, Color color, float size, float life, Vector3 velocity)
        {
            var go = new GameObject("Fx");
            go.transform.SetParent(_root, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = PixelKit.Disc;
            renderer.color = color;
            renderer.sortingOrder = 60;
            go.transform.position = new Vector3(pos.X, height, pos.Y);
            go.transform.rotation = _camera.transform.rotation;
            go.transform.localScale = Vector3.one * size;
            _fades.Add(new Fade
            {
                Root = go.transform,
                Renderer = renderer,
                Life = life,
                Velocity = velocity
            });
        }

        static bool StillAlive(BattleWorld world, int id)
        {
            for (int i = 0; i < world.Mechas.Count; i++)
                if (world.Mechas[i].Id == id) return true;
            for (int i = 0; i < world.Troops.Count; i++)
                if (world.Troops[i].Id == id) return true;
            return false;
        }

        static readonly Color PlayerColor = new Color(0.38f, 0.86f, 1f);
        static readonly Color EnemyColor = new Color(1f, 0.38f, 0.32f);
    }
}

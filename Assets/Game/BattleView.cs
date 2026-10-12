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
            public Transform BarFill;
            public SpriteRenderer BodyRenderer;
            public SpriteRenderer BarBg;
            public SpriteRenderer BarFillRenderer;
            public float Flash;
            public float LastHp;
            public float BarWidth;
            public float BarY;
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

        Transform _rally;
        int _epoch = -1;
        Camera _camera;

        public Camera Build(BattleWorld world)
        {
            PixelKit.Ensure();
            var cameraGo = new GameObject("BattleCamera");
            _camera = cameraGo.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 14.5f;
            _camera.transform.position = new Vector3(0f, 0f, -10f);
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.055f, 0.06f, 0.075f);
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 40f;
            _camera.depth = 20f;
            cameraGo.AddComponent<AudioListener>();

            var ground = new GameObject("Arena");
            var groundRenderer = ground.AddComponent<SpriteRenderer>();
            groundRenderer.sprite = PixelKit.Ground;
            groundRenderer.sortingOrder = -200;

            for (int i = 0; i < world.Obstacles.Count; i++)
            {
                Obstacle obstacle = world.Obstacles[i];
                var block = new GameObject("Cover");
                var renderer = block.AddComponent<SpriteRenderer>();
                renderer.sprite = PixelKit.Block;
                renderer.sortingOrder = Depth(obstacle.Center.Y);
                block.transform.position = new Vector3(obstacle.Center.X, obstacle.Center.Y, 0f);
                block.transform.localScale = new Vector3(obstacle.Half.X * 2f, obstacle.Half.Y * 2f, 1f);
            }

            _rally = new GameObject("Rally").transform;
            var rallyRenderer = _rally.gameObject.AddComponent<SpriteRenderer>();
            rallyRenderer.sprite = PixelKit.Disc;
            rallyRenderer.color = new Color(0.95f, 0.82f, 0.28f, 0.85f);
            rallyRenderer.sortingOrder = 20;
            _rally.localScale = new Vector3(0.45f, 0.45f, 1f);
            _rally.gameObject.SetActive(false);
            return _camera;
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
            SyncFx(world, dt);
            _rally.gameObject.SetActive(world.HasRally);
            if (world.HasRally)
            {
                float pulse = 0.4f + Mathf.Sin(Time.time * 6f) * 0.06f;
                _rally.localScale = new Vector3(pulse, pulse, 1f);
                _rally.position = Snap(world.Rally.X, world.Rally.Y);
            }
        }

        void SyncMecha(Mecha mecha, float dt)
        {
            Avatar avatar = GetAvatar(mecha.Id, PixelKit.Mecha, mecha.IsPlayer ? PlayerColor : EnemyColor, 1.8f, 1.7f, 1.55f);
            Paint(avatar, mecha.Pos, mecha.Facing, mecha.Hp, BattleTuning.MechaHp, dt, mecha.Hp <= 0f);
        }

        void SyncTroop(Troop troop, float dt)
        {
            bool heavy = troop.Kind == TroopKind.Heavy;
            Color color = troop.Team == 0 ? PlayerColor : EnemyColor;
            if (heavy)
                color *= 0.82f;
            Sprite sprite = heavy ? PixelKit.Heavy : PixelKit.Rifle;
            float maxHp = heavy ? BattleTuning.HeavyHp : BattleTuning.RifleHp;
            Avatar avatar = GetAvatar(troop.Id, sprite, color, heavy ? 1.05f : 0.72f, heavy ? 0.95f : 0.72f, heavy ? 0.95f : 0.7f);
            Paint(avatar, troop.Pos, troop.Facing, troop.Hp, maxHp, dt, false);
        }

        void Paint(Avatar avatar, SimVec2 pos, float facing, float hp, float maxHp, float dt, bool dead)
        {
            if (hp < avatar.LastHp - 0.1f)
                avatar.Flash = 0.09f;
            avatar.LastHp = hp;
            if (avatar.Flash > 0f)
                avatar.Flash -= dt;

            avatar.Root.position = Snap(pos.X, pos.Y);
            avatar.Body.rotation = Quaternion.Euler(0f, 0f, facing * Mathf.Rad2Deg);
            float flash = avatar.Flash > 0f ? avatar.Flash / 0.09f : 0f;
            Color color = dead ? avatar.Color * 0.28f : Color.Lerp(avatar.Color, Color.white, flash);
            avatar.BodyRenderer.color = color;
            int order = Depth(pos.Y);
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
            avatar.BarFill.localPosition = new Vector3(-avatar.BarWidth * 0.5f + width * 0.5f, avatar.BarY, 0f);
            avatar.BarFillRenderer.color = pct > 0.45f ? new Color(0.45f, 0.9f, 0.45f) : new Color(0.95f, 0.42f, 0.28f);
        }

        Avatar GetAvatar(int id, Sprite sprite, Color color, float shadowScale, float barY, float barWidth)
        {
            if (_avatars.TryGetValue(id, out Avatar existing))
                return existing;

            var root = new GameObject("Unit");
            var shadowGo = new GameObject("Shadow");
            shadowGo.transform.SetParent(root.transform, false);
            shadowGo.transform.localScale = new Vector3(shadowScale, shadowScale * 0.55f, 1f);
            var shadow = shadowGo.AddComponent<SpriteRenderer>();
            shadow.sprite = PixelKit.Disc;
            shadow.color = new Color(0f, 0f, 0f, 0.35f);
            shadow.sortingOrder = -20;

            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(root.transform, false);
            var body = bodyGo.AddComponent<SpriteRenderer>();
            body.sprite = sprite;

            var barBgGo = new GameObject("BarBg");
            barBgGo.transform.SetParent(root.transform, false);
            barBgGo.transform.localScale = new Vector3(barWidth + 0.08f, 0.16f, 1f);
            barBgGo.transform.localPosition = new Vector3(0f, barY, 0f);
            var barBg = barBgGo.AddComponent<SpriteRenderer>();
            barBg.sprite = PixelKit.Square;
            barBg.color = new Color(0.05f, 0.05f, 0.07f, 0.9f);

            var barGo = new GameObject("Bar");
            barGo.transform.SetParent(root.transform, false);
            var bar = barGo.AddComponent<SpriteRenderer>();
            bar.sprite = PixelKit.Square;

            var avatar = new Avatar
            {
                Root = root.transform,
                Body = bodyGo.transform,
                BarFill = barGo.transform,
                BodyRenderer = body,
                BarBg = barBg,
                BarFillRenderer = bar,
                LastHp = float.MaxValue,
                BarWidth = barWidth,
                BarY = barY,
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
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = PixelKit.Square;
                renderer.sortingOrder = 760;
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
                if (shot.IsBeam)
                {
                    Vector3 a = Snap(shot.Origin.X, shot.Origin.Y);
                    Vector3 b = Snap(shot.Pos.X, shot.Pos.Y);
                    float length = Vector3.Distance(a, b);
                    _shotPool[i].position = (a + b) * 0.5f;
                    float angle = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
                    _shotPool[i].rotation = Quaternion.Euler(0f, 0f, angle);
                    _shotPool[i].localScale = new Vector3(Mathf.Max(0.05f, length), 0.07f, 1f);
                }
                else
                {
                    _shotPool[i].position = Snap(shot.Pos.X, shot.Pos.Y);
                    float angle = Mathf.Atan2(shot.Vel.Y, shot.Vel.X) * Mathf.Rad2Deg;
                    _shotPool[i].rotation = Quaternion.Euler(0f, 0f, angle);
                    _shotPool[i].localScale = new Vector3(0.55f, 0.16f, 1f);
                }
            }
        }

        void SyncFx(BattleWorld world, float dt)
        {
            for (int i = 0; i < world.Events.Count; i++)
            {
                SimEvent e = world.Events[i];
                if (e.Type == SimEventType.Muzzle)
                    SpawnFade(e.Pos, e.Team == 0 ? new Color(1f, 0.95f, 0.7f) : new Color(1f, 0.65f, 0.4f), 0.55f, 0.08f, Vector3.zero);
                else if (e.Type == SimEventType.Hit)
                    SpawnFade(e.Pos, new Color(1f, 0.85f, 0.55f), 0.28f, 0.12f, Vector3.zero);
                else if (e.Type == SimEventType.Death)
                {
                    for (int k = 0; k < 4; k++)
                    {
                        float ang = k * Mathf.PI * 0.5f;
                        var vel = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * 2.2f;
                        SpawnFade(e.Pos, e.Team == 0 ? PlayerColor : EnemyColor, 0.22f, 0.28f, vel);
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
                fade.Root.localScale = Vector3.one * (0.15f + 0.45f * t);
                Color color = fade.Renderer.color;
                color.a = t;
                fade.Renderer.color = color;
            }
        }

        void SpawnFade(SimVec2 pos, Color color, float size, float life, Vector3 velocity)
        {
            var go = new GameObject("Fx");
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = PixelKit.Disc;
            renderer.color = color;
            renderer.sortingOrder = 820;
            go.transform.position = new Vector3(pos.X, pos.Y, 0f);
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

        static int Depth(float y) => 500 - Mathf.RoundToInt(y * 12f);

        static Vector3 Snap(float x, float y) => new Vector3(Mathf.Round(x * 8f) / 8f, Mathf.Round(y * 8f) / 8f, 0f);

        static readonly Color PlayerColor = new Color(0.38f, 0.86f, 1f);
        static readonly Color EnemyColor = new Color(1f, 0.38f, 0.32f);
    }
}

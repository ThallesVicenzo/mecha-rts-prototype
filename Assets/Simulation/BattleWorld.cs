using System;
using System.Collections.Generic;

namespace MechaRts.Sim
{
    /// <summary>
    /// Real-time battle simulation with no engine references.
    /// The Unity project only reads this state and draws it.
    /// </summary>
    public sealed class BattleWorld
    {
        public const float TickDt = 1f / 60f;

        readonly List<Mecha> _mechas = new List<Mecha>(2);
        readonly List<Troop> _troops = new List<Troop>(40);
        readonly List<Shot> _shots = new List<Shot>(48);
        readonly List<Obstacle> _obstacles = new List<Obstacle>(8);

        public readonly List<SimEvent> Events = new List<SimEvent>(64);

        SimRng _rng;
        int _nextId = 1;
        int _nextSlot = 1;

        public int Epoch { get; private set; }
        public int Seed { get; private set; }
        public float Time { get; private set; }
        public BattleOutcome Outcome { get; private set; }
        public bool AiEnabled = true;
        public bool HasRally { get; private set; }
        public SimVec2 Rally { get; private set; }
        public BattleNotice Notice { get; private set; }
        public float NoticeTime { get; private set; }

        public Mecha Player { get; private set; }
        public Mecha Enemy { get; private set; }

        public IReadOnlyList<Mecha> Mechas => _mechas;
        public IReadOnlyList<Troop> Troops => _troops;
        public IReadOnlyList<Shot> Shots => _shots;
        public IReadOnlyList<Obstacle> Obstacles => _obstacles;

        public BattleWorld(int seed)
        {
            Reset(seed);
        }

        public void SetRally(float x, float y)
        {
            HasRally = true;
            Rally = ClampToArena(new SimVec2(x, y), 0.4f);
        }

        public void ClearRally() => HasRally = false;

        public void ClearEvents() => Events.Clear();

        public int CountTroops(int team)
        {
            int count = 0;
            for (int i = 0; i < _troops.Count; i++)
            {
                if (_troops[i].Team == team && _troops[i].Hp > 0f)
                    count++;
            }
            return count;
        }

        public bool HasLineOfSight(SimVec2 from, SimVec2 to)
        {
            for (int i = 0; i < _obstacles.Count; i++)
            {
                Obstacle obstacle = _obstacles[i];
                if (SimGeom.SegmentHitsAabb(from, to, obstacle.Center, obstacle.Half))
                    return false;
            }
            return true;
        }

        public void Reset(int seed)
        {
            Epoch++;
            Seed = seed;
            _rng = new SimRng(seed);
            Time = 0f;
            Outcome = BattleOutcome.Ongoing;
            HasRally = false;
            Rally = SimVec2.Zero;
            Notice = BattleNotice.None;
            NoticeTime = 0f;
            _nextId = 1;
            _nextSlot = 1;
            _mechas.Clear();
            _troops.Clear();
            _shots.Clear();
            _obstacles.Clear();
            Events.Clear();

            AddObstacle(0f, 0f, 1.15f, 1.15f);
            AddObstacle(-7.5f, 3.2f, 2.4f, 0.65f);
            AddObstacle(7.2f, -2.6f, 0.65f, 2.5f);
            AddObstacle(-13.2f, -5.2f, 1.45f, 1.45f);
            AddObstacle(12.4f, 6.1f, 1.7f, 1.0f);
            AddObstacle(2.2f, 7.6f, 2.0f, 0.55f);

            Player = AddMecha(0, true, new SimVec2(0f, -9f), MathF.PI * 0.5f);
            Enemy = AddMecha(1, false, new SimVec2(0f, 9f), -MathF.PI * 0.5f);
            SpawnSquad(Player, TroopKind.Rifle, true);
            SpawnSquad(Enemy, TroopKind.Rifle, true);
            Notice = BattleNotice.None;
            NoticeTime = 0f;
            Events.Clear();
        }

        public void Tick(float dt, BattleCommand command)
        {
            if (Outcome != BattleOutcome.Ongoing)
                return;

            Time += dt;
            if (NoticeTime > 0f)
            {
                NoticeTime -= dt;
                if (NoticeTime <= 0f)
                    Notice = BattleNotice.None;
            }

            for (int i = 0; i < _mechas.Count; i++)
                _mechas[i].JustFired = false;
            for (int i = 0; i < _troops.Count; i++)
                _troops[i].JustFired = false;

            StepPlayer(dt, command);
            if (AiEnabled)
                StepEnemy(dt);
            else
                CoolMecha(Enemy, dt);

            StepTroops(dt);
            StepShots(dt);
            Separate();
            Cull();
            ResolveOutcome();
        }

        void StepPlayer(float dt, BattleCommand command)
        {
            if (Player.Hp <= 0f)
                return;

            var wish = new SimVec2(command.MoveX, command.MoveY);
            DriveMecha(Player, wish, command.Dash, dt);
            TurnToward(Player, new SimVec2(command.AimX, command.AimY) - Player.Pos, BattleTuning.MechaTurnRate, dt);

            if (command.Fire)
                TryFireMecha(Player);
            if (command.DeployRifle)
                SpawnSquad(Player, TroopKind.Rifle, false);
            if (command.DeployHeavy)
                SpawnSquad(Player, TroopKind.Heavy, false);
        }

        void StepEnemy(float dt)
        {
            if (Enemy.Hp <= 0f || Player.Hp <= 0f)
                return;

            SimVec2 toPlayer = Player.Pos - Enemy.Pos;
            float distSq = toPlayer.SqrMagnitude;
            float dist = MathF.Sqrt(distSq);
            SimVec2 dir = dist > 0.001f ? toPlayer * (1f / dist) : SimVec2.FromAngle(Enemy.Facing);

            SimVec2 wish;
            if (Enemy.Hp < BattleTuning.MechaHp * 0.32f && dist < 12f)
                wish = -dir;
            else if (dist > BattleTuning.EnemyRangeMax)
                wish = dir;
            else if (dist < BattleTuning.EnemyRangeMin)
                wish = -dir;
            else
            {
                var side = new SimVec2(-dir.Y, dir.X);
                float sign = MathF.Sin(Time * 0.85f) >= 0f ? 1f : -1f;
                wish = side * sign;
            }

            bool wantDash = dist < 6.2f && Enemy.DashTime <= 0f;
            DriveMecha(Enemy, wish, wantDash, dt);

            SimVec2 lead = Player.Pos + Player.Vel * 0.18f;
            SimVec2 aim = lead - Enemy.Pos;
            TurnToward(Enemy, aim, BattleTuning.MechaTurnRate, dt);
            float align = SimGeom.DeltaAngle(Enemy.Facing, aim.Angle);
            if (dist < 26f && MathF.Abs(align) < 0.42f && HasLineOfSight(Enemy.Pos, Player.Pos))
                TryFireMecha(Enemy);

            AutoDeploy(Enemy);
        }

        void CoolMecha(Mecha mecha, float dt)
        {
            if (mecha.FireCooldown > 0f)
                mecha.FireCooldown -= dt;
            if (mecha.DashCooldown > 0f)
                mecha.DashCooldown -= dt;
        }

        void DriveMecha(Mecha mecha, SimVec2 wish, bool wantDash, float dt)
        {
            if (mecha.DashCooldown > 0f)
                mecha.DashCooldown -= dt;
            if (mecha.FireCooldown > 0f)
                mecha.FireCooldown -= dt;
            mecha.DeployPoints = MathF.Min(BattleTuning.MaxDeploy, mecha.DeployPoints + BattleTuning.DeployRegen * dt);

            bool dashing = mecha.DashTime > 0f;
            if (!dashing && wantDash && mecha.DashCooldown <= 0f)
            {
                mecha.DashDir = wish.SqrMagnitude > 0.04f ? SimVec2.Normalize(wish) : SimVec2.FromAngle(mecha.Facing);
                mecha.DashTime = BattleTuning.DashDuration;
                mecha.DashCooldown = BattleTuning.DashCooldown;
                dashing = true;
            }

            float mag = dashing ? 1f : wish.Magnitude;
            if (mag > 1f)
                mag = 1f;
            SimVec2 direction = dashing ? mecha.DashDir : (mag > 0.001f ? SimVec2.Normalize(wish) : SimVec2.Zero);
            float speed = dashing ? BattleTuning.DashSpeed : BattleTuning.MechaSpeed;
            float accel = dashing ? 180f : BattleTuning.MechaAccel;
            if (dashing)
            {
                mecha.DashTime -= dt;
                if (mecha.DashTime < 0f)
                    mecha.DashTime = 0f;
            }

            ApplyMotion(ref mecha.Pos, ref mecha.Vel, direction, mag, speed, accel, BattleTuning.MechaRadius, dt);
        }

        void StepTroops(float dt)
        {
            int playerCount = CountTroops(0);
            int enemyCount = CountTroops(1);
            int playerSeen = 0;
            int enemySeen = 0;

            for (int i = 0; i < _troops.Count; i++)
            {
                Troop troop = _troops[i];
                if (troop.Hp <= 0f)
                    continue;
                if (troop.FireCooldown > 0f)
                    troop.FireCooldown -= dt;

                Mecha ally = troop.Team == 0 ? Player : Enemy;
                int index = troop.Team == 0 ? playerSeen++ : enemySeen++;
                int total = troop.Team == 0 ? playerCount : enemyCount;
                StepTroop(troop, ally, index, total, dt);
            }
        }

        void StepTroop(Troop troop, Mecha ally, int index, int total, float dt)
        {
            float radius = Radius(troop.Kind);
            float speed = troop.Kind == TroopKind.Heavy ? BattleTuning.HeavySpeed : BattleTuning.RifleSpeed;
            bool found = TryAcquire(troop, out bool targetIsMecha, out int targetId, out SimVec2 targetPos);
            SimVec2 wish;
            SimVec2 look;

            if (found)
            {
                float preferred = troop.Kind == TroopKind.Heavy ? BattleTuning.HeavyPrefer : BattleTuning.RiflePrefer;
                float range = troop.Kind == TroopKind.Heavy ? BattleTuning.HeavyRange : BattleTuning.RifleRange;
                SimVec2 to = targetPos - troop.Pos;
                float dist = to.Magnitude;
                SimVec2 dir = dist > 0.001f ? to * (1f / dist) : SimVec2.FromAngle(troop.Facing);
                if (dist > preferred + 0.35f)
                    wish = dir;
                else if (dist < preferred - 0.75f)
                    wish = -dir;
                else
                {
                    var side = new SimVec2(-dir.Y, dir.X);
                    float sign = MathF.Sin(Time * 2.2f + troop.Id) >= 0f ? 1f : -1f;
                    wish = side * sign;
                }

                look = dir;
                if (dist <= range)
                    FireTroop(troop, targetIsMecha, targetId, targetPos);
            }
            else if (troop.Team == 0 && HasRally)
            {
                SimVec2 to = Rally - troop.Pos;
                float dist = to.Magnitude;
                wish = dist > 1.15f ? SimVec2.Normalize(to) : SimVec2.Zero;
                look = dist > 0.2f ? to : SimVec2.FromAngle(troop.Facing);
            }
            else
            {
                float arc = (index - (total - 1) * 0.5f) * 0.55f;
                float follow = troop.Kind == TroopKind.Heavy ? 4.5f : 3.15f;
                SimVec2 dest = ally.Pos + SimVec2.FromAngle(ally.Facing + MathF.PI + arc) * follow;
                SimVec2 to = dest - troop.Pos;
                wish = to.SqrMagnitude > 0.35f ? SimVec2.Normalize(to) : SimVec2.Zero;
                look = wish.SqrMagnitude > 0.01f ? wish : SimVec2.FromAngle(ally.Facing);
            }

            ApplyMotion(ref troop.Pos, ref troop.Vel, wish, wish.SqrMagnitude > 0.001f ? 1f : 0f, speed, BattleTuning.TroopAccel, radius, dt);
            TurnFacing(ref troop.Facing, look, BattleTuning.TroopTurnRate, dt);
        }

        bool TryAcquire(Troop troop, out bool targetIsMecha, out int targetId, out SimVec2 targetPos)
        {
            targetIsMecha = false;
            targetId = 0;
            targetPos = SimVec2.Zero;

            float aggro = troop.Team == 0
                ? (HasRally ? BattleTuning.PlayerRallyAggro : BattleTuning.PlayerAggro)
                : BattleTuning.EnemyAggro;
            float best = aggro * aggro;
            bool found = false;

            for (int i = 0; i < _troops.Count; i++)
            {
                Troop other = _troops[i];
                if (other.Team == troop.Team || other.Hp <= 0f)
                    continue;
                float d = SimVec2.DistanceSq(troop.Pos, other.Pos);
                if (d >= best)
                    continue;
                best = d;
                found = true;
                targetIsMecha = false;
                targetId = other.Id;
                targetPos = other.Pos;
            }

            Mecha foe = troop.Team == 0 ? Enemy : Player;
            if (foe.Hp > 0f)
            {
                float weighted = SimVec2.DistanceSq(troop.Pos, foe.Pos) * 1.35f;
                if (weighted < best)
                {
                    found = true;
                    targetIsMecha = true;
                    targetId = foe.Id;
                    targetPos = foe.Pos;
                }
            }

            return found;
        }

        void FireTroop(Troop troop, bool targetIsMecha, int targetId, SimVec2 aim)
        {
            if (troop.FireCooldown > 0f)
                return;
            if (!HasLineOfSight(troop.Pos, aim))
                return;

            float vsMecha = troop.Kind == TroopKind.Heavy ? BattleTuning.HeavyVsMecha : BattleTuning.RifleVsMecha;
            float vsTroop = troop.Kind == TroopKind.Heavy ? BattleTuning.HeavyVsTroop : BattleTuning.RifleVsTroop;
            troop.FireCooldown = troop.Kind == TroopKind.Heavy ? BattleTuning.HeavyInterval : BattleTuning.RifleInterval;
            troop.JustFired = true;

            if (targetIsMecha)
            {
                Mecha mecha = targetId == Player.Id ? Player : Enemy;
                if (mecha.Hp > 0f)
                    HurtMecha(mecha, vsMecha, aim);
            }
            else
            {
                Troop victim = FindTroop(targetId);
                if (victim != null && victim.Hp > 0f)
                    HurtTroop(victim, vsTroop, aim);
            }

            _shots.Add(new Shot
            {
                Team = troop.Team,
                Pos = aim,
                Origin = troop.Pos,
                Life = 0.07f,
                IsBeam = true
            });
            PushEvent(SimEventType.Muzzle, troop.Pos, troop.Team);
        }

        void TryFireMecha(Mecha mecha)
        {
            if (mecha.Hp <= 0f || mecha.FireCooldown > 0f)
                return;

            mecha.FireCooldown = BattleTuning.MechaFireInterval;
            mecha.JustFired = true;
            SimVec2 dir = SimVec2.FromAngle(mecha.Facing);
            var origin = mecha.Pos + dir * (BattleTuning.MechaRadius + 0.3f);
            _shots.Add(new Shot
            {
                Team = mecha.Team,
                Pos = origin,
                Origin = origin,
                Vel = dir * BattleTuning.MechaShotSpeed,
                Life = BattleTuning.MechaShotLife,
                Radius = 0.14f,
                IsBeam = false
            });
            PushEvent(SimEventType.Muzzle, origin, mecha.Team);
        }

        void StepShots(float dt)
        {
            for (int i = _shots.Count - 1; i >= 0; i--)
            {
                Shot shot = _shots[i];
                shot.Age += dt;
                if (shot.IsBeam)
                {
                    if (shot.Age >= shot.Life)
                        _shots.RemoveAt(i);
                    continue;
                }

                SimVec2 start = shot.Pos;
                SimVec2 delta = shot.Vel * dt;
                float distance = delta.Magnitude;
                int steps = Math.Max(1, (int)MathF.Ceiling(distance / 0.22f));
                bool dead = shot.Age >= shot.Life;
                for (int step = 1; step <= steps && !dead; step++)
                {
                    SimVec2 point = start + delta * (step / (float)steps);
                    shot.Pos = point;
                    if (!InsideArena(point, 0f) || HitsObstacle(point, shot.Radius))
                    {
                        PushEvent(SimEventType.Hit, point, shot.Team);
                        dead = true;
                        break;
                    }

                    if (HitUnits(shot, point))
                        dead = true;
                }

                if (dead)
                    _shots.RemoveAt(i);
            }
        }

        bool HitUnits(Shot shot, SimVec2 point)
        {
            for (int i = 0; i < _mechas.Count; i++)
            {
                Mecha mecha = _mechas[i];
                if (mecha.Team == shot.Team || mecha.Hp <= 0f)
                    continue;
                float reach = shot.Radius + BattleTuning.MechaRadius;
                if (SimVec2.DistanceSq(point, mecha.Pos) > reach * reach)
                    continue;
                HurtMecha(mecha, BattleTuning.MechaShotDamage, point);
                return true;
            }

            for (int i = 0; i < _troops.Count; i++)
            {
                Troop troop = _troops[i];
                if (troop.Team == shot.Team || troop.Hp <= 0f)
                    continue;
                float reach = shot.Radius + Radius(troop.Kind);
                if (SimVec2.DistanceSq(point, troop.Pos) > reach * reach)
                    continue;
                HurtTroop(troop, BattleTuning.MechaShotVsTroop, point);
                return true;
            }

            return false;
        }

        void HurtMecha(Mecha mecha, float damage, SimVec2 pos)
        {
            if (mecha.Hp <= 0f)
                return;
            mecha.Hp -= damage;
            if (mecha.Hp < 0f)
                mecha.Hp = 0f;
            PushEvent(SimEventType.Hit, pos, mecha.Team);
            if (mecha.Hp <= 0f)
                PushEvent(SimEventType.Death, mecha.Pos, mecha.Team);
        }

        void HurtTroop(Troop troop, float damage, SimVec2 pos)
        {
            if (troop.Hp <= 0f)
                return;
            troop.Hp -= damage;
            if (troop.Hp < 0f)
                troop.Hp = 0f;
            PushEvent(SimEventType.Hit, pos, troop.Team);
            if (troop.Hp <= 0f)
                PushEvent(SimEventType.Death, troop.Pos, troop.Team);
        }

        void AutoDeploy(Mecha mecha)
        {
            if (mecha.Hp <= 0f)
                return;
            int have = CountTroops(mecha.Team);
            int foe = CountTroops(mecha.Team == 0 ? 1 : 0);
            if (have >= BattleTuning.MaxTroops)
                return;
            if (foe > have && mecha.DeployPoints >= BattleTuning.HeavyCost && have + BattleTuning.HeavySquad <= BattleTuning.MaxTroops)
                SpawnSquad(mecha, TroopKind.Heavy, false);
            else if (mecha.DeployPoints >= BattleTuning.RifleCost && have + BattleTuning.RifleSquad <= BattleTuning.MaxTroops)
                SpawnSquad(mecha, TroopKind.Rifle, false);
        }

        void SpawnSquad(Mecha mecha, TroopKind kind, bool free)
        {
            int count = kind == TroopKind.Rifle ? BattleTuning.RifleSquad : BattleTuning.HeavySquad;
            float cost = kind == TroopKind.Rifle ? BattleTuning.RifleCost : BattleTuning.HeavyCost;
            if (!free)
            {
                if (mecha.DeployPoints + 0.001f < cost)
                {
                    if (mecha.IsPlayer)
                        SetNotice(BattleNotice.NeedPoints);
                    return;
                }

                if (CountTroops(mecha.Team) + count > BattleTuning.MaxTroops)
                {
                    if (mecha.IsPlayer)
                        SetNotice(BattleNotice.TroopCap);
                    return;
                }

                mecha.DeployPoints -= cost;
                if (mecha.IsPlayer)
                    SetNotice(BattleNotice.Deployed);
            }
            else if (CountTroops(mecha.Team) + count > BattleTuning.MaxTroops)
            {
                return;
            }

            SimVec2 back = SimVec2.FromAngle(mecha.Facing + MathF.PI);
            var side = new SimVec2(-back.Y, back.X);
            for (int i = 0; i < count; i++)
            {
                float lateral = (i - (count - 1) * 0.5f) * 0.9f;
                float radius = Radius(kind);
                SimVec2 pos = mecha.Pos + back * (BattleTuning.MechaRadius + 1.4f) + side * lateral;
                pos = FindClear(pos, radius);
                _troops.Add(new Troop
                {
                    Id = _nextId++,
                    Team = mecha.Team,
                    Kind = kind,
                    Pos = pos,
                    Facing = mecha.Facing,
                    Hp = kind == TroopKind.Heavy ? BattleTuning.HeavyHp : BattleTuning.RifleHp,
                    FireCooldown = _rng.Range(0f, 0.35f),
                    Slot = _nextSlot++
                });
            }

            PushEvent(SimEventType.Deploy, mecha.Pos, mecha.Team);
        }

        void ApplyMotion(ref SimVec2 pos, ref SimVec2 vel, SimVec2 direction, float mag, float speed, float accel, float radius, float dt)
        {
            SimVec2 steered = mag <= 0.001f ? SimVec2.Zero : Steer(pos, radius, direction);
            SimVec2 desired = steered * (speed * mag);
            vel = SimVec2.MoveTowards(vel, desired, accel * dt);
            SimVec2 next = Slide(pos, vel * dt, radius);
            if ((next - pos).SqrMagnitude < vel.SqrMagnitude * dt * dt * 0.2f)
                vel = SimVec2.Zero;
            pos = next;
        }

        SimVec2 Steer(SimVec2 pos, float radius, SimVec2 direction)
        {
            if (direction.SqrMagnitude < 1e-6f)
                return SimVec2.Zero;
            direction = SimVec2.Normalize(direction);
            float look = radius + 0.9f;
            if (!Blocked(pos + direction * look, radius))
                return direction;

            var left = new SimVec2(-direction.Y, direction.X);
            var right = new SimVec2(direction.Y, -direction.X);
            bool openLeft = !Blocked(pos + left * look, radius);
            bool openRight = !Blocked(pos + right * look, radius);
            if (openLeft && !openRight)
                return SimVec2.Normalize(direction + left * 1.25f);
            if (openRight && !openLeft)
                return SimVec2.Normalize(direction + right * 1.25f);
            if (openLeft)
            {
                float side = (pos.X + pos.Y >= 0f) ? 1f : -1f;
                return SimVec2.Normalize(direction + left * side);
            }

            return direction;
        }

        SimVec2 Slide(SimVec2 pos, SimVec2 delta, float radius)
        {
            SimVec2 next = pos + delta;
            if (!Blocked(next, radius))
                return next;
            var xOnly = new SimVec2(pos.X + delta.X, pos.Y);
            if (!Blocked(xOnly, radius))
                return xOnly;
            var yOnly = new SimVec2(pos.X, pos.Y + delta.Y);
            if (!Blocked(yOnly, radius))
                return yOnly;
            return pos;
        }

        void Separate()
        {
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < _troops.Count; i++)
                {
                    Troop a = _troops[i];
                    if (a.Hp <= 0f)
                        continue;
                    float ra = Radius(a.Kind);
                    for (int j = i + 1; j < _troops.Count; j++)
                    {
                        Troop b = _troops[j];
                        if (b.Hp <= 0f)
                            continue;
                        PushApart(ref a.Pos, ra, ref b.Pos, Radius(b.Kind));
                    }

                    for (int m = 0; m < _mechas.Count; m++)
                    {
                        Mecha mecha = _mechas[m];
                        if (mecha.Hp <= 0f)
                            continue;
                        float min = ra + BattleTuning.MechaRadius + 0.08f;
                        SimVec2 away = a.Pos - mecha.Pos;
                        float sq = away.SqrMagnitude;
                        if (sq >= min * min)
                            continue;
                        SimVec2 n = sq < 1e-6f ? new SimVec2(1f, 0f) : SimVec2.Normalize(away);
                        a.Pos = mecha.Pos + n * min;
                    }

                    a.Pos = PushOutOfObstacles(a.Pos, ra);
                    a.Pos = ClampToArena(a.Pos, ra);
                }

                if (_mechas.Count == 2 && Player.Hp > 0f && Enemy.Hp > 0f)
                    PushApart(ref Player.Pos, BattleTuning.MechaRadius, ref Enemy.Pos, BattleTuning.MechaRadius);

                for (int m = 0; m < _mechas.Count; m++)
                {
                    Mecha mecha = _mechas[m];
                    mecha.Pos = PushOutOfObstacles(mecha.Pos, BattleTuning.MechaRadius);
                    mecha.Pos = ClampToArena(mecha.Pos, BattleTuning.MechaRadius);
                }
            }
        }

        static void PushApart(ref SimVec2 a, float ra, ref SimVec2 b, float rb)
        {
            SimVec2 delta = b - a;
            float min = ra + rb;
            float sq = delta.SqrMagnitude;
            if (sq >= min * min)
                return;
            SimVec2 n = sq < 1e-6f ? new SimVec2(1f, 0f) : SimVec2.Normalize(delta);
            float push = (min - MathF.Sqrt(MathF.Max(sq, 0f))) * 0.5f;
            a -= n * push;
            b += n * push;
        }

        void Cull()
        {
            for (int i = _troops.Count - 1; i >= 0; i--)
            {
                if (_troops[i].Hp <= 0f)
                    _troops.RemoveAt(i);
            }
        }

        void ResolveOutcome()
        {
            bool playerDead = Player.Hp <= 0f;
            bool enemyDead = Enemy.Hp <= 0f;
            if (playerDead && enemyDead)
                Outcome = BattleOutcome.Draw;
            else if (enemyDead)
                Outcome = BattleOutcome.Victory;
            else if (playerDead)
                Outcome = BattleOutcome.Defeat;
        }

        Mecha AddMecha(int team, bool player, SimVec2 pos, float facing)
        {
            var mecha = new Mecha
            {
                Id = _nextId++,
                Team = team,
                IsPlayer = player,
                Pos = pos,
                Facing = facing,
                Hp = BattleTuning.MechaHp,
                DeployPoints = 30f,
                DashDir = SimVec2.FromAngle(facing)
            };
            _mechas.Add(mecha);
            return mecha;
        }

        void AddObstacle(float x, float y, float halfX, float halfY)
        {
            _obstacles.Add(new Obstacle
            {
                Center = new SimVec2(x, y),
                Half = new SimVec2(halfX, halfY)
            });
        }

        Troop FindTroop(int id)
        {
            for (int i = 0; i < _troops.Count; i++)
            {
                if (_troops[i].Id == id)
                    return _troops[i];
            }
            return null;
        }

        void TurnToward(Mecha mecha, SimVec2 direction, float rate, float dt)
        {
            TurnFacing(ref mecha.Facing, direction, rate, dt);
        }

        static void TurnFacing(ref float facing, SimVec2 direction, float rate, float dt)
        {
            if (direction.SqrMagnitude < 1e-6f)
                return;
            float delta = SimGeom.DeltaAngle(facing, direction.Angle);
            float max = rate * dt;
            if (delta > max) delta = max;
            else if (delta < -max) delta = -max;
            facing = SimGeom.Wrap(facing + delta);
        }

        SimVec2 FindClear(SimVec2 pos, float radius)
        {
            if (!Blocked(pos, radius))
                return pos;
            for (int ring = 1; ring <= 8; ring++)
            {
                for (int k = 0; k < 8; k++)
                {
                    SimVec2 candidate = pos + SimVec2.FromAngle(k * (MathF.PI * 0.25f)) * (ring * 0.6f);
                    if (!Blocked(candidate, radius))
                        return candidate;
                }
            }
            return ClampToArena(pos, radius);
        }

        SimVec2 PushOutOfObstacles(SimVec2 pos, float radius)
        {
            for (int i = 0; i < _obstacles.Count; i++)
            {
                Obstacle obstacle = _obstacles[i];
                if (!SimGeom.CircleAabb(pos, radius, obstacle.Center, obstacle.Half))
                    continue;
                float left = obstacle.Center.X - obstacle.Half.X - radius;
                float right = obstacle.Center.X + obstacle.Half.X + radius;
                float down = obstacle.Center.Y - obstacle.Half.Y - radius;
                float up = obstacle.Center.Y + obstacle.Half.Y + radius;
                float toLeft = MathF.Abs(pos.X - left);
                float toRight = MathF.Abs(right - pos.X);
                float toDown = MathF.Abs(pos.Y - down);
                float toUp = MathF.Abs(up - pos.Y);
                float best = toLeft;
                SimVec2 resolved = new SimVec2(left, pos.Y);
                if (toRight < best) { best = toRight; resolved = new SimVec2(right, pos.Y); }
                if (toDown < best) { best = toDown; resolved = new SimVec2(pos.X, down); }
                if (toUp < best) resolved = new SimVec2(pos.X, up);
                pos = resolved;
            }
            return pos;
        }

        bool Blocked(SimVec2 pos, float radius) => !InsideArena(pos, radius) || HitsObstacle(pos, radius);

        bool HitsObstacle(SimVec2 pos, float radius)
        {
            for (int i = 0; i < _obstacles.Count; i++)
            {
                if (SimGeom.CircleAabb(pos, radius, _obstacles[i].Center, _obstacles[i].Half))
                    return true;
            }
            return false;
        }

        static bool InsideArena(SimVec2 pos, float radius)
        {
            return pos.X >= -BattleTuning.ArenaHalfX + radius
                && pos.X <= BattleTuning.ArenaHalfX - radius
                && pos.Y >= -BattleTuning.ArenaHalfY + radius
                && pos.Y <= BattleTuning.ArenaHalfY - radius;
        }

        static SimVec2 ClampToArena(SimVec2 pos, float radius)
        {
            pos.X = SimGeom.Clamp(pos.X, -BattleTuning.ArenaHalfX + radius, BattleTuning.ArenaHalfX - radius);
            pos.Y = SimGeom.Clamp(pos.Y, -BattleTuning.ArenaHalfY + radius, BattleTuning.ArenaHalfY - radius);
            return pos;
        }

        static float Radius(TroopKind kind) => kind == TroopKind.Heavy ? BattleTuning.HeavyRadius : BattleTuning.RifleRadius;

        void SetNotice(BattleNotice notice)
        {
            Notice = notice;
            NoticeTime = 1.2f;
        }

        void PushEvent(SimEventType type, SimVec2 pos, int team)
        {
            if (Events.Count > 320)
                Events.RemoveRange(0, 160);
            Events.Add(new SimEvent { Type = type, Pos = pos, Team = team });
        }
    }
}

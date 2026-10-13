namespace MechaRts.Sim
{
    public enum BattleOutcome
    {
        Ongoing,
        Victory,
        Defeat,
        Draw
    }

    public enum BattleMode
    {
        Skirmish,
        Duel,
        Colossus
    }

    public enum TroopRole
    {
        Attack,
        Tank,
        Support
    }

    public enum TroopKind
    {
        Rifle,
        Cannon,
        Vanguard,
        Heavy,
        Banner,
        Medic
    }

    public enum BattleNotice
    {
        None,
        NeedPoints,
        TroopCap,
        Deployed,
        OutOfRange
    }

    public enum SimEventType
    {
        Muzzle,
        Hit,
        Death,
        Deploy
    }

    public struct BattleCommand
    {
        public float MoveX;
        public float MoveY;
        public float AimX;
        public float AimY;
        public bool Fire;
        public bool Dash;
        public bool ToggleHover;
        public bool Melee;
        public float Climb;
        public bool DeployRifle;
        public bool DeployHeavy;
        public bool DeployAt;
        public TroopKind DeployKind;
        public float DeployX;
        public float DeployY;
    }

    public struct SimEvent
    {
        public SimEventType Type;
        public SimVec2 Pos;
        public int Team;
    }

    public sealed class Mecha
    {
        public int Id;
        public int Team;
        public bool IsPlayer;
        public SimVec2 Pos;
        public SimVec2 Vel;
        public SimVec2 DashDir;
        public float Facing;
        public float Hp;
        public float FireCooldown;
        public float DashCooldown;
        public float DashTime;
        public float DeployPoints;
        public float Altitude;
        public float FlightTarget;
        public bool WantHover;
        public float StanceCooldown;
        public float HitRadius;
        public float MaxHp;
        public float MoveSpeed;
        public float TurnRate;
        public float ShotDamage;
        public float FireInterval;
        public float MeleeDamage;
        public float MeleeInterval;
        public float MeleeCooldown;
        public float AttackMul;
        public float BodyScale;
        public bool JustFired;
    }

    public sealed class Troop
    {
        public int Id;
        public int Team;
        public TroopKind Kind;
        public SimVec2 Pos;
        public SimVec2 Vel;
        public float Facing;
        public float Hp;
        public float MaxHp;
        public float AttackMul;
        public float FireCooldown;
        public int Slot;
        public bool JustFired;
    }

    public sealed class Shot
    {
        public int Team;
        public SimVec2 Pos;
        public SimVec2 Origin;
        public SimVec2 Vel;
        public float Age;
        public float Life;
        public float Radius;
        public float Damage;
        public float Height;
        public bool IsBeam;
        public bool IgnoresCover;
    }

    public sealed class Obstacle
    {
        public SimVec2 Center;
        public SimVec2 Half;
    }

    public sealed class BossPart
    {
        public string Name;
        public SimVec2 Offset;
        public float Radius;
        public float Hp;
        public float MaxHp;
    }

    public struct SimRng
    {
        uint _state;

        public SimRng(int seed)
        {
            _state = (uint)seed;
            if (_state == 0)
                _state = 1u;
        }

        public float NextFloat()
        {
            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;
            return (_state & 16777215u) / 16777216f;
        }

        public float Range(float min, float max) => min + (max - min) * NextFloat();
    }
}

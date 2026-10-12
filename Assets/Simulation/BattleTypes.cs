namespace MechaRts.Sim
{
    public enum BattleOutcome
    {
        Ongoing,
        Victory,
        Defeat,
        Draw
    }

    public enum TroopKind
    {
        Rifle,
        Heavy
    }

    public enum BattleNotice
    {
        None,
        NeedPoints,
        TroopCap,
        Deployed
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
        public bool DeployRifle;
        public bool DeployHeavy;
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
        public bool IsBeam;
    }

    public sealed class Obstacle
    {
        public SimVec2 Center;
        public SimVec2 Half;
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

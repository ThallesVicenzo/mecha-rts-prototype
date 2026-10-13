namespace MechaRts.Sim
{
    public struct TroopStats
    {
        public TroopRole Role;
        public string Name;
        public float Hp;
        public float Speed;
        public float Range;
        public float Interval;
        public float Radius;
        public float Prefer;
        public float VsMecha;
        public float VsTroop;
        public float Cost;
        public int Squad;
        public bool Melee;
        public float Aura;
        public float AttackBuff;
        public float HealPerSecond;
    }

    public static class TroopCatalog
    {
        public static TroopStats Get(TroopKind kind)
        {
            switch (kind)
            {
                case TroopKind.Cannon: return Cannon();
                case TroopKind.Vanguard: return Vanguard();
                case TroopKind.Heavy: return Bulwark();
                case TroopKind.Banner: return Banner();
                case TroopKind.Medic: return Medic();
                default: return Rifle();
            }
        }

        public static TroopStats Rifle()
        {
            return new TroopStats
            {
                Role = TroopRole.Attack,
                Name = "Fuzil",
                Hp = BattleTuning.RifleHp,
                Speed = BattleTuning.RifleSpeed,
                Range = BattleTuning.RifleRange,
                Interval = BattleTuning.RifleInterval,
                Radius = BattleTuning.RifleRadius,
                Prefer = BattleTuning.RiflePrefer,
                VsMecha = BattleTuning.RifleVsMecha,
                VsTroop = BattleTuning.RifleVsTroop,
                Cost = BattleTuning.RifleCost,
                Squad = BattleTuning.RifleSquad,
                Melee = false
            };
        }

        public static TroopStats Cannon()
        {
            return new TroopStats
            {
                Role = TroopRole.Attack,
                Name = "Canhão",
                Hp = 48f,
                Speed = 3.6f,
                Range = 9.2f,
                Interval = 1.15f,
                Radius = 0.46f,
                Prefer = 7.6f,
                VsMecha = 9f,
                VsTroop = 26f,
                Cost = 36f,
                Squad = 2,
                Melee = false
            };
        }

        public static TroopStats Vanguard()
        {
            return new TroopStats
            {
                Role = TroopRole.Tank,
                Name = "Vanguarda",
                Hp = 96f,
                Speed = 4.4f,
                Range = 1.55f,
                Interval = 0.72f,
                Radius = 0.46f,
                Prefer = 1.35f,
                VsMecha = 11f,
                VsTroop = 22f,
                Cost = 28f,
                Squad = 3,
                Melee = true
            };
        }

        public static TroopStats Bulwark()
        {
            return new TroopStats
            {
                Role = TroopRole.Tank,
                Name = "Baluarte",
                Hp = 160f,
                Speed = 3.1f,
                Range = 1.85f,
                Interval = 1.05f,
                Radius = 0.56f,
                Prefer = 1.55f,
                VsMecha = 16f,
                VsTroop = 30f,
                Cost = BattleTuning.HeavyCost,
                Squad = BattleTuning.HeavySquad,
                Melee = true
            };
        }

        public static TroopStats Banner()
        {
            return new TroopStats
            {
                Role = TroopRole.Support,
                Name = "Estandarte",
                Hp = 42f,
                Speed = 4.6f,
                Range = 0f,
                Interval = 0f,
                Radius = 0.4f,
                Prefer = 3.2f,
                Cost = 30f,
                Squad = 1,
                Melee = false,
                Aura = 6.2f,
                AttackBuff = 0.4f
            };
        }

        public static TroopStats Medic()
        {
            return new TroopStats
            {
                Role = TroopRole.Support,
                Name = "Médico",
                Hp = 40f,
                Speed = 5f,
                Range = 0f,
                Interval = 0f,
                Radius = 0.38f,
                Prefer = 3f,
                Cost = 30f,
                Squad = 1,
                Melee = false,
                Aura = 5.4f,
                HealPerSecond = 14f
            };
        }
    }
}

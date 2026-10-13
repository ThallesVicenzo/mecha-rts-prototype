namespace MechaRts.Sim
{
    /// <summary>
    /// Combat numbers for the prototype. This is the file to tweak when a fight feels wrong.
    /// </summary>
    public static class BattleTuning
    {
        public const float ArenaHalfX = 20f;
        public const float ArenaHalfY = 13.5f;

        public const float MechaHp = 1600f;
        public const float MechaRadius = 1.25f;
        public const float MechaSpeed = 8.2f;
        public const float MechaAccel = 48f;
        public const float MechaTurnRate = 6.5f;
        public const float MechaFireInterval = 0.38f;
        public const float MechaShotSpeed = 24f;
        public const float MechaShotDamage = 42f;
        public const float MechaShotVsTroop = 50f;
        public const float MechaShotLife = 1.35f;
        public const float DashSpeed = 19f;
        public const float DashDuration = 0.18f;
        public const float DashCooldown = 1.35f;

        public const float HoverClear = 0.62f;
        public const float HoverRise = 0.72f;
        public const float HoverSpeed = 11.2f;
        public const float StanceCooldown = 1.6f;
        public const float HoverTroopDamage = 1.45f;
        public const float AntiAirAggro = 11.5f;
        public const float GroundCamera = 12.2f;
        public const float HoverCamera = 15.6f;
        public const float HoverVisualLift = 1.9f;

        public const float DeployRange = 15f;
        public const float ShotHeightWindow = 2.8f;
        public const float FlightRise = 4.2f;
        public const float MaxDeploy = 100f;
        public const float DeployRegen = 9f;
        public const float RifleCost = 24f;
        public const float HeavyCost = 34f;
        public const int RifleSquad = 4;
        public const int HeavySquad = 2;
        public const int MaxTroops = 16;

        public const float RifleHp = 34f;
        public const float RifleSpeed = 5.4f;
        public const float RifleRange = 6.2f;
        public const float RifleInterval = 0.48f;
        public const float RifleVsMecha = 3.2f;
        public const float RifleVsTroop = 12f;
        public const float RifleRadius = 0.36f;
        public const float RiflePrefer = 4.6f;

        public const float HeavyHp = 88f;
        public const float HeavySpeed = 3.5f;
        public const float HeavyRange = 7.4f;
        public const float HeavyInterval = 1.05f;
        public const float HeavyVsMecha = 8.5f;
        public const float HeavyVsTroop = 24f;
        public const float HeavyRadius = 0.5f;
        public const float HeavyPrefer = 6.4f;

        public const float TroopAccel = 36f;
        public const float TroopTurnRate = 9f;
        public const float PlayerAggro = 8.2f;
        public const float PlayerRallyAggro = 5.2f;
        public const float EnemyAggro = 9f;

        public const float EnemyRangeMin = 8.5f;
        public const float EnemyRangeMax = 13.5f;
    }
}

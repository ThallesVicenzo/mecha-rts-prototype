using System;

namespace MechaRts.Sim
{
    public static class BattleSelfCheck
    {
        public static string Run()
        {
            string error = MovementChangesPosition();
            if (error != null) return error;
            error = ShotDamagesEnemy();
            if (error != null) return error;
            error = CoverBlocksShot();
            if (error != null) return error;
            error = DeploySpendsAndSpawns();
            if (error != null) return error;
            error = DeathEndsBattle();
            if (error != null) return error;
            error = LongFightStaysFinite();
            if (error != null) return error;
            return null;
        }

        static string MovementChangesPosition()
        {
            var world = new BattleWorld(1);
            world.AiEnabled = false;
            float start = world.Player.Pos.Y;
            var command = new BattleCommand { MoveY = 1f, AimX = 0f, AimY = 20f };
            for (int i = 0; i < 60; i++)
                world.Tick(BattleWorld.TickDt, command);
            if (world.Player.Pos.Y < start + 2f)
                return "player mecha did not move forward";
            if (!Finite(world.Player.Pos))
                return "player position became non-finite";
            return null;
        }

        static string ShotDamagesEnemy()
        {
            var world = new BattleWorld(2);
            world.AiEnabled = false;
            world.Player.Pos = new SimVec2(-6f, -8f);
            world.Player.Facing = 0f;
            world.Enemy.Pos = new SimVec2(-2f, -8f);
            var command = new BattleCommand { Fire = true, AimX = 8f, AimY = -8f };
            for (int i = 0; i < 50; i++)
                world.Tick(BattleWorld.TickDt, command);
            if (world.Enemy.Hp > BattleTuning.MechaHp - 30f)
                return "mecha shot did not damage the enemy. hp=" + world.Enemy.Hp.ToString("0");
            return null;
        }

        static string CoverBlocksShot()
        {
            var world = new BattleWorld(3);
            world.AiEnabled = false;
            world.Player.Pos = new SimVec2(0f, -4f);
            world.Player.Facing = MathF.PI * 0.5f;
            world.Enemy.Pos = new SimVec2(0f, 4f);
            if (world.HasLineOfSight(world.Player.Pos, world.Enemy.Pos))
                return "center cover should block line of sight";
            var command = new BattleCommand { Fire = true, AimX = 0f, AimY = 12f };
            for (int i = 0; i < 40; i++)
                world.Tick(BattleWorld.TickDt, command);
            if (world.Enemy.Hp < BattleTuning.MechaHp - 0.5f)
                return "shot passed through cover";
            return null;
        }

        static string DeploySpendsAndSpawns()
        {
            var world = new BattleWorld(4);
            world.AiEnabled = false;
            int before = world.CountTroops(0);
            float points = world.Player.DeployPoints;
            world.Tick(BattleWorld.TickDt, new BattleCommand { DeployRifle = true });
            if (world.CountTroops(0) != before + BattleTuning.RifleSquad)
                return "rifle deploy did not spawn a squad";
            if (world.Player.DeployPoints > points - BattleTuning.RifleCost + 1f)
                return "rifle deploy did not spend mobilization";
            world.Player.DeployPoints = 0f;
            int capped = world.CountTroops(0);
            world.Tick(BattleWorld.TickDt, new BattleCommand { DeployHeavy = true });
            if (world.CountTroops(0) != capped)
                return "deploy succeeded without mobilization";
            if (world.Notice != BattleNotice.NeedPoints)
                return "missing mobilization notice";
            return null;
        }

        static string DeathEndsBattle()
        {
            var world = new BattleWorld(5);
            world.AiEnabled = false;
            world.Enemy.Hp = 0f;
            world.Tick(BattleWorld.TickDt, default);
            if (world.Outcome != BattleOutcome.Victory)
                return "destroying the enemy mecha should end the battle";
            return null;
        }

        static string LongFightStaysFinite()
        {
            var world = new BattleWorld(6);
            var command = new BattleCommand { MoveX = 1f, MoveY = 0.2f, Fire = true, AimX = 0f, AimY = 9f };
            for (int i = 0; i < 1200; i++)
            {
                if (i == 90)
                    command.DeployRifle = true;
                else
                    command.DeployRifle = false;
                if (i == 200)
                    world.SetRally(0f, 8f);
                world.Tick(BattleWorld.TickDt, command);
                if (world.Events.Count > 200)
                    world.ClearEvents();
            }

            if (!Finite(world.Player.Pos) || !Finite(world.Enemy.Pos))
                return "mecha position became non-finite";
            if (!float.IsFinite(world.Player.Hp) || !float.IsFinite(world.Enemy.Hp))
                return "mecha hp became non-finite";
            if (world.CountTroops(0) > BattleTuning.MaxTroops || world.CountTroops(1) > BattleTuning.MaxTroops)
                return "troop cap exceeded";
            for (int i = 0; i < world.Troops.Count; i++)
            {
                if (!Finite(world.Troops[i].Pos) || !float.IsFinite(world.Troops[i].Hp))
                    return "troop state became non-finite";
            }
            return null;
        }

        static bool Finite(SimVec2 v) => float.IsFinite(v.X) && float.IsFinite(v.Y);
    }
}

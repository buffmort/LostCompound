using Environment = LostCompound.MapEnvironment.Environment;
using LostCompound.MapEnvironment;
using LostCompound.Monsters;

namespace LostCompound.Tools
{
    public static class Tools
    {

        /**
         * 战斗主循环
         * 大回合 = roundSetup + 玩家小回合 + 怪物小回合
         * 跳过回合逻辑封装在 Monster.onMonsterTurn 内部
         */
        public static void RunBattle(Player p, List<Monster> monsters, Environment env)
        {
            Console.WriteLine("=======战斗开始=======");
            int round = 1;

            while (p.IsAlive && monsters.Count > 0)
            {

                // ── 大回合开场：DOT 结算一次 ──
                ApplyDotEffects(p, monsters, round);

                // ── 环境推进：每个大回合一次（如熔岩瀑布每回合掉水）──
                env?.OnBattleTurn();

                RemoveDeadMonsters(p, monsters);
                if (!p.IsAlive || monsters.Count == 0) break;

                // ── 小回合 A：玩家行动 ──
                p.OnPlayerTurn(monsters, env?.Type ?? EnvType.None);
                RemoveDeadMonsters(p, monsters);
                if (!p.IsAlive || monsters.Count == 0) break;

                // ── 小回合 B：怪物行动（skipTurn 内部自理）──
                foreach (Monster m in monsters)
                {
                    if (!p.IsAlive) break;
                    if (!m.IsAlive) continue;
                    m.OnMonsterTurn(p);
                }
                RemoveDeadMonsters(p, monsters);

                round++;
            }

            Console.WriteLine(p.IsAlive ? "战斗结束 你赢了!" : "你死了!");
        }

        /**
         * 大回合开场：结算所有 DOT
         * DOT 走 takeDmg → 会消耗格挡(block) 一次
         * 纪律：只扣血，不调 die()，收尸统一交给 cleanupDead
         */
        private static void ApplyDotEffects(Player p, List<Monster> monsters, int round)
        {
            Console.WriteLine("=====第" + round + "回合=====");
            Calculator.Delay(250);

            // 怪物 DOT
            foreach (Monster m in monsters)
            {
                if (!m.IsAlive || m.DotDuration <= 0) continue;

                Console.WriteLine(m.Name + " 受到 " + m.DotDamage + " 点持续伤害");
                m.TakeDamage(m.DotDamage);
                m.DecreaseDotDuration();
                if (m.DotDuration <= 0) m.ClearDot();
            }

            // 玩家 DOT
            if (p.IsAlive && p.DotDuration > 0)
            {
                Console.WriteLine(" 你受到 " + p.DotDamage + " 点持续伤害");
                p.TakeDamage(p.DotDamage);
                p.DecreaseDotDuration();
                if (p.DotDuration <= 0) p.ClearDot();
            }
        }

        /**
         * 死亡统一出口：发掉落 + 移除
         * 依赖 Monster.die(Player) 幂等（内部 lootDropped 标志），
         * 否则 DOT 击杀 + 技能补刀会双发掉落
         */
        private static void RemoveDeadMonsters(Player p, List<Monster> monsters)
        {
            for (int i = 0; i < monsters.Count; i++)
            {
                Monster m = monsters[i];
                if (!m.IsAlive)
                {
                    m.Die(p);
                    monsters.RemoveAt(i);
                    i--;
                }
            }
        }
    }
}

using Environment = LostCompound.MapEnvironment.Environment;
using LostCompound.MapEnvironment;
using LostCompound.Monsters;

namespace LostCompound.Tools
{
    public static class Tools
    {

        /**
         * 战斗主循环
         * 大回合 = 回合头/DOT + 环境推进 + 玩家小回合 + 怪物小回合
         *
         * 通用逻辑一律集中在这里，子类和 Player 只保留各自特有的行为：
         *   - 回合头、DOT 结算、收尸（RemoveDeadMonsters）全部由本循环负责；
         *   - 跳过检查统一走 TryConsumeSkipTurn()，避免各子类自己内联一份而漏写
         *     （LavaSlime 原先就漏了，导致热硫的 AddSkipTurn 对它无效）。
         */
        public static void RunBattle(Player p, List<Monster> monsters, Environment env)
        {
            Console.WriteLine("=======战斗开始=======");
            int round = 1;

            // 战斗期间状态栏会显示血量/防御/格挡/攻击力；用 try/finally 保证异常退出也能恢复
            p.SetInBattle(true);
            try
            {
                while (p.IsAlive && monsters.Count > 0)
                {

                    // ── 1. 大回合开场：回合头 + 所有 DOT 结算一次 ──
                    ApplyDotEffects(p, monsters, round);

                    // ── 2. 环境推进：每个大回合一次（如熔岩瀑布每回合掉水）──
                    env?.OnBattleTurn();

                    // 水淹洞穴：水位涨到极限 → 全部对象死亡，玩家直接算输
                    if (env is FloodedCavern flooded && flooded.WaterLevel >= FloodedCavern.MaxWaterLevel)
                    {
                        Console.WriteLine("水位涨到了极限，整个洞穴被淹没——你失去了意识……");
                        p.Hp = 0;
                        break;
                    }

                    RemoveDeadMonsters(p, monsters);
                    if (!p.IsAlive || monsters.Count == 0) break;

                    // ── 3. 玩家回合：先做回合开始的簿记，再判跳过，最后才是行动 ──
                    // 簿记（道具计时/待入包合并/状态栏）即使本回合被跳过也要执行，
                    // 所以它必须在跳过检查之前。
                    p.OnTurnStart(env?.Type ?? EnvType.None);
                    if (!p.TryConsumeSkipTurn())
                    {
                        p.OnPlayerTurn(monsters, env?.Type ?? EnvType.None);
                    }
                    RemoveDeadMonsters(p, monsters);
                    if (!p.IsAlive || monsters.Count == 0) break;

                    // ── 4. 怪物回合：回合头与跳过检查由本循环统一处理 ──
                    foreach (Monster m in monsters)
                    {
                        if (!p.IsAlive) break;
                        if (!m.IsAlive) continue;
                        Console.WriteLine("=====" + m.Name + "回合=====");
                        if (m.TryConsumeSkipTurn()) continue;
                        m.OnMonsterTurn(p);
                    }
                    RemoveDeadMonsters(p, monsters);

                    round++;
                }
            }
            finally
            {
                p.SetInBattle(false);
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

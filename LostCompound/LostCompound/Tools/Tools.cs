using LostCompound.MapEnvironment;
using LostCompound.Monsters;

namespace LostCompound.Tools
{
    public class Tools
    {

        /**
         * 战斗主循环
         * 大回合 = roundSetup + 玩家小回合 + 怪物小回合
         * 跳过回合逻辑封装在 Monster.onMonsterTurn 内部
         */
        public static void Fight(Player p, List<Monster> monsters, EnvType envType)
        {
            Console.WriteLine("=======战斗开始=======");
            int round = 1;

            while (p.isAlive() && monsters.Count > 0)
            {

                // ── 大回合开场：DOT 结算一次 ──
                roundSetup(p, monsters, round);
                cleanupDead(p, monsters);
                if (!p.isAlive() || monsters.Count == 0) break;

                // ── 小回合 A：玩家行动 ──
                p.onPlayerTurn(monsters, envType);
                cleanupDead(p, monsters);
                if (!p.isAlive() || monsters.Count == 0) break;

                // ── 小回合 B：怪物行动（skipTurn 内部自理）──
                foreach (Monster m in monsters)
                {
                    if (!p.isAlive()) break;
                    if (!m.isAlive()) continue;
                    m.onMonsterTurn(p);
                }
                cleanupDead(p, monsters);

                round++;
            }

            Console.WriteLine(p.isAlive() ? "战斗结束 你赢了!" : "你死了!");
        }

        /**
         * 大回合开场：结算所有 DOT
         * DOT 走 takeDmg → 会消耗格挡(block) 一次
         * 纪律：只扣血，不调 die()，收尸统一交给 cleanupDead
         */
        private static void roundSetup(Player p, List<Monster> monsters, int round)
        {
            Console.WriteLine("=====第" + round + "回合=====");
            Calculator.delay(250);

            // 怪物 DOT
            foreach (Monster m in monsters)
            {
                if (!m.isAlive() || m.getDOTdur() <= 0) continue;

                Console.WriteLine(m.getName() + " 受到 " + m.getDOTdmg() + " 点持续伤害");
                m.takeDmg(m.getDOTdmg());
                m.decreaseDOTdur();
                if (m.getDOTdur() <= 0) m.clearDOT();
            }

            // 玩家 DOT
            if (p.isAlive() && p.getDOTdur() > 0)
            {
                Console.WriteLine(" 你受到 " + p.getDOTdmg() + " 点持续伤害");
                p.takeDmg(p.getDOTdmg());
                p.decreaseDOTdur();
                if (p.getDOTdur() <= 0) p.clearDOT();
            }
        }

        /**
         * 死亡统一出口：发掉落 + 移除
         * 依赖 Monster.die(Player) 幂等（内部 lootDropped 标志），
         * 否则 DOT 击杀 + 技能补刀会双发掉落
         */
        private static void cleanupDead(Player p, List<Monster> monsters)
        {
            for (int i = 0; i < monsters.Count; i++)
            {
                Monster m = monsters[i];
                if (!m.isAlive())
                {
                    m.die(p);
                    monsters.RemoveAt(i);
                    i--;
                }
            }
        }
    }
}

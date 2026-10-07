using Environment = LostCompound.MapEnvironment.Environment;
using LostCompound.MapEnvironment;
using LostCompound.Monsters;
using LostCompound.Buffs;

namespace LostCompound.Tools
{
    public static class Tools
    {

        /**
         * 战斗主循环
         * 大回合 = 回合头 + buff（流血）+ 环境推进 + 玩家小回合 + 怪物小回合
         *
         * 通用逻辑一律集中在这里，子类和 Player 只保留各自特有的行为：
         *   - 回合头、buff 结算（流血，含旧 DOT 的全部职责）、收尸（RemoveDeadMonsters）
         *     全部由本循环负责；
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

                    // ── 1. 大回合开场：回合头 ──
                    // 这行原先由 ApplyDotEffects 打印。DOT 机制已删除、那个方法整体删掉了，
                    // 所以必须把它搬到这里，否则战斗日志里再也没有「第N回合」的分隔。
                    Console.WriteLine("=====第" + round + "回合=====");
                    Calculator.Delay(250);   // 与原先 DOT 结算前的停顿一致，节奏不变

                    // ── buff 结算（流血）：唯一的周期性伤害出口 ──
                    // 必须在 RemoveDeadMonsters 之前：buff 打死怪也要走统一收尸，否则漏掉落。
                    ApplyBuffs(p, monsters);

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
                // 战斗结束：清空双方的状态效果。
                // 为什么必须清：流血只在「剩余回合」走完时才消失，而 Player 是跨战斗复用的
                // 同一个对象 —— 不清的话，上一场战斗里没走完的流血会跟着他进后面每一场战斗，
                // 变成「新战斗第 1 回合就白掉一次血」。
                ClearBuffs(p, monsters);
                p.SetInBattle(false);
            }

            Console.WriteLine(p.IsAlive ? "战斗结束 你赢了!" : "你死了!");
        }

        /**
         * 大回合开场：结算所有状态效果（buff，如流血）。
         *
         * 为什么集中在这里（见 CONVENTIONS 2.5）：通用结算只写一份，
         * 各 buff 只实现 OnTurnTick（自己那套规则），时机由本循环统一决定。
         * 位置上就是大回合的最开头、RemoveDeadMonsters 之前：
         *   buff 只扣血、不调 die()，所以被打死的怪一样会走统一收尸（不漏掉落、不双发）。
         *
         * 遍历顺序沿用旧 DOT 结算的顺序：先怪物，后玩家。
         * 倒序遍历是因为结算中要移除失效（剩余回合归零、或伤害归零）的 buff，
         * 正序删会漏掉紧随其后的一个。
         */
        private static void ApplyBuffs(Player p, List<Monster> monsters)
        {
            // 怪物 buff
            foreach (Monster m in monsters)
            {
                if (!m.IsAlive) continue;
                for (int i = m.Buffs.Count - 1; i >= 0; i--)
                {
                    Buff b = m.Buffs[i];
                    if (b.IsExpired)
                    {
                        m.Buffs.RemoveAt(i);
                        continue;
                    }
                    b.OnTurnTick(m);
                    if (b.IsExpired) m.Buffs.RemoveAt(i);
                }
            }

            // 玩家 buff
            if (!p.IsAlive) return;
            for (int i = p.Buffs.Count - 1; i >= 0; i--)
            {
                Buff b = p.Buffs[i];
                if (b.IsExpired)
                {
                    p.Buffs.RemoveAt(i);
                    continue;
                }
                b.OnTurnTick(p);
                if (b.IsExpired) p.Buffs.RemoveAt(i);
            }
        }

        /**
         * 战斗结束时的清理：移除双方身上的全部状态效果。
         *
         * 为什么必须清：流血是「不自然衰减、只在剩余回合归零时消失」的机制（见 BleedBuff 注释），
         * 而 Player 是跨战斗复用的同一个对象 —— 不清的话，玩家在第一场战斗里中的流血
         * 会跟着他进后面每一场战斗，变成每回合无限掉血。
         */
        private static void ClearBuffs(Player p, List<Monster> monsters)
        {
            p.Buffs.Clear();
            foreach (Monster m in monsters)
            {
                m.Buffs.Clear();
            }
        }

        /**
         * 死亡统一出口：发掉落 + 移除
         * 依赖 Monster.die(Player) 幂等（内部 lootDropped 标志），
         * 否则流血击杀 + 技能补刀会双发掉落
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

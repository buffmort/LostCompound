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

            // ── 0. 开战初始化：按环境给怪物写入「环境相关特性」──
            // 为什么放在这里：Monster.TakeDamage(double) 拿不到环境，改它的签名会牵动基类、
            // 所有怪物子类和所有调用点（含 BleedBuff），所以由战斗循环在开打前把
            // 「当前环境 → 怪物抗性」一次性写进怪物自身（见 LavaSlime._highTempImmunity 注释）。
            ApplyEnvironmentMonsterTraits(env, monsters);

            // 战斗期间状态栏会显示血量/防御/护盾/攻击力；用 try/finally 保证异常退出也能恢复
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
                    // 环境只推进自己；它对玩家的影响（如缺水扣 STR）由 Player 重算时读取
                    env?.OnBattleTurn();

                    // 环境自己声明的「玩家必败」条件（如水淹洞穴水位满）→ 全部对象死亡，玩家直接算输。
                    // 为什么改成问能力：原先这里写的是 `env is FloodedCavern`，等于把某个环境的具体
                    // 判负规则搬进了通用战斗循环；以后再加一个会淹死人的环境，就得回来改 RunBattle。
                    if (env?.IsFatalToPlayer == true)
                    {
                        Console.WriteLine(env.FatalMessage);
                        p.Hp = 0;
                        foreach (Monster m in monsters)
                        {
                            // 这里写 0.01 而不是 0 是刻意的：0.01 让 IsAlive（Hp > 0）为 false，
                            // 怪物仍走 RemoveDeadMonsters 的统一收尸出口（发掉落 + 移除），
                            // 直接写 0 会跳过 Die() 的幂等判断路径、漏掉落。
                            m.Hp = 0.01;
                        }
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
                    // 输入流已结束（EOF）：玩家已经没法再操作了，继续循环只会一回合接一回合地
                    // 「自动跳过/放弃行动」空转到死。这里直接结束这场战斗 —— 既不算赢也不算输。
                    if (p.InputClosed)
                    {
                        break;
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

                // 跳过回合计数也必须一起清（本次修掉的一个真 bug）。
                // 与 buff 同一个道理：Player 跨战斗复用，而 SkipTurn 是「还没被消费掉的跳过次数」。
                // 触发路径：双子的「困住你」把 SkipTurn 置 1 之后，同一回合内怪全被流血打死 /
                // 玩家把怪清完 → 战斗直接结束、TryConsumeSkipTurn 还没轮到 → 那 1 点残留下来，
                // 下一场战斗玩家的第一回合被白跳一次（不报错、不崩溃，只是莫名其妙少一回合）。
                // 清在 finally 里而不是循环末尾：异常退出、EOF 中止、战败三条路都覆盖得到。
                p.SkipTurn = 0;

                p.SetInBattle(false);
            }

            if (p.InputClosed)
            {
                // EOF 收场：别报胜负 —— 这一场既没打赢也没打输，是输入没了
                Console.WriteLine("输入已结束，战斗中止。");
            }
            else
            {
                Console.WriteLine(p.IsAlive ? "战斗结束 你赢了!" : "你死了!");
            }
        }

        /**
         * 战斗开始前按当前环境初始化怪物特性。
         *
         * 「环境 × 怪物」的规则写在环境那一侧（Environment.GetDamageImmunity，
         * 如熔岩瀑布对岩浆怪的高温免疫 30%，设计文档六）；这里只做搬运：
         * 逐只怪问当前环境「你对它给多少免疫比例」，再交给怪物自己收下
         * （Monster.ApplyDamageImmunity，没有免疫机制的怪默认忽略）。
         *
         * 为什么这么分（见 CONVENTIONS 2.5）：规则内容属于环境，本方法属于通用流程。
         * 原先这里是 `if (env is not LavaWaterfall) return;` + `if (m is LavaSlime slime)`，
         * 每加一条环境×怪物规则就要在通用循环里再写一个 is。
         */
        private static void ApplyEnvironmentMonsterTraits(Environment env, List<Monster> monsters)
        {
            if (env == null) return;
            foreach (Monster m in monsters)
            {
                m.ApplyDamageImmunity(env.GetDamageImmunity(m));
            }
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
            bool removedAny = false;
            for (int i = p.Buffs.Count - 1; i >= 0; i--)
            {
                Buff b = p.Buffs[i];
                if (b.IsExpired)
                {
                    p.Buffs.RemoveAt(i);
                    removedAny = true;
                    continue;
                }
                b.OnTurnTick(p);
                // 结算完可能刚好到期（最后一回合结算过了），这时才移除 ——
                // 注意不能把它和上面那个「进来就过期」的分支合并：那种情况说明
                // 它已经不该再结算一次了（否则最后一回合会被扣两次）。
                if (b.IsExpired)
                {
                    p.Buffs.RemoveAt(i);
                    removedAny = true;
                }
            }

            // 有 buff 消失就要重算派生值 —— 否则「某 buff 提供 +1 STR」会在它到期后
            // 一直留着那 1 点，因为没有任何地方会去「还原」它（现算机制的意义就在这）。
            if (removedAny) p.RecomputeStats();
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
            p.RecomputeStats();    // buff 全清了，派生值要跟着回到「基础 + 环境修正」
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

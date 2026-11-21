using Environment = LostCompound.MapEnvironment.Environment;
using LostCompound.MapEnvironment;
using LostCompound.Monsters;
using LostCompound.Buffs;

using LostCompound.Tools.Players;
using LostCompound.Tools.Debug;

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

            // 把「本场的队友名单」介绍给每只怪：盾兵要给其它怪加盾、医生要给全员回血，
            // 而它们的 Attack/OnMonsterTurn 签名里只有玩家，拿不到同伴。
            // 必须在列表生成完之后注入（第一只创建时后面的还没生成）。
            foreach (Monster m in monsters)
            {
                m.BattleAllies = monsters;
            }
            try
            {
                // 意图编排者：整场战斗一个
                MonsterIntents intents = new(monsters);

                // ── 回合流程模板（Hyberty 2026-11-21 B 方案）──
                // 顺序现在是**数据**，不再藏在语句和缩进里：一眼能看完，环境还能往副本里追加。
                // 每一步返回 bool：false = 战斗结束（有人死了 / 怪清空了 / 输入流断了）。
                //
                // 「被跳过」这件事在这里就消费掉了（playerSkipped），而不是放在玩家行动里 ——
                // 因为城市地图可能会追加第二次「玩家行动」，
                // 被眩晕的回合应该是**整个回合都不能动**，而不是只少动一次。
                bool playerSkipped = false;

                TurnPipeline template = new();
                template.Add(TurnPipeline.BuffStep, () => SettleBuffs(p, monsters));
                template.Add(TurnPipeline.EnvironmentStep, () => AdvanceEnvironment(p, monsters, env));
                template.Add(TurnPipeline.IntentStep, () => { intents.BeginRound(p); return true; });
                template.Add(TurnPipeline.PlayerTurnStartStep, () =>
                {
                    p.OnTurnStart(env?.Type ?? EnvType.None);
                    playerSkipped = p.TryConsumeSkipTurn();
                    return true;
                });
                template.Add(TurnPipeline.PlayerActionStep, () =>
                    playerSkipped ? true : RunPlayerAction(p, monsters, env));
                template.Add(TurnPipeline.MonsterTurnStep, () => RunMonsterTurns(p, monsters));

                while (p.IsAlive && monsters.Count > 0)
                {
                    // ── 1. 回合头 ──
                    // 这行原先由 ApplyDotEffects 打印。DOT 机制已删除、那个方法整体删掉了，
                    // 所以必须把它搬到这里，否则战斗日志里再也没有「第N回合」的分隔。
                    Console.WriteLine("=====第" + round + "回合=====");
                    BattleDelay.Tick();     // 回合分隔前停一下（关键节点，见 BattleDelay）

                    // 每回合**克隆**一份再让环境追加：
                    // 直接往模板上塞的话，第 N 回合玩家会动 N 次（见 TurnPipeline 类注释）。
                    TurnPipeline roundSteps = template.Clone();
                    env?.ContributeTurnSteps(roundSteps, p, monsters);

                    // 调试模式下把本回合的实际顺序打一次（只在第 1 回合打，不刷屏）。
                    // 改回合结构时靠它确认「环境到底有没有把步骤加进去」。
                    if (round == 1 && DebugState.Enabled)
                    {
                        Console.WriteLine("（回合顺序：" + string.Join(" → ", roundSteps.Order) + "）");
                    }

                    if (!roundSteps.Run()) break;

                    round++;
                }
            }
            finally
            {
                // 战斗结束是**重要节点**：停 200ms，让「你赢了 / 你死了」和最后一行伤害看得清。
                BattleDelay.Tick(BattleDelay.ImportantDelayMs);

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

        // ==================== 回合流程的四个步骤 ====================
        // 每个都返回 bool：false = 战斗结束。

        /// <summary>
        /// 状态结算：buff 每回合的跳伤（流血等）。
        /// 这里**不收尸**：buff 跳死的怪由「环境推进」那一步统一处理，
        /// 保持原有的「环境看到的仍是全量名单」顺序，不改变已有的同归于尽判定。
        /// </summary>
        private static bool SettleBuffs(Player p, List<Monster> monsters)
        {
            ApplyBuffs(p, monsters);
            return true;
        }

        /// <summary>环境推进：掉水 / 涨水位 / 环境判负，然后统一收尸判胜负。</summary>
        private static bool AdvanceEnvironment(Player p, List<Monster> monsters, Environment env)
        {
            // 高温房间（设计文档 §5.1）在这里告诉环境：热量挂在**房间**上，
            // 而环境对象是整层共享的，它自己查不到玩家站在哪一间。
            env?.OnBattleTurn(p.CurrentRoom?.IsHot == true);

            // 环境自己声明的「玩家必败」条件（如水淹洞穴水位满）→ 全部对象死亡，玩家直接算输。
            if (env?.IsFatalToPlayer == true)
            {
                Console.WriteLine(env.FatalMessage);
                p.Hp = 0;
                foreach (Monster m in monsters)
                {
                    // 写 0.01 而不是 0，是为了让 IsAlive（Hp > 0）判定为 false。
                    m.Hp = 0.01;
                }
                return false;
            }

            RemoveDeadMonsters(p, monsters);
            return p.IsAlive && monsters.Count > 0;
        }

        /// <summary>
        /// 玩家行动**一次**（含额外回合）。
        /// 「被跳过」不在这里判断 —— 已由「玩家回合开始」那一步统一消费掉
        /// （城市双动时，被眩晕只会跳过一次，而不是跳一次动一次）。
        /// </summary>
        private static bool RunPlayerAction(Player p, List<Monster> monsters, Environment env)
        {
            p.OnPlayerTurn(monsters, env?.Type ?? EnvType.None);
            BattleDelay.Tick();      // 玩家这次行动之后停 100ms

            // ── 额外回合（ExtraTurn buff）──
            // 每扣一层就**直接再调一次 OnPlayerTurn**：语义是「你多打了一下」，
            // 不是时间倒流重开一轮 —— 所以怪物这一轮只出手一次，
            // 额外回合里也不重走 OnTurnStart（道具耐久、流血都不再涨）。
            // 判断权在 buff 自己身上（Buff.OnPlayerActionEnd），这里只负责「问了、照做」。
            // ⚠️ 每次额外行动后立刻收尸判胜负 —— 否则会继续打一只已经死了的怪。
            while (p.IsAlive && monsters.Count > 0 && p.NotifyPlayerActionEnd())
            {
                Console.WriteLine("=====额外回合=====");
                p.OnPlayerTurn(monsters, env?.Type ?? EnvType.None);
                BattleDelay.Tick();

                RemoveDeadMonsters(p, monsters);
                if (p.InputClosed) break;
            }

            RemoveDeadMonsters(p, monsters);

            // 输入流已结束（EOF）：玩家没法再操作了，继续跑只会空转。
            // 返回 false = 结束这场战斗（既不算赢也不算输）。
            return p.IsAlive && monsters.Count > 0 && !p.InputClosed;
        }

        /// <summary>怪物逐个出手（跳过检查也在这里，每只怪一次）。</summary>
        private static bool RunMonsterTurns(Player p, List<Monster> monsters)
        {
            foreach (Monster m in monsters)
            {
                if (!p.IsAlive) break;
                if (!m.IsAlive) continue;
                Console.WriteLine("=====" + m.Name + "回合=====");

                // ⚠️ 跳过检查**只在这里做一次**。怪物自己的 OnMonsterTurn 里不能再调一遍 ——
                // 那会把「跳过 1 回合」扣成 2 点库存（探针 probe_skip 抓到过）。
                if (m.TryConsumeSkipTurn()) continue;

                m.OnMonsterTurn(p);
                BattleDelay.Tick();      // 每只怪出手之后停一下
            }

            RemoveDeadMonsters(p, monsters);
            return p.IsAlive && monsters.Count > 0;
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
         * 战斗结束时的清理：移除双方身上的状态效果。
         *
         * 为什么必须清：流血是「不自然衰减、只在剩余回合归零时消失」的机制（见 BleedBuff 注释），
         * 而 Player 是跨战斗复用的同一个对象 —— 不清的话，玩家在第一场战斗里中的流血
         * 会跟着他进后面每一场战斗，变成每回合无限掉血。
         *
         * ⚠️ 玩家侧改成走 Player.ClearBattleBuffs()：它会**保留**显式声明了
         *    Buff.SurvivesBattleEnd 的 buff（如常驻诅咒）。默认仍然是全清 ——
         *    例外必须由 buff 自己声明，不会因为加了这个口子就让流血莫名其妙跨战斗。
         */
        private static void ClearBuffs(Player p, List<Monster> monsters)
        {
            p.ClearBattleBuffs();  // 清掉普通 buff，保留"跨战斗"的那些
            p.RecomputeStats();    // buff 变了，派生值要跟着回到「基础 + 环境 + 剩余 buff」
            foreach (Monster m in monsters)
            {
                m.Buffs.Clear();
                // ⚠️ 这里**故意不**跟着重算怪物属性（对称起来看像是漏了一行，其实不是）：
                // 怪物完全不读 Buff.StrBonus / AtkBonus / DefBonus，它的 Atk/Def/Str
                // 就是字段本身、没有「基础值 + 修正」这层结构，所以清了 buff 也没有可回退的东西。
                // 详见 Monster.AddBuff 的说明（Hyberty裁定：明确写死怪物不吃属性修正，
                // 而不是给 Monster 也建一套 RecomputeStats）。
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
                    BattleDelay.Tick();   // 死亡 + 掉落播报之后再停一下，别让掉落一闪而过
                }
            }
        }
    }
}

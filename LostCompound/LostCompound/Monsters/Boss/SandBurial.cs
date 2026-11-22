using LostCompound.Items;
using LostCompound.Tools;
using LostCompound.Tools.Players;

namespace LostCompound.Monsters.Boss
{
    /// <summary>
    /// **沙葬者**（沙漠层专属 Boss，Hyberty 2026-11-22 定）。
    ///
    /// 设计原话（Hyberty）：
    /// <code>
    /// 技能 · 吞噬（每 2 回合必定发动，其余回合普攻）
    /// •有沙：吸走玩家背包全部沙；每吸 1 沙恢复自身 1 血（上限 20），并获得 吸走数量 ÷ 2
    /// •无沙：造成 12 伤害 + 跳过玩家下 1 回合。
    /// •其余回合：普攻 12 伤害，不跳回合。
    /// 时序：第 2、4、6… 回合发动吞噬；第 1、3、5… 回合普攻。
    ///
    /// 设计边界
    /// 1. 吞噬每 2 回合一次是底线，不改每回合 —— 玩家必须保证每 2 回合有一次出手机会。
    /// 2. 唯一解法是「当场把沙变战斗力」（徒手合成压缩沙瓶 / 玻璃罐回血），不是囤沙过战。
    ///    后续加道具时，不要加「让沙能安全囤过 Boss 战」的机制。
    /// </code>
    ///
    /// 三处「偏向玩家」的判定（Hyberty：「计数偏向玩家即可」）：
    ///   · <b>吞噬的间隔只数它自己真出手的次数</b>。被跳过、被眩晕、死了的回合都不会进
    ///     <see cref="OnMonsterTurn"/>（Tools.RunBattle 先 <c>TryConsumeSkipTurn</c>、被跳过就 continue），
    ///     所以玩家丢一个「跳过」到它身上，等于白赚两回合。
    ///   · <b>有沙 / 无沙 在吞噬真正执行的那一刻才判</b>。玩家在自己回合里把沙合成掉、丢掉，
    ///     它这一回合的吞噬就空手 —— 走「无沙」分支。判定不看回合开始的快照。
    ///   · <b>「吸走数量 ÷ 2」向下取整</b>。吸走 3 个沙只补 1 点盾。
    ///
    /// 为什么回血上限是**整场战斗累计**（Hyberty 裁定「每场战斗」）：
    ///   这是本场的核心张力 —— 一场 Boss 战最多靠吸沙回 20 血。如果每回合都能回 20，
    ///   它就成了回血机器，第 2 条设计边界直接失效。上限只看它这场一共回了多少
    ///   （<see cref="_healedByDevour"/>）。
    ///
    /// 关于「护盾每回合重置」（设计书「实现边界」第 2 条）：
    ///   沙葬者的护盾只能从吞噬的沙里来，所以它**按回合清零**等于「只挡一次吞噬周期」——
    ///   玩家在两次吞噬之间把盾打空，那段窗口里的伤害就全是实伤。
    ///   这与玩家侧 <c>Player.ResetShieldForTurn</c> 是同一套「盾按回合重置」口径。
    /// </summary>
    public sealed class SandBurial : Monster
    {
        /// <summary>档位：Boss。毒气弹的「10% 秒杀非 Boss」对它无效，与龙同档。</summary>
        public override MonsterRank Rank => MonsterRank.Boss;

        // ==================== 数值（Hyberty 定，改这里就是改平衡）====================

        /// <summary>普攻伤害（设计书原文：每回合 12）。</summary>
        public const double AttackDamage = 12;

        /// <summary>无沙吞噬时的一次性伤害（设计书原文：12）。</summary>
        public const double EmptyDevourDamage = 12;

        /// <summary>整场战斗靠吞噬回血的上限。</summary>
        public const int MaxHealPerBattle = 20;

        /// <summary>每吸 1 沙恢复的 HP。</summary>
        public const int HealPerSand = 1;

        /// <summary>每吸 2 沙获得 1 点护盾（吸走数量 ÷ 2，向下取整）。</summary>
        public const int SandPerShield = 2;

        /// <summary>吞噬间隔：每 2 次**它自己出手**发动一次。</summary>
        public const int DevourEvery = 3;
        
        /// <summary>基础Str</summary>
        public const double BaseStr = 2;

        /// <summary>掉落沙子数量范围（含两端）。见 <see cref="MonsterLootTable"/>。</summary>
        public const int MinLootSand = 20;
        public const int MaxLootSand = 40;

        /// <summary>
        /// 血量与防御。
        ///
        /// ⚠️ <b>这两个数字设计书里没有</b>，是我按「比龙（80 血 / 5 防）高一档的 Boss」暂定的，
        ///    等 Hyberty 定。改这两行就是改难度，别的地方不用动。
        ///
        /// 为什么防御取 0：它的生存全靠「吞噬来的护盾 + 回血」，
        /// 再加一个固定的 DEF 5 会让每次吸沙的收益被双重放大（回血的对象更值钱）。
        /// </summary>
        private const double BaseHp = 80;
        private const double BaseDef = 5;

        /// <summary>这场战斗里一共靠吞噬回了多少血（判定「上限 20」用）。</summary>
        private int _healedByDevour;

        /// <summary>它自己已经真出手过几次（被跳过的回合不算，见类注释）。</summary>
        private int _turnsTaken;

        public SandBurial()
        {
            Type = MonsterType.SandBurial;
            Name = "沙葬者";

            Hp = BaseHp;
            Atk = AttackDamage;
            Str = BaseStr;
            Def = BaseDef;
            Shield = 0;
            SkipTurn = 0;
        }

        // ==================== 意图 ====================

        /// <summary>
        /// 本次出手是不是吞噬回合。
        ///
        /// <see cref="_turnsTaken"/> 是已经出手的次数，+1 就是这一次的序号；能被 2 整除就是吞噬。
        /// 于是第 1 次出手普攻、第 2 次吞噬、第 3 次普攻、第 4 次吞噬…… 与设计书的
        /// 「第 2、4、6…回合吞噬」一致（这里的「回合」只数它自己出手）。
        /// </summary>
        public bool IsDevourTurn => (_turnsTaken + 1) % DevourEvery == 0;

        /// <summary>
        /// 意图：吞噬回合要提前告诉玩家，否则玩家没法决定「要不要先把沙用掉」。
        ///
        /// 吞噬回合统一显示「吞噬」，不再去区分有沙 / 无沙：
        ///   沙在**玩家自己回合里**会被用掉，回合开始显示的沙量到执行时早就不准了。
        ///   显示死的数值反而骗人，所以只报技能名 —— 有沙没沙玩家自己清楚。
        /// </summary>
        public override string IntentText()
        {
            return IsDevourTurn ? "吞噬" : "打 " + AttackDamage.ToString("F0");
        }

        /// <summary>
        /// 意图完全由「第几次出手」决定，不用摇骰子。
        /// 但必须走一遍 <see cref="Monster.PlanIntent"/>：不调的话基类会留着 -1，
        /// 让 TakePlannedOption 在出手时自己摇一个 —— 这里没有可摇的东西。
        /// </summary>
        public override void PlanIntent(Player p) => SetPlannedOption(0);

        // ==================== 出手 ====================

        public override void Attack(Player p)
        {
            Console.WriteLine(Name + "扬起漫天沙尘，猛地压了下来！");
            p.TakeDamage(AttackDamage);
        }

        public override void OnMonsterTurn(Player p)
        {
            // 回合头（「XXX的回合」）与跳过检查都在 Tools.RunMonsterTurns 里统一处理，
            // 所以走到这里 = 这一回合它真的要出手了，计数可以安心 +1。
            _turnsTaken++;
            TakePlannedOption(1);   // 用掉本回合选项，与其它怪保持同一状态机

            if (_turnsTaken % DevourEvery == 0)
            {
                Devour(p);
            }
            else
            {
                Attack(p);
            }
        }

        /// <summary>
        /// 吞噬。
        ///
        /// 两条分支的分界只有一个：<b>此刻</b>玩家背包里还有没有沙（<see cref="Player.CountItemById"/>）。
        /// 不看回合开始的快照 —— 玩家在自己回合里把沙烧掉就能躲过这一吸（偏向玩家）。
        /// </summary>
        private void Devour(Player p)
        {
            int sand = p.CountItemById(ItemIds.Sand);

            if (sand <= 0)
            {
                Console.WriteLine(Name + "张开口吞向你的背包 —— 里面一粒沙也没有！");
                DevourWithoutSand(p);
                return;
            }

            Console.WriteLine(Name + "张开巨口，把你背包里的沙全部吸了进去！（沙 x" + sand + "）");
            TakeAllSand(p);
            HealFromSand(sand);
            GainShieldFromSand(sand);
        }

        /// <summary>
        /// **无沙**分支：造成伤害 + 跳过玩家下 1 回合。
        ///
        /// 伤害走 <see cref="Player.TakeDamage"/> 而不是直接扣血：
        ///   与普攻同一口径 —— 玩家的 DEF、护盾照常挡，不会因为「这是技能伤害」就白挨。
        ///
        /// 跳过走 <see cref="Player.AddSkipTurn"/> 计数（设计书「实现边界」第 1 条）。
        /// 消费在 <c>Player.TryConsumeSkipTurn</c>（玩家的 OnTurnStart 里，每回合一次），
        /// 战后清零在 Tools.RunBattle 的 finally 里 —— 这两处都已存在，这里只负责加计数。
        /// </summary>
        private void DevourWithoutSand(Player p)
        {
            Console.WriteLine(Name + "把空空的沙袋砸向你！");
            p.TakeDamage(EmptyDevourDamage);

            // 玩家可能被这一下打死。死人不该再被挂「跳过」——
            // 否则战后清理前还会多播报一次「你被跳过」。
            if (!p.IsAlive) return;

            p.AddSkipTurn(1);
            Console.WriteLine("你被埋进沙里，下一回合无法行动！");
        }

        /// <summary>
        /// 把玩家背包里的沙**全部**取走。
        ///
        /// 倒序遍历、整条目移除：沙是可堆叠道具（MaxStack 99），背包里理论上可能有不止一条
        /// （99 堆满后又开了一格），设计书说的是「全部沙」，所以不能只处理第一条。
        /// 倒序是移除元素时的常规写法 —— 正序删会让后面的下标全部前移、漏掉元素。
        /// </summary>
        private void TakeAllSand(Player p)
        {
            for (int i = p.Inventory.Count - 1; i >= 0; i--)
            {
                if (p.Inventory[i].Id == ItemIds.Sand)
                {
                    p.Inventory.RemoveAt(i);
                }
            }

            // 背包被删过东西，状态栏的格数显示要刷新一次。
            p.RefreshAfterItemUse();
        }

        /// <summary>
        /// 回血：每吸 1 沙回 1 血，**整场战斗累计**不超过 <see cref="MaxHealPerBattle"/>。
        /// 血已经满了也不浪费额度 —— 上限只数真正回到身上的血量。
        /// </summary>
        private void HealFromSand(int sand)
        {
            int want = sand * HealPerSand;
            int budget = MaxHealPerBattle - _healedByDevour;
            int heal = Math.Min(want, budget);
            if (heal <= 0)
            {
                Console.WriteLine(Name + "的伤势已经无法靠吞沙复原了。");
                return;
            }

            Hp += heal;
            _healedByDevour += heal;
            Console.WriteLine(Name + "从沙里汲取养分，恢复了 " + heal + " 点生命！（本场已回 "
                              + _healedByDevour + "/" + MaxHealPerBattle + "）");
        }

        /// <summary>护盾：吸走数量 ÷ 2，向下取整。</summary>
        private void GainShieldFromSand(int sand)
        {
            int shield = sand / SandPerShield;
            if (shield <= 0)
            {
                Console.WriteLine("沙太少，没有结成护甲。");
                return;
            }

            AddShield(shield);
            Console.WriteLine(Name + "把沙压成护甲（护盾 +" + shield + "）！");
        }

        // ==================== 护盾 ====================

        /// <summary>
        /// 每回合出手前清空护盾（设计书「实现边界」第 2 条：护盾每回合重置）。
        ///
        /// 于是它的盾只在**它刚吞完沙、还没轮到玩家砍**的那一小段存在 ——
        /// 玩家想打实伤，就要在两次吞噬之间把盾打掉、或者干脆让它吞不到沙（空手走「无沙」分支，也就不给盾）。
        /// 这是设计书第 1 条「玩家必须保证每 2 回合有一次出手机会」的另一面。
        /// </summary>
        public override void ResetShieldForTurn() => Shield = 0;

        // ==================== 受伤 ====================

        public override void TakeDamage(double dmg) => TakeDamageWithDefaultLog(dmg);

        protected override void LogShieldAbsorbed()
        {
            Console.WriteLine(Name + "的沙甲挡下了攻击！（剩余护盾:" + Shield.ToString("F1") + "）");
        }

        protected override void LogDamageTaken(double hpLoss)
        {
            Console.WriteLine(Name + "受到{0:F1}点伤害!", hpLoss);
        }

        protected override int RollIndex(int count) => count <= 0 ? 0 : Random.Shared.Next(count);
    }
}

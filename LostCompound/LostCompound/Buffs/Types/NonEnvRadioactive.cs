using LostCompound.Items;
using LostCompound.Monsters;
using LostCompound.Tools.Players;

namespace LostCompound.Buffs
{
    /// <summary>
    /// **非环境辐射**（设计文档 §7.4 buff 2）：身上带着辐射源时，**按回合**定期掉血。
    ///
    /// 与 <see cref="EnvRadioactive"/> 成对命名 —— 那两个 buff 一个按**秒**（环境给的）、
    /// 一个按**回合**（道具给的），改名字必须一起改。
    ///
    /// ## 一句话规则
    /// 每个辐射源（放射性矿物 11 / 放射金属罐 18）各带一条账，**各自计时**，
    /// 到点扣它自己那一笔；放射金属罐戴在饰品栏时**屏蔽全部**非环境辐射。
    ///
    /// ## Hyberty 2026-11-22 裁定：C 方案「各来源各记一本账」+ 同类按数量叠伤害
    ///
    /// ⚠️ 这一条**偏离了设计文档 §7.4**，是 Hyberty 在实现阶段明确改的口径：
    ///   文档原文要求「同类只留一个 buff 对象，新来源把**每次扣血的量**加进去」，
    ///   而 11 与 18 的**间隔并不相同**（5 回合 / 10 回合），
    ///   「合并成一个 Stacks」在数值上没有唯一解 —— 文档自己也把它标成了
    ///   「⚠️ 待裁定（实现前必须先问）」。
    ///
    ///   问过之后 Hyberty 选了 **C：各来源各记一本账**（11 与 18 互不合并）。
    ///   于是本类内部维护一张 <see cref="Sources"/> 表，
    ///   每个来源自己有「已经倒了几个回合」，到自己的间隔就扣自己那次的血。
    ///
    ///   代价（**写清楚免得后来者以为是 bug**）：
    ///     · 11 和 18 同时在身上时，一个回合里**可能扣两次**（两条账同时到点）；
    ///     · 这就是「各记一本账」的字面含义 —— 两个不同的辐射源各算各的。
    ///   好处：
    ///     · 不用回答「合并后取谁的间隔」那个没有唯一解的问题；
    ///     · 加上放射金属罐的屏蔽，三条规则（各自计时 / 屏蔽 / 移除法）互不干扰。
    ///
    ///   **同类道具按数量叠伤害**（Hyberty 同一轮追加的裁定）：
    ///     2 个放射金属罐 → 每 10 回合扣 2×2 = 4 点；
    ///     3 块放射性矿物 → 每 5 回合扣 1×3 = 3 点。
    ///     叠的是「**每次扣多少**」，间隔不变（不会扣得更勤）。
    ///     这正是文档 §7.4 原本的「叠伤害」语义 —— 只是作用域从「所有来源」缩小到了「同一来源」。
    ///
    /// ## 为什么 `Stacks` 在这里语义不同
    /// 文档 §7.4 反复强调：本 buff 里 `Stacks` 是**回合间隔**、不是强度、也不是层数。
    /// 但各来源的间隔不同，一个 `Stacks` 已经装不下 ——
    /// 所以本类把 `Stacks` **只当显示用**（取自「最短的那个间隔」），
    /// 真正的间隔存在 <see cref="Sources"/> 的每一条里。
    ///
    /// ## 为什么不用「每回合检查背包」
    /// 那样每回合都要遍历一次背包 + 判断饰品栏，而本 buff 的生命周期完全由道具决定。
    /// 现在改成：道具进/出背包或饰品栏时调 <see cref="Sync(Player)"/> 重建那张账，
    /// 回合推进（<see cref="OnTurnTick(Player)"/>）只做「表里每条账 +1、到点扣血」。
    /// 重建很便宜（两份道具表各扫一遍），但每回合扫就不必要了。
    /// </summary>
    public class NonEnvRadioactive : Buff
    {
        /// <summary>
        /// 一个辐射源。**每个来源一条**，各自计时 —— 这就是 C 方案「各记一本账」。
        /// 同一来源的多个道具不再各占一条，而是合并进同一条并把 <see cref="Damage"/> 累加。
        /// </summary>
        /// 为什么用 record 而不是可变对象：
        ///   <see cref="TurnsLeft"/> 每个回合都要改，只读类会让「重建表」变成每次 new 一堆；
        ///   这里用普通 class + 可变字段更省，而且表本身很小（最多两种来源）。
        /// </summary>
        private sealed class Source
        {
            /// <summary>来源道具的 ID（11 / 18），只用于「同一来源不重复记账」和播报。</summary>
            public int ItemId;

            /// <summary>这个来源的间隔（回合）。11 = 5、18 = 10，由道具自己声明。</summary>
            public int Interval;

            /// <summary>这个来源每次扣多少血（普通难度）。11 = 1、18 = 2。</summary>
            public int Damage;

            /// <summary>距离下一次发作还剩几个回合（从 <see cref="Interval"/> 起倒数）。</summary>
            public int TurnsLeft;
        }

        /// <summary>身上现有的辐射源，每个来源一条。空表 = 没有辐射（buff 应当被移除）。</summary>
        private readonly List<Source> _sources = [];

        public NonEnvRadioactive(int stacks, int turns)
            : base(stacks, turns, survivesBattleEnd: true)
        {
            // survivesBattleEnd = true：文档 §7.4「只要道具在身上就永久」——
            // 它是道具的产物，不该因为打完一场战斗就被 ClearBattleBuffs 清掉。
            // 真正的移除条件是「道具离身」，见 Sync。
        }

        public override string Name => "非环境辐射";

        public override int Id => BuffIds.NonEnvRadioactive;

        public override bool IsDebuff => true;

        /// <summary>永远不自然到期：存活与否只看身上还有没有辐射源（见 <see cref="IsExpired"/>）。</summary>
        public override bool IsExpired => _sources.Count == 0;

        /// <summary>
        /// 状态栏那一行。故意把每个来源分开写 ——
        /// 合并成一个「每 5 回合扣 1」会骗人：两条账同时到点时实际扣的是 3 点。
        /// </summary>
        public override string Describe()
        {
            if (_sources.Count == 0) return "";

            List<string> parts = [];
            foreach (Source src in _sources)
            {
                parts.Add(ItemFactory.NameOf(src.ItemId) + " 每 " + src.Interval + " 回合 -" + src.Damage);
            }

            return string.Join("；", parts);
        }

        /// <summary>
        /// 按玩家当前身上的辐射源**重建**那张账。
        ///
        /// 谁调它：
        ///   · 道具被加进背包 / 丢出背包 / 装进饰品栏 / 卸下时（见 Player.SyncRadiationBuff）；
        ///   · 开战时兜底（读档之后第一次结算之前也能正确建表）。
        ///
        /// 为什么是「重建」而不是「增量加/减一条」：
        ///   增量口径要处理「同种道具堆叠数变了」「饰品栏里放了两罐」这些边角，
        ///   而重建只需要问一句「你现在身上有哪些辐射源」—— 一条规则，没有例外。
        ///   重建的副作用是**计时会复位**：把矿物从背包挪到饰品栏再挪回来，会重新数 5 回合。
        ///   这可以接受（搬动道具本身就是玩家的动作），而且比「记账错位」好得多。
        /// </summary>
        public void Sync(Player p)
        {
            if (p == null) return;

            // 放射金属罐戴在饰品栏时，**全部**非环境辐射都不发作（含它自己那一份）。
            // 文档 §7.4：「它屏蔽的只是 NonEnvRadioactive，不屏蔽 EnvRadioactive」——
            // 环境辐射根本不走本类（那是 RadioactiveArea 的秒表），所以天然不被屏蔽。
            if (HasShieldingCan(p))
            {
                _sources.Clear();
                AddStacks(-Stacks);          // 把显示用的 Stacks 也清掉
                return;
            }

            Dictionary<int, Source> found = [];

            // 1) 背包里的道具
            foreach (Item item in p.Inventory)
            {
                AddSource(found, item);
            }

            // 2) 饰品栏里的道具（放射金属罐可以戴在这里）
            foreach (Item item in p.Accessories)
            {
                AddSource(found, item);
            }

            _sources.Clear();
            _sources.AddRange(found.Values);

            // Stacks 只留给显示/调试看：取最短间隔，语义见类注释。
            if (_sources.Count > 0)
            {
                int shortest = int.MaxValue;
                foreach (Source src in _sources) shortest = Math.Min(shortest, src.Interval);
                AddStacks(shortest - Stacks);
            }
            else
            {
                AddStacks(-Stacks);
            }
        }

        /// <summary>
        /// 饰品栏里是否戴着放射金属罐。
        ///
        /// 只查 `p.Accessories`（**不是**背包）：文档说「饰品栏时屏蔽」——
        /// 装在背包里的罐子只会**多**一个辐射源，不会保护你。
        /// </summary>
        private static bool HasShieldingCan(Player p)
        {
            foreach (Item item in p.Accessories)
            {
                if (item?.Id == ItemIds.RadioactiveMetalCan) return true;
            }

            return false;
        }

        /// <summary>把一个道具登记成辐射源（不认识的道具直接忽略）。</summary>
        private static void AddSource(Dictionary<int, Source> found, Item item)
        {
            if (item == null) return;

            // Hyberty 2026-11-22：**同类道具按数量叠伤害**。
            // 例：2 个放射金属罐 → 每 10 回合扣 2×2 = 4 点（间隔不变，只有伤害翻倍）。
            // 所以同一 ItemId 只留**一条账**，但 Damage 按 Count 累加。
            //
            // 间隔仍取该来源自己的固定值（矿 5 / 罐 10）——
            // 「叠数量」叠的是**每次扣多少**，不是「扣得更勤」。
            if (found.ContainsKey(item.Id)) return;

            switch (item.Id)
            {
                case ItemIds.RadioactiveOre:
                    found[item.Id] = new Source
                    {
                        ItemId = item.Id,
                        Interval = Items.Materials.RadioactiveOre.DamageInterval,
                        Damage = Items.Materials.RadioactiveOre.DamagePerTick * item.Count,
                        TurnsLeft = Items.Materials.RadioactiveOre.DamageInterval,
                    };
                    break;

                case ItemIds.RadioactiveMetalCan:
                    found[item.Id] = new Source
                    {
                        ItemId = item.Id,
                        Interval = Items.Equipment.RadioactiveMetalCan.DamageInterval,
                        Damage = Items.Equipment.RadioactiveMetalCan.DamagePerTick * item.Count,
                        TurnsLeft = Items.Equipment.RadioactiveMetalCan.DamageInterval,
                    };
                    break;
            }
        }

        /// <summary>
        /// 每大回合结算一次：每条账 +1，到点的按它自己的伤害扣血。
        ///
        /// 扣血走 <see cref="Player.TakeDamage"/>（与项目里其它「定期扣血」一致，
        /// 会正常过 DEF / 护盾）—— 文档没要求辐射无视防御，那是**环境辐射**
        /// （<see cref="EnvRadioactive"/>）才有的特权。
        /// </summary>
        public override void OnTurnTick(Player owner)
        {
            if (owner == null || _sources.Count == 0) return;

            foreach (Source src in _sources)
            {
                src.TurnsLeft -= 1;
                if (src.TurnsLeft > 0) continue;

                src.TurnsLeft = src.Interval;

                Console.WriteLine(
                    ItemFactory.NameOf(src.ItemId) + "正在侵蚀你！（每 " + src.Interval + " 回合发作一次）");

                // 逐点结算而不是一次扣 D 点：Calculator.ApplyDefense 最低取 1，
                // 合并成一次会被 DEF 白吃一部分 —— 与放射性矿物原先的写法保持一致。
                for (int i = 0; i < src.Damage; i++)
                {
                    owner.TakeDamage(1);
                }

                if (!owner.IsAlive) return;
            }
        }

        /// <summary>怪物身上不会有道具辐射。</summary>
        public override void OnTurnTick(Monster owner) { }
    }
}

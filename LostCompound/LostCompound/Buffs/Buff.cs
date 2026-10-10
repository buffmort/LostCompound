using LostCompound.Monsters;
using LostCompound.Tools;

namespace LostCompound.Buffs
{
    /// <summary>
    /// 状态效果基类。每个效果是一个对象，挂在 Player 或 Monster 身上，
    /// 由战斗回合管线统一处理（见 Tools.RunBattle）。
    ///
    /// 为什么用对象而不是散落的字段？
    ///   以前持续伤害是 Player/Monster 上的两个字段（DotDamage/DotDuration），只能存在一种，
    ///   想加第二种持续伤害就要再加两个字段。对象化之后，挂几个就是几个，
    ///   而且每个 buff 的结算规则互不干扰。
    ///   DOT 字段与结算已按你的要求整体删除 —— 周期性伤害现在只有 buff 这一条出口。
    ///
    /// 注意：本类只负责「结算自己」，不负责「什么时候结算」——
    /// 时机统一由 Tools.ApplyBuffs 决定（大回合开场，收尸之前）。
    /// </summary>
    public abstract class Buff
    {
        /// <summary>
        /// 初始效果量与初始剩余回合。子类构造函数传进来，避免每个子类自己写一遍字段初始化。
        /// 这里不能直接给 Stacks 赋值：MaxStacks 是虚属性，赋值必须走 AddStacks 才能保证不超过上限。
        /// </summary>
        protected Buff(int stacks, int turns)
        {
            AddStacks(stacks);
            RemainingTurns = Math.Max(turns, 0);
        }

        /// <summary>显示名（如「流血」）。</summary>
        public abstract string Name { get; }

        /// <summary>
        /// 效果量。流血里它的语义是 **每回合扣血数**（不是「层数」）。
        /// 为什么改成伤害而不是层数：你的口径是「上限 10 管的是每回合伤害」——
        /// 现有 7 点再加 5 点应该变成 10 点，而不是 12 层。
        /// </summary>
        public int Stacks { get; protected set; }

        /// <summary>效果量上限。默认不限。</summary>
        public virtual int MaxStacks => int.MaxValue;

        /// <summary>
        /// 剩余回合：每结算一次 -1（由各 buff 在自己的 OnTurnTick 里走 ConsumeTurn，
        /// 见 BleedBuff），归零则该效果失效。
        /// 为什么单独存一个字段而不是把剩余回合并进 Stacks：
        ///   上限 10 只约束「每回合伤害」，而持续时间可以是任意长（同类叠加时相加），
        ///   两者混在一个数里就无法既夹取伤害又保留时长。
        /// </summary>
        public int RemainingTurns { get; private set; }

        /// <summary>
        /// 是否已失效（该移除了）：剩余回合归零，或效果量为 0。
        /// 伤害为 0 也算失效 —— 留着它每回合什么都不做，只会在状态栏里显示一条假状态。
        /// </summary>
        public bool IsExpired => RemainingTurns <= 0 || Stacks <= 0;

        /// <summary>叠加效果量（不超过 MaxStacks）。</summary>
        public void AddStacks(int n)
        {
            Stacks = Math.Min(Stacks + n, MaxStacks);
        }

        /// <summary>
        /// 累加剩余回合。同类叠加时用「两者之和」——
        /// 与旧 DOT 的 AddDotDuration 就是 += 累加的语义保持一致。
        /// </summary>
        public void AddTurns(int n)
        {
            RemainingTurns = Math.Max(RemainingTurns + n, 0);
        }

        /// <summary>结算一次剩余回合（每回合调用一次）。归零后 IsExpired 为真，由回合管线移除。</summary>
        public void ConsumeTurn()
        {
            RemainingTurns = Math.Max(RemainingTurns - 1, 0);
        }

        /// <summary>
        /// 同类叠加：效果量取 min(现有 + 新增, 上限)，剩余回合取两者之和。
        /// 为什么放在基类：Player.AddBuff 和 Monster.AddBuff 都要做这件事
        /// （CONVENTIONS 2.5：同一段逻辑出现两遍就提到基类），否则以后加第三种持有者又要再抄一遍。
        /// </summary>
        public void MergeFrom(Buff other)
        {
            AddStacks(other.Stacks);
            AddTurns(other.RemainingTurns);
        }

        /// <summary>
        /// 减少效果量（如设计文档里的「万能药减 4 层流血」）。
        /// 减到 0 即 IsExpired，由回合管线负责移除。万能药本身尚未实现，这里先把口子留好。
        /// </summary>
        public void RemoveStacks(int n)
        {
            Stacks = Math.Max(Stacks - n, 0);
        }

        /// <summary>
        /// 本 buff 对 STR 的修正量（正数加强、负数削弱），默认 0。
        ///
        /// 为什么是「暴露修正量」而不是在 OnTurnTick 里直接改 owner.Str：
        ///   直接改要么互相覆盖（环境和 buff 各写一次 Str），要么累加、
        ///   然后得在 buff 消失时再加回来 —— 而 buff 有三条移除路径
        ///   （到期 / ClearBuffs / 将来的驱散），漏一条就永久偏移。
        ///   做成修正量之后，Player.RecomputeStr 每次从基础值重算，
        ///   挂上自动生效、消失自动恢复，不需要任何「还原」代码。
        /// </summary>
        public virtual double StrBonus => 0.0;

        /// <summary>
        /// 每回合结算一次。由战斗回合管线（Tools.ApplyBuffs）调用。
        /// 只扣血，不负责收尸 —— 与原 DOT 结算的纪律一致：
        /// 死亡统一交给 Tools.RemoveDeadMonsters，否则 buff 击杀 + 技能补刀会双发掉落。
        /// </summary>
        public abstract void OnTurnTick(Player owner);

        /// <summary>怪物身上的版本（流血可以挂在怪物身上）。</summary>
        public abstract void OnTurnTick(Monster owner);
    }
}

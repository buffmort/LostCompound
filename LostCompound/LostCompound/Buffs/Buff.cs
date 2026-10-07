using LostCompound.Monsters;
using LostCompound.Tools;

namespace LostCompound.Buffs
{
    /// <summary>
    /// 状态效果基类。每个效果是一个对象，挂在 Player 或 Monster 身上，
    /// 由战斗回合管线统一处理（见 Tools.RunBattle）。
    ///
    /// 为什么用对象而不是散落的字段？
    ///   现在 DOT 是 Player 上的两个字段（DotDamage/DotDuration），只能存在一种，
    ///   想加第二种持续伤害就要再加两个字段。对象化之后，挂几个就是几个，
    ///   而且每个 buff 的结算规则互不干扰。
    ///
    /// 注意：本类只负责「结算自己」，不负责「什么时候结算」——
    /// 时机统一由 Tools.ApplyBuffs 决定，和 DOT 同一时机（大回合开场，收尸之前）。
    /// </summary>
    public abstract class Buff
    {
        /// <summary>
        /// 初始层数。子类构造函数传进来，避免每个子类自己写一遍字段初始化。
        /// 这里不能直接给 Stacks 赋值：MaxStacks 是虚属性，赋值必须走 AddStacks 才能保证不超过上限。
        /// </summary>
        protected Buff(int stacks)
        {
            AddStacks(stacks);
        }

        /// <summary>显示名（如「流血」）。</summary>
        public abstract string Name { get; }

        /// <summary>剩余层数/时长，归零后移除。</summary>
        public int Stacks { get; protected set; }

        /// <summary>层数上限。默认不限。</summary>
        public virtual int MaxStacks => int.MaxValue;

        /// <summary>是否已失效（该移除了）。</summary>
        public bool IsExpired => Stacks <= 0;

        /// <summary>叠加层数（不超过 MaxStacks）。</summary>
        public void AddStacks(int n)
        {
            Stacks = Math.Min(Stacks + n, MaxStacks);
        }

        /// <summary>
        /// 减少层数（如设计文档里的「万能药减 4 层流血」）。
        /// 减到 0 即 IsExpired，由回合管线负责移除。万能药本身尚未实现，这里先把口子留好。
        /// </summary>
        public void RemoveStacks(int n)
        {
            Stacks = Math.Max(Stacks - n, 0);
        }

        /// <summary>
        /// 每回合结算一次。由战斗回合管线（Tools.ApplyBuffs）调用。
        /// 只扣血，不负责收尸 —— 与 ApplyDotEffects 的纪律一致：
        /// 死亡统一交给 Tools.RemoveDeadMonsters，否则 DOT 击杀 + buff 击杀会双发掉落。
        /// </summary>
        public abstract void OnTurnTick(Player owner);

        /// <summary>怪物身上的版本（流血可以挂在怪物身上）。</summary>
        public abstract void OnTurnTick(Monster owner);
    }
}

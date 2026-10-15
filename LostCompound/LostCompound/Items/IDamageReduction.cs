namespace LostCompound.Items
{
    /// <summary>
    /// 持有者受到的伤害减免。与 <see cref="IShieldSource"/> / <see cref="IOnHit"/> 同一思路：
    /// 只有真正有减伤效果的道具才实现它，剑/盔甲不必写一个空的「我不减伤」。
    ///
    /// 为什么要有它：玩家的唯一受伤入口 <see cref="LostCompound.Tools.Player.TakeDamage"/>
    /// 里原先只有 <c>DEF 减免 → 吃盾 → 扣血</c> 三步，**没有任何插槽**能让装备改这一跳的数值
    /// （怪物侧有 <see cref="LostCompound.Monsters.Monster.TakeDamage"/> 这个虚方法可以覆写预处理，
    /// 玩家侧没有对应的一层）。于是「斗篷 20% 免伤」这类效果无处落地。
    ///
    /// ⚠️ 与 <see cref="IEquippable"/> 的区别（这两条通道不能混）：
    ///   IEquippable 改的是**派生属性**（DEF 变高 → 由公式顺带少挨打），
    ///   本接口改的是**这一跳伤害本身**（按比例打折），与 DEF 是两条独立的减免，
    ///   不能写成「DEF +20%」那种派生值改写 —— 那会被 RecomputeStats 静默冲掉（CONVENTIONS 2.3）。
    /// </summary>
    public interface IDamageReduction
    {
        /// <summary>
        /// 减伤比例（0.2 = 减 20%）。取值 0..1。
        ///
        /// 读取方（<see cref="LostCompound.Tools.Player.TakeDamage"/>）会把超出 0..1 的值夹回来，
        /// 所以实现方不必自己夹；但**多个来源如何叠加由读取方定义**（本实现是连乘，
        /// 见 <c>Player.DamageReductionMultiplier</c>），道具不需要也不应该关心别人减多少。
        /// </summary>
        double DamageReductionRatio { get; }
    }
}

using LostCompound.Tools;
using LostCompound.Tools.Players;

namespace LostCompound.Items.Capabilities
{
    /// <summary>
    /// 「玩家这一跳**真的受伤了**（Hp 被扣）」的事件通知 —— 装备侧的口子。
    ///
    /// 为什么要有它：既有的两条装备通道都拿不到「又挨了一下」这个**事件**。
    ///   · <see cref="IEquippable"/> 只在重算派生值时被调用（每回合开头 / 换装 / buff 变化…），
    ///     调用次数与「挨打次数」毫无关系；
    ///   · <see cref="IDamageReduction"/> 只回答「这一跳打几折」，是纯函数式的数值查询，
    ///     它可以被读很多次却一次伤害都没有发生。
    /// 而斗篷(27) 的「受伤 50 次后破损」要的正是**次数**，只能由受伤入口显式通知一次。
    ///
    /// ⚠️ 触发口径（由调用方 <see cref="Player.TakeDamage"/> 保证）：
    ///   只在伤害真的打进身体、`Hp` 被扣掉的那一条分支里各通知一次；
    ///   **被盾完全挡下（血一点没掉）的那一跳不通知** —— 斗篷这一下并没有替玩家挨到，
    ///   耐久不该因为盾厚而被消耗。`realDmg` 为 0（如 100% 减伤）同样走盾那条分支，也不通知。
    ///
    /// ⚠️ 与 <see cref="IDotAbsorber"/> 的区别（不要混成一条通道）：
    ///   IDotAbsorber 作用在**伤害还没过 DEF / 盾之前**的原始 DOT 值上（它要吃掉伤害）；
    ///   本接口作用在**伤害已经结算完毕、血已经掉了之后**（它只是事后记账）。
    ///   前者改数值、后者收事件 —— 两者语义正交，需要的是「都能拿到受伤信息」才考虑合并。
    /// </summary>
    public interface IOnDamaged
    {
        /// <summary>
        /// 玩家挨了一下（Hp 已被扣）。实现方在这里记账 / 播报 / 触发自身效果。
        ///
        /// 返回值无意义：本通道不改变这一跳的伤害（要改数值请用
        /// <see cref="IDamageReduction"/>）。实现方若把自己用坏了，只需让
        /// <see cref="IChangeable.ShouldRemove"/> 返回 true —— 调用方会在遍历结束后
        /// 统一把破损的道具从背包 / 饰品栏里收走（边遍历边改列表会漏项）。
        /// </summary>
        /// <param name="owner">挨打的玩家（与 <see cref="IOnHit.OnHit"/> 同一风格，把上下文显式传进来）。</param>
        void OnDamaged(Player owner);
    }
}

using LostCompound.Tools;

namespace LostCompound.Items
{
    /// <summary>
    /// 背包被动道具：放在背包里就生效。
    ///
    /// 设计文档里很多道具是「背包 +7ATK」「背包 +10DEF」这种描述 ——
    /// 它们不需要玩家主动使用，只要待在背包里就该起作用。
    ///
    /// 生效方式：Player 每次战斗开始时对背包里所有 Equippable 调用一次
    /// <see cref="ApplyPassive"/>，把加成累加到玩家身上。
    /// </summary>
    public interface IEquippable
    {
        /// <summary>把本道具的被动加成应用到玩家身上（每次战斗开始时调用一次）。</summary>
        void ApplyPassive(Player p);

        /// <summary>给玩家看的一行效果说明，用于列表展示。</summary>
        string DescribePassive();
    }
}

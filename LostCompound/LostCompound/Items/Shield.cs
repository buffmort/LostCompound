using LostCompound.Tools;

namespace LostCompound.Items
{
    /// <summary>
    /// 盾（配方 1，ID 28）：防具。格挡次数 +1。
    /// 合成材料：3 金属。
    /// 「格挡次数」对应 Player.Block（每层可免疫一次伤害的计数）。
    /// </summary>
    public sealed class Shield(int count) : Item(ItemIds.Shield, "盾", 1, false, count), IEquippable
    {
        public override ItemType? Type => ItemType.Shield;

        /// <summary>背包时提供的格挡次数加成。</summary>
        public const int BlockBonus = 1;

        public void ApplyPassive(Player p) => p.Block += BlockBonus;

        public string DescribePassive() => "格挡次数 +" + BlockBonus;
    }
}

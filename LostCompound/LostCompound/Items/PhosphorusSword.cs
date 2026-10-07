using LostCompound.Tools;

namespace LostCompound.Items
{
    /// <summary>
    /// 磷化剑（配方 1，ID 22）：武器。背包时 +15 ATK，攻击挂 1 流血。
    /// 合成材料：铁剑 + 2 白磷 + 2 红磷。
    /// 「攻击挂 1 流血」需要流血系统，尚未实现，先只做 ATK 加成。
    /// </summary>
    public sealed class PhosphorusSword(int count) : Item(ItemIds.PhosphorusSword, "磷化剑", 1, false, count), Equippable
    {
        public override ItemType? Type => ItemType.PhosphorusSword;

        /// <summary>背包时提供的攻击力加成。</summary>
        public const double AtkBonus = 15;

        public void ApplyPassive(Player p) => p.Atk += AtkBonus;

        public string DescribePassive() => "背包 +" + AtkBonus + " ATK，攻击挂 1 流血（流血未实现）";
    }
}

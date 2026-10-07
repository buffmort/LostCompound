using LostCompound.Tools;

namespace LostCompound.Items
{
    /// <summary>
    /// 铁剑（配方 1，ID 21）：武器。背包时 +7 ATK。
    /// 合成材料：2 木头 + 2 金属。
    /// </summary>
    public sealed class IronSword(int count) : Item(ItemIds.IronSword, "铁剑", 1, false, count), IEquippable
    {
        public override ItemType? Type => ItemType.IronSword;

        /// <summary>背包时提供的攻击力加成。</summary>
        public const double AtkBonus = 7;

        public void ApplyPassive(Player p) => p.Atk += AtkBonus;

        public string DescribePassive() => "背包 +" + AtkBonus + " ATK";
    }
}

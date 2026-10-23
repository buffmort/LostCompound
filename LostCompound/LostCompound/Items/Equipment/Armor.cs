using LostCompound.Tools;
using LostCompound.Items;
using LostCompound.Items.Capabilities;

using LostCompound.Tools.Players;

namespace LostCompound.Items.Equipment
{
    /// <summary>
    /// 盔甲（配方 1，ID 24）：防具。背包时 +10 DEF。
    /// 合成材料：3 金属 + 1 白磷。
    /// </summary>
    public sealed class Armor(int count) : Item(ItemIds.Armor, "盔甲", 1, false, count), IEquippable
    {
        public override ItemType? Type => ItemType.Armor;

        /// <summary>背包时提供的防御加成。</summary>
        public const double DefBonus = 10;

        public void ApplyPassive(Player p) => p.Def += DefBonus;

        public string DescribePassive() => "背包 +" + DefBonus + " DEF";
    }
}

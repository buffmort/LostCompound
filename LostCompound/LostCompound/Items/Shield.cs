using LostCompound.Tools;

namespace LostCompound.Items
{
    /// <summary>
    /// 盾（配方 1，ID 28）：防具。**护盾值 +5**。
    /// 合成材料：3 金属。
    ///
    /// 本次改动：原效果是「格挡次数 +1」—— 次数制下 1 层格挡 = 完全免疫一次任意大的伤害，
    /// 用户裁定「太超模」，整套机制换成护盾值制后，这里相应改为加护盾值。
    ///
    /// 为什么定 5 点：
    ///   1. 与玩家的「防御」行动同价（2.防御 = 护盾 +5），两者都是「花一回合/一格装备换盾」，
    ///      数值对齐后玩家不用记两套换算；
    ///   2. 玩家面对的单次最高伤害是龙的 10 点、双子技能的 5 点 —— 5 点盾 ≈ 挡掉半刀，
    ///      明显弱于旧机制（一回合/一格装备白吃一整刀），符合「削弱超模机制」的意图；
    ///   3. 合成成本是 3 金属，换算下来 1 金属 = 1.67 点盾，不至于让盾变成废品。
    /// 注意它是 IEquippable（被动），加成走 ApplyEquipmentPassives 的「重算」路径：
    /// 一旦盾离开背包/饰品栏，下一场战斗开始时护盾会回到 0。
    /// </summary>
    public sealed class Shield(int count) : Item(ItemIds.Shield, "盾", 1, false, count), IEquippable
    {
        public override ItemType? Type => ItemType.Shield;

        /// <summary>背包/饰品栏时提供的护盾值加成。</summary>
        public const double ShieldBonus = 5;

        public void ApplyPassive(Player p) => p.AddShield(ShieldBonus);

        public string DescribePassive() => "护盾 +" + ShieldBonus;
    }
}

using LostCompound.Tools;
using LostCompound.Items;
using LostCompound.Items.Capabilities;

using LostCompound.Tools.Players;

namespace LostCompound.Items.Equipment
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
    /// 注意它仍然是被动物件（IEquippable：放背包或饰品栏就生效），但**护盾加成**走
    /// ApplyEquipmentPassives → ResetShieldForTurn 这条「重置后再发一次」的路径：
    /// 一旦盾离开背包/饰品栏，下一次重置（开战 / 换装 / 下一回合开头）护盾就回到 0。
    ///
    /// 实现两个接口，分工必须看清（这是「护盾与 Atk/Def 解耦」的一部分）：
    ///   - IShieldSource → ApplyShield：真正加盾这一步。只在「重置护盾」的时机被调用
    ///     （每回合开头 / 换装 / 开战），所以盾能被消耗、也能每回合重新长出来；
    ///   - IEquippable   → ApplyPassive：**空实现**。盾是资源不是派生值，一旦把它写进
    ///     ApplyPassive（改造前就是 `p.AddShield(5)` 写在那里），每次重算派生值都会再补一次盾，
    ///     而重算在一场战斗里会被调用很多次（buff 变化、环境变化、回合开头……）= 无限补盾。
    ///     实现 IEquippable 只是为了让盾能放进饰品栏（饰品栏只收被动物件，见 Player.EquipAccessory）。
    /// </summary>
    public sealed class Shield(int count) : Item(ItemIds.Shield, "盾", 1, false, count), IEquippable, IShieldSource
    {
        public override ItemType? Type => ItemType.Shield;

        /// <summary>背包/饰品栏时提供的护盾值加成。</summary>
        public const double ShieldBonus = 5;

        /// <summary>
        /// 派生属性加成为 0（护盾走 <see cref="ApplyShield"/>）。
        /// 不能写成 p.AddShield(ShieldBonus) —— 那样每次重算派生值都会补一次盾。
        /// </summary>
        public void ApplyPassive(Player p) { }

        /// <summary>加 5 点护盾（每回合开头由 Player.ResetShieldForTurn 调用一次）。</summary>
        public void ApplyShield(Player p) => p.AddShield(ShieldBonus);

        public string DescribePassive() => "护盾 +" + ShieldBonus;
    }
}

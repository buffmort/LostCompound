using LostCompound.Tools;

namespace LostCompound.Items
{
    /// <summary>
    /// 磷化剑（配方 1，ID 22）：武器。背包时 +15 ATK，攻击挂 1 流血。
    /// 合成材料：铁剑 + 2 白磷 + 2 红磷。
    /// 「攻击挂 1 流血」的挂载点在 Player.Attack：命中且目标还活着时
    /// AddBuff(new BleedBuff(1))，流血本身由 Buffs\BleedBuff 实现、
    /// 由战斗回合管线 Tools.ApplyBuffs 每大回合结算。
    /// 本项目没有「装备中的武器」概念（IEquippable 在背包/饰品栏都生效），
    /// 所以「是否在用磷化剑」的判定 = 身上有没有磷化剑。
    /// </summary>
    public sealed class PhosphorusSword(int count) : Item(ItemIds.PhosphorusSword, "磷化剑", 1, false, count), IEquippable
    {
        public override ItemType? Type => ItemType.PhosphorusSword;

        /// <summary>背包时提供的攻击力加成。</summary>
        public const double AtkBonus = 15;

        /// <summary>每次命中挂上的流血层数（设计文档第九章配方 1：攻击挂 1 流血）。</summary>
        public const int BleedStacksPerHit = 1;

        public void ApplyPassive(Player p) => p.Atk += AtkBonus;

        public string DescribePassive() => "背包 +" + AtkBonus + " ATK，攻击挂 " + BleedStacksPerHit + " 流血";
    }
}

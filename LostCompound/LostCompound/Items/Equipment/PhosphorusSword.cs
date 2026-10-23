using LostCompound.Buffs;
using LostCompound.Monsters;
using LostCompound.Tools;
using LostCompound.Items;
using LostCompound.Items.Capabilities;
using LostCompound.Buffs.Types;

using LostCompound.Tools.Players;

namespace LostCompound.Items.Equipment
{
    /// <summary>
    /// 磷化剑（配方 1，ID 22）：武器。背包时 +15 ATK，攻击挂 1 流血。
    /// 合成材料：铁剑 + 2 白磷 + 2 红磷。
    /// 「攻击挂 1 流血」由本类实现 <see cref="IOnHit"/> 声明：命中且目标还活着时
    /// AddBuff(new BleedBuff(每回合伤害, 持续回合))，流血本身由 Buffs\Types\BleedBuff 实现、
    /// 由战斗回合管线 Tools.ApplyBuffs 每大回合结算。
    /// 本项目没有「装备中的武器」概念（IEquippable 在背包/饰品栏都生效），
    /// 所以「是否在用磷化剑」的判定 = 身上有没有磷化剑。
    /// </summary>
    public sealed class PhosphorusSword(int count) : Item(ItemIds.PhosphorusSword, "磷化剑", 1, false, count), IEquippable, IOnHit
    {
        public override ItemType? Type => ItemType.PhosphorusSword;

        /// <summary>背包时提供的攻击力加成。</summary>
        public const double AtkBonus = 15;

        /// <summary>每次命中挂上的流血「每回合伤害」（设计文档第九章配方 1：攻击挂 1 流血）。</summary>
        public const int BleedDamagePerHit = 1;

        /// <summary>
        /// 每次命中挂上的流血持续回合数。
        /// 文档只写了「攻击挂 1 流血」，没写持续多久；DOT 删除后流血必须有剩余回合，
        /// 所以这里按磷系道具一致的「3 回合」取值（白磷 3 回合、热硫 3 回合）。
        /// **这一条是我定的，不是文档数值，待你确认。**
        /// </summary>
        public const int BleedTurnsPerHit = 3;

        public void ApplyPassive(Player p) => p.Atk += AtkBonus;

        /// <summary>
        /// 命中特效：给目标挂一次流血（设计文档第九章配方 1 / 3.2 技能 6）。
        /// 数值与文案与改造前 Player.Attack 里那段逐字一致；「目标已经死了就不挂」
        /// 由调用方（Player.Attack）判断 —— 那条规则属于战斗流程，不属于某件武器。
        /// </summary>
        public void OnHit(Player attacker, Monster target)
        {
            target.AddBuff(new BleedBuff(BleedDamagePerHit, BleedTurnsPerHit));
            Console.WriteLine("磷化剑的磷毒让" + target + "开始流血！");
        }

        public string DescribePassive() => "背包 +" + AtkBonus + " ATK，攻击挂 " + BleedDamagePerHit + " 流血";
    }
}

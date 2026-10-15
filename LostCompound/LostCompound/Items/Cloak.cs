using LostCompound.Tools;

namespace LostCompound.Items
{
    /// <summary>
    /// 斗篷（ID 27，饰品，堆叠 1）：<b>目前只做了 +5 DEF</b>。
    ///
    /// 文档依据（DESIGN 第九章）：「27 斗篷 饰品 1 放射性区域低概率 |
    ///   +5 DEF，20% 免伤，免疫 3 次流血后破损」。
    /// ⚠️ 本次任务书里写的是「免疫 20 次 dot 后破损」，文档写的是「3 次流血」——
    ///   两处数字不一致（3 vs 20），未替用户裁定，见下方第 2 条 TODO。
    ///
    /// 已实现：+5 DEF（走 <see cref="IEquippable.ApplyPassive"/>，与盔甲/盾同一条重算通路，
    ///   进背包或饰品栏都生效，符合 DESIGN 8.2「任何被动物件都能放进饰品栏」）。
    ///
    /// ⚠️ **故意没做的两条效果，因为它们都需要新的伤害管线钩子，而项目里没有现成的**：
    ///
    /// 1. 「20% 免伤」—— 玩家挨打的唯一入口是 <see cref="Player.TakeDamage"/>，它
    ///    **不是 virtual**，里面直接就是 <c>ApplyDefense → 吃盾 → 扣血</c>，
    ///    没有任何「进伤修正量 / 减伤比例」的插槽（对比怪物侧有
    ///    <see cref="Monsters.Monster.ProcessIncomingDamage"/> 这个 virtual 预处理位，
    ///    但那是**怪物**挨打时才走的，玩家侧没有对应的一层）。
    ///    要做得给 Player 加一个「减伤修正量」的通道（由装备声明比例、TakeDamage 读取），
    ///    属于新增管线 —— 按「有就用、没有就报告，不要硬造」留 TODO。
    ///
    /// 2. 「免疫 20（文档：3）次流血后破损」—— 周性伤害现在是贴在玩家身上的
    ///    <see cref="Buffs.BleedBuff"/>，它在回合开场由 <c>Tools.ApplyBuffs</c> 直接
    ///    <c>owner.TakeDamage(TickDamage)</c>；**没有任何「玩家被 DOT 结算」的回调**
    ///    可以让装备插进去记账（道具既不参与 buff 列表，也拿不到那次结算）。
    ///    要做得加一个「玩家身上挂 buff 时通知装备」的钩子 —— 同样是新增管线。
    ///
    /// 所以本类只声明能力（IEquippable），把两条缺的写在报告里，不新造机制。
    ///
    /// ⚠️ 另一处文档与本次任务的冲突（记下来供复核）：
    ///   DESIGN 第九章给斗篷的**获取**是「放射性区域低概率」，DESIGN 8.2 也把斗篷
    ///   列在「尚未实现、效果依赖辐射系统」那一批里；而本次任务书要求把
    ///   「斗篷 = 3 木头 + 1 金属 + 1 布料」接进配方 0。本次按任务书接配方
    ///   （这也让新增的布料有了下游用途），但**获取方式与文档不一致**，请复核。
    /// </summary>
    public sealed class Cloak(int count) : Item(ItemIds.Cloak, "斗篷", 1, false, count), IEquippable
    {
        public override ItemType? Type => ItemType.Cloak;

        /// <summary>背包 / 饰品栏时提供的防御加成（文档：+5 DEF）。</summary>
        public const double DefBonus = 5;

        /// <summary>
        /// 只加 DEF（累加式，由 Player.RecomputeStats 保证「先还原成基础值再应用」）。
        /// 「20% 免伤」不能写在这里：ApplyPassive 是**重算通路**，一场战斗里会被调用很多次
        /// （回合开头 / buff 变化 / 环境变化…），把减伤当状态记在这里会被反复叠加/清零。
        /// </summary>
        public void ApplyPassive(Player p) => p.Def += DefBonus;

        public string DescribePassive() => "背包 +" + DefBonus + " DEF（20% 免伤与免疫流血待实现）";
    }
}

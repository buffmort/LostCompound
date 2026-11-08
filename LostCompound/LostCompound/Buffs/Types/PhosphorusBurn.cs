using LostCompound.Buffs;
using LostCompound.Monsters;
using LostCompound.Tools.Players;

namespace LostCompound.Buffs.Types
{
    /// <summary>
    /// **磷灼烧**（木乃伊专属，Hyberty 2026-11-08 定）：被磷系 / 火系道具打中时挂上。
    ///
    /// 效果：每回合扣 **10** 点血，持续 **3** 回合；**期间木乃伊的 DEF 变为 0**。
    ///
    /// DEF 归零为什么不在这个 buff 里写「改 Def / 再改回来」：
    ///   那是最容易出错的写法 —— buff 有三条移除路径（到期 / 战斗结束 / 将来驱散），
    ///   漏一条就永久偏移。这里改成**只声明状态**，由
    ///   <see cref="Monsters.Normal.Mummy.Def"/> 自己回答「我现在防御多少」：
    ///   身上有这个 buff 就是 0，没有就是 10。挂上自动生效、消失自动恢复，零还原代码。
    ///   （与玩家侧「重算制」同一思路，见 CONVENTIONS 2.3。）
    /// </summary>
    public sealed class PhosphorusBurn : Buff
    {
        /// <summary>每回合伤害（裁定：10）。</summary>
        public const int DamagePerTurn = 10;

        /// <summary>持续回合（裁定：3）。</summary>
        public const int StandardTurns = 3;

        public PhosphorusBurn(int stacks = 1, int turns = StandardTurns)
            : base(stacks, turns)
        {
        }

        public override string Name => "磷灼烧";

        public override int Id => BuffIds.PhosphorusBurn;

        /// <summary>
        /// 同类叠加：**层数取最高（本来就是固定 1 层）、时间相加**。
        ///
        /// 这里不再单独覆写 —— 基类 MergeFrom 已是这个口径（Hyberty 2026-11-08 定的全局规则）。
        /// 于「再中一次磷道具」的结果是：伤害仍是每回合 10 点（不翻倍），
        /// 但**持续时间会叠加**（3 回合 + 3 回合 = 6 回合）。
        /// </summary>
        public override string Describe() => "每回合" + DamagePerTurn + "伤害 · " + RemainingTurnsText;

        /// <summary>
        /// 玩家身上的版本：本 buff 只对木乃伊有意义（怪物侧规则），
        /// 万一被挂到玩家身上也**不结算伤害** —— 宁可什么都不做，也不要让玩家莫名其妙掉血。
        /// </summary>
        public override void OnTurnTick(Player owner)
        {
        }

        /// <summary>怪物身上的版本：每回合扣 10 点（走 TakeDamage，会正常过 DEF —— 但此时 DEF 已经是 0）。</summary>
        public override void OnTurnTick(Monster owner)
        {
            if (IsExpired) return;
            owner.TakeDamage(DamagePerTurn);
            ConsumeTurn();
        }
    }
}

using LostCompound.Monsters;
using LostCompound.Tools.Players;

namespace LostCompound.Buffs.Types
{
    /// <summary>
    /// 减压病（洞潜僵尸的 4 种 debuff 之一，DESIGN §6）。
    /// 效果：**-1 STR、-2 ATK**（每层/每次施加各一份）。
    ///
    /// 为什么只暴露 <see cref="StrBonus"/> / <see cref="AtkBonus"/> 而不在回合里直接改号：
    ///   直接改 <c>owner.Str</c> 会被下一次 <see cref="Player.RecomputeStats"/> 重算冲掉
    ///   （见 CONVENTIONS 2.3 与 Buff 基类的说明）。走修正量，挂上自动生效、到期自动恢复。
    ///
    /// 持续时间由施加方决定（用户裁定：**3 回合**，见 <see cref="DiverZombie"/>）。
    /// </summary>
    public sealed class DecompressionSickness(int stacks, int turns, bool survivesBattleEnd = false)
        : Buff(stacks, turns, survivesBattleEnd)
    {
        /// <summary>每份效果扣多少 STR（文档：1）。</summary>
        public const int StrLossPerStack = 1;

        /// <summary>每份效果扣多少 ATK（文档：2）。</summary>
        public const int AtkLossPerStack = 2;

        public override string Name => "减压病";

        public override int Id => BuffIds.DecompressionSickness;

        public override double StrBonus => -StrLossPerStack * Stacks;

        public override double AtkBonus => -AtkLossPerStack * Stacks;

        /// <summary>Buff 框只有两行宽，文案压短。</summary>
        public override string Describe() =>
            "STR-" + (StrLossPerStack * Stacks) + " ATK-" + (AtkLossPerStack * Stacks) + " " + RemainingTurnsText;

        public override void OnTurnTick(Player owner)
        {
            if (IsExpired) return;
            ConsumeTurn();
            // 修正量本身是「读的时候现算」的，但派生值需要重算一次才能反映 RemainingTurns 的变化
            owner.RecomputeStats();
        }

        public override void OnTurnTick(Monster owner)
        {
            if (IsExpired) return;
            ConsumeTurn();   // 怪物不读修正量（见 Buff.StrBonus 的说明）
        }
    }
}

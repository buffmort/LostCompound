using LostCompound.Monsters;
using LostCompound.Tools.Players;

namespace LostCompound.Buffs
{
    /// <summary>
    /// **额外回合**：每层换一次「再行动一次」。
    ///
    /// 语义（Hyberty 2026-11-21 定）：玩家**结束一次行动**时，把它当作又一轮玩家小回合，
    /// **直接再调一次 <see cref="Player.OnPlayerTurn"/>** —— 不是重开整个大回合。
    /// 所以：
    ///   · 怪物这一轮只出手一次（不是时间倒流）
    ///   · 额外回合里**不重走** <c>OnTurnStart</c>（道具耐久、流血都不再涨）
    ///   · 被眩晕时**整个回合**都不能动，额外回合也不执行（层数保留到下次能动的回合）
    ///
    /// 为什么层数用 Stacks、且时不按回合递减：
    ///   它的消耗点是「一次行动」，不是「一个大回合」。
    ///   如果放在 <see cref="OnTurnTick"/> 里递减，一次大回合只会扣一层，
    ///   但玩家可能在这一回合里已经用掉了两层（额外回合本身也是行动）。
    ///   所以真正扣层的地方是 <see cref="Player.TryConsumeExtraTurn"/>，
    ///   这里的两个 OnTurnTick 都必须是空的。
    /// </summary>
    public class ExtraTurn : Buff
    {
        public ExtraTurn(int stacks, int turns, bool survivesBattleEnd = false)
            : base(stacks, turns, survivesBattleEnd)
        {
        }

        public override string Name => "额外回合";

        public override int Id => BuffIds.ExtraTurn;

        /// <summary>它不是负面状态 —— 图标/排序按正面处理。</summary>
        public override bool IsDebuff => false;

        public override string Describe() => "可再行动 " + Stacks + " 次";

        /// <summary>故意为空：层数由「一次行动」驱动，不由回合驱动（见类注释）。</summary>
        public override void OnTurnTick(Player owner) { }

        public override void OnTurnTick(Monster owner) { }

        /// <summary>
        /// 玩家这次行动结束 → 扣一层，并告诉调用方「还能再动一次」。
        ///
        /// 这是 ExtraTurn 唯一的执行点：层数的消费、上限判断、用光移除全在这里，
        /// 战斗循环只负责「问了、照做」（见 <see cref="Buff.OnPlayerActionEnd"/> 的注释）。
        ///
        /// 用光之后**立刻从 owner 身上摘掉**，而不是等下一次 BuffTick：
        /// 否则右列 Buff 框会挂着一个「可再行动 0 次」的图标，
        /// 玩家会以为还能动，但下一次行动就没了 —— 那是最糟的一种"看起来能用"。
        /// </summary>
        public override bool OnPlayerActionEnd(Player owner)
        {
            if (owner == null || Stacks <= 0) return false;

            AddStacks(-1);

            if (Stacks <= 0) owner.RemoveBuff(this);

            owner.RefreshBuffBoxPublic();     // 扣层后右列立刻跟上
            return true;
        }
    }
}

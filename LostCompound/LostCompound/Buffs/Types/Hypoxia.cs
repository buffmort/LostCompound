using LostCompound.Monsters;
using LostCompound.Tools.Players;

namespace LostCompound.Buffs.Types
{
    /// <summary>
    /// 缺氧（洞潜僵尸的 4 种 debuff 之一，DESIGN §6）。
    /// 效果：**每回合 -0.1 STR，持续 5 回合，结束后恢复**。
    ///
    /// 「结束后恢复」**不需要任何还原代码**：本 buff 只暴露 <see cref="StrBonus"/>
    /// 这个修正量，到期被移除后，<see cref="Player.RecomputeStats"/> 自然算回原来的 STR
    /// （这正是 CONVENTIONS 2.3 里「重算制」代替记账制的地方）。
    ///
    /// 衰减量按**已经过了几个回合**算：第 1 回合 -0.1、第 2 回合 -0.2 …、第 5 回合 -0.5。
    /// 所以 StrBonus = -0.1 × 已过回合数。
    /// </summary>
    public sealed class Hypoxia(int stacks, int turns, bool survivesBattleEnd = false)
        : Buff(stacks, turns, survivesBattleEnd)
    {
        /// <summary>每回合掉多少 STR（文档：0.1）。</summary>
        public const double StrLossPerTurn = 0.1;

        /// <summary>标准持续时间（文档：5 回合）。</summary>
        public const int StandardTurns = 5;

        /// <summary>
        /// **已经结算过几个回合** —— 这是唯一可靠的衰减依据。
        ///
        /// 为什么不能像以前那样用「总回合数 − 剩余回合数」：
        ///   剩余回合是会被**外力改动**的（硫磺的「随机 debuff -2 回合」就干这个）。
        ///   一旦剩余回合被削到小于等于总回合数，差值就会变成 0 或负数，
        ///   于是已经掉了的 STR 被瞬间"还回来" —— 硫磺反而成了能治缺氧的药。
        ///   改成"结算次数"之后，只有真的走了 OnTurnTick 才增加，外力改不了它。
        ///
        /// 为什么它也要存档：读档后已累计的衰减必须还在，否则存读档就是免费洗点。
        /// 用 <see cref="TotalTurns"/> / <see cref="RestoreTotalTurns"/> 这一对通道存
        /// （基类本来就把这两个成员留给「需要额外状态的 buff」，这里借它捎带结算次数）。
        /// </summary>
        private int _elapsedTurns;

        public override string Name => "缺氧";

        /// <summary>负面状态（硫磺的随机减益会抽到它）。</summary>
        public override bool IsDebuff => true;

        public override int Id => BuffIds.Hypoxia;

        /// <summary>
        /// 报给存档层的数：这里不报"总回合数"，报**已结算次数** ——
        /// 存档层只需要一个 int 把状态带过存档，具体含义由本类自己定。
        /// </summary>
        public override int TotalTurns => _elapsedTurns;

        /// <summary>
        /// 读档专用：把已结算次数灌回来。只由 <c>SaveRestorer.BuildBuffs</c> 调用。
        /// 旧存档没有这个字段（值为 0），按「没结算过」处理 —— 与改动前同为 0 衰减，行为可接受。
        /// </summary>
        internal override void RestoreTotalTurns(int totalTurns) => _elapsedTurns = Math.Max(totalTurns, 0);

        public override double StrBonus => -StrLossPerTurn * _elapsedTurns * Stacks;

        public override string Describe() =>
            "STR-" + (StrLossPerTurn * _elapsedTurns * Stacks).ToString("F1") + " " + RemainingTurnsText;

        public override void OnTurnTick(Player owner)
        {
            if (IsExpired) return;
            ConsumeTurn();
            _elapsedTurns++;
            // 剩余回合变了 → 衰减量变了 → 派生值要重算一次
            owner.RecomputeStats();
        }

        public override void OnTurnTick(Monster owner)
        {
            if (IsExpired) return;
            ConsumeTurn();   // 怪物不读修正量
            _elapsedTurns++;
        }
    }
}

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
    /// 所以 StrBonus = -0.1 × (总回合数 − 剩余回合数)。
    /// </summary>
    public sealed class Hypoxia(int stacks, int turns, bool survivesBattleEnd = false)
        : Buff(stacks, turns, survivesBattleEnd)
    {
        /// <summary>每回合掉多少 STR（文档：0.1）。</summary>
        public const double StrLossPerTurn = 0.1;

        /// <summary>标准持续时间（文档：5 回合）。</summary>
        public const int StandardTurns = 5;

        /// <summary>
        /// 挂上时的总回合数。衰减量要靠它和 RemainingTurns 的差来算，
        /// 所以必须记下来（不能假定恒等于 <see cref="StandardTurns"/>：
        /// 同类叠加会改变剩余回合）。
        ///
        /// ⚠️ **不是 readonly**：读档时 <see cref="BuffFactory"/> 只能用「剩余回合」重建实例，
        /// 挂上时的总回合数在构造函数里已经丢失。若不把它改成可写并由存档层灌回
        /// （<see cref="RestoreTotalTurns"/>），读档后 `_initialTurns` 会等于当前剩余回合，
        /// 「已过回合数」归零 —— 玩家存读档一次就白赚回所有 STR 衰减。
        /// </summary>
        private int _initialTurns = Math.Max(turns, 1);

        public override string Name => "缺氧";

        /// <summary>负面状态（硫磺的随机减益会抽到它）。</summary>
        public override bool IsDebuff => true;

        public override int Id => BuffIds.Hypoxia;

        /// <summary>
        /// 报给存档层的「总回合数」= 挂上时的回合数（不是剩余回合）。
        /// 基类默认返回剩余回合，那样读档后已过回合数会归零，所以这里必须覆写。
        /// </summary>
        public override int TotalTurns => _initialTurns;

        /// <summary>
        /// 读档专用：把挂上时的总回合数灌回来。只由 <c>SaveRestorer.BuildBuffs</c> 调用。
        /// （旧存档没有这个字段，调用方会跳过它，保持基类注释里的「按剩余回合处理」行为。）
        /// </summary>
        internal override void RestoreTotalTurns(int totalTurns) => _initialTurns = totalTurns;

        /// <summary>已经过去的回合数（永久 buff 传 -1 时不成立，但缺氧不会是永久的）。</summary>
        private int ElapsedTurns => Math.Max(_initialTurns - RemainingTurns, 0);

        public override double StrBonus => -StrLossPerTurn * ElapsedTurns * Stacks;

        public override string Describe() =>
            "STR-" + (StrLossPerTurn * ElapsedTurns * Stacks).ToString("F1") + " " + RemainingTurnsText;

        public override void OnTurnTick(Player owner)
        {
            if (IsExpired) return;
            ConsumeTurn();
            // 剩余回合变了 → 衰减量变了 → 派生值要重算一次
            owner.RecomputeStats();
        }

        public override void OnTurnTick(Monster owner)
        {
            if (IsExpired) return;
            ConsumeTurn();   // 怪物不读修正量
        }
    }
}

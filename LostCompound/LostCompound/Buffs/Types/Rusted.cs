using LostCompound.Buffs;
using LostCompound.Monsters;
using LostCompound.Tools.Players;

namespace LostCompound.Buffs.Types
{
    /// <summary>
    /// **锈蚀**（锈铁傀儡 <see cref="Monsters.Normal.RustedGolem"/> 给玩家上的，Hyberty 2026-11-08 定）。
    ///
    /// 效果：**每层 -1 DEF**（走 <see cref="DefBonus"/> 这条既有通道，
    /// 由 <see cref="Player.RecomputeStats"/> 统一相加，不需要任何「还原」代码）。
    /// 最多 5 层（= -5 DEF），持续 3 回合。
    ///
    /// 为什么用 <see cref="DefBonus"/> 而不是在结算时直接改 `p.Def`：
    ///   与 STR/ATK 的修正通道同一套原因 —— 直接改会被下一次重算冲掉，
    ///   想「改回去」又得记住改了多少、在 buff 到期时补回来，
    ///   而 buff 有三条移除路径，漏一条就永久偏移。
    ///   暴露修正量之后：挂上自动生效、消失自动恢复。
    /// </summary>
    public sealed class Rusted : Buff
    {
        /// <summary>每层扣多少 DEF。</summary>
        public const double DefLossPerStack = 1.0;

        /// <summary>层数上限（= 最多扣 5 点 DEF）。</summary>
        public const int MaxLayers = 5;

        /// <summary>标准持续时间。</summary>
        public const int StandardTurns = 3;

        public Rusted(int stacks, int turns = StandardTurns)
            : base(stacks, turns)
        {
        }

        public override string Name => "锈蚀";

        public override int Id => BuffIds.Rusted;

        /// <summary>负面状态（硫磺的随机减益会抽到它）。</summary>
        public override bool IsDebuff => true;

        public override int MaxStacks => MaxLayers;

        /// <summary>每层 -1 DEF，由 Player.RecomputeStats 统一相加。</summary>
        public override double DefBonus => -DefLossPerStack * Stacks;

        public override string Describe() =>
            "DEF-" + (DefLossPerStack * Stacks).ToString("F0") + " " + RemainingTurnsText;

        /// <summary>纯修正量 buff，没有每回合效果（伤害由施加方决定）。</summary>
        public override void OnTurnTick(Player owner)
        {
        }

        /// <summary>挂到怪物身上不产生任何效果（本 buff 只对玩家有意义）。</summary>
        public override void OnTurnTick(Monster owner)
        {
        }
    }
}

using LostCompound.Monsters;
using LostCompound.Tools;
using LostCompound.Tools.Players;

namespace LostCompound.Buffs.Types
{
    /// <summary>
    /// **辐照**（Hyberty 2026-11-22 设计，放射性僵尸给的）。
    ///
    /// 效果：**没有直接效果**。它纯粹是一根「读数」——
    ///   玩家身上有 N 层，放射僵尸的攻击力就是 <c>10 + N × 5</c>。
    ///
    /// 为什么做成 buff 而不是怪自己记一个计数器：
    ///   ① 玩家**看得见**（Buff 框里显示「辐照 ×N・剩余 M 回合」），
    ///      才知道「越拖越疼」不是 bug；
    ///   ② 死亡/离开战斗时由 `ClearBattleBuffs()` 一并清掉，
    ///      不需要怪额外维护「这一场给它叠了几层」；
    ///   ③ 将来若有别的怪/道具要读「辐射层数」，读的是同一个东西。
    ///
    /// ⚠️ 唯一会读它的地方是 <see cref="Monsters.Elite.RadioactiveZombie"/>，
    ///   所以**不要**给它加 `AtkBonus`/`StrBonus` —— 那会让它在玩家身上生效成增伤，
    ///   方向正好反了（这是给**敌人**加攻击的层数，不是给玩家加的）。
    /// </summary>
    public class RadiationStack : Buff
    {
        /// <summary>上限层数（Hyberty：3）。</summary>
        public const int MaxStackCount = 3;

        public RadiationStack(int stacks, int turns, bool survivesBattleEnd = false)
            : base(stacks, turns, survivesBattleEnd)
        {
        }

        public override string Name => "辐照";

        public override int Id => BuffIds.RadiationStack;

        public override bool IsDebuff => true;

        /// <summary>层数封顶 3 —— 由 <see cref="Buff.AddStacks"/> 里的夹取统一管。</summary>
        public override int MaxStacks => MaxStackCount;

        /// <summary>
        /// 每回合只倒计时、**不做任何事**。它是别的东西（放射僵尸）读的状态，
        /// 不是自己会结算的 DoT —— 别在这里加扣血逻辑。
        /// </summary>
        public override void OnTurnTick(Player owner)
        {
            ConsumeTurn();
        }

        public override void OnTurnTick(Monster owner)
        {
            ConsumeTurn();
        }

        public override string Describe() => "×" + Stacks + "（放射僵尸打得更疼）· " + RemainingTurnsText;
    }
}

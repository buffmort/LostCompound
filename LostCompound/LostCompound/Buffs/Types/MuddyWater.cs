using LostCompound.Monsters;
using LostCompound.Tools.Players;

namespace LostCompound.Buffs.Types
{
    /// <summary>
    /// 搅乱泥沙（洞潜僵尸的 4 种 debuff 之一，DESIGN §6）。
    /// 效果：**这一回合打不开背包**（文档：1 回合；用户裁定同上）。
    ///
    /// 实现口径：**不按回合数递减，而是在玩家真的去开背包那一刻消费掉。**
    /// 为什么不用「OnTurnTick 里 ConsumeTurn」那套：
    ///   玩家 buff 的结算发生在**大回合开场**（`Tools.ApplyBuffs`），而「打开背包」是之后
    ///   玩家自己的动作。若在开销时递减，1 回合的 buff 会在玩家动手之前就到期 ——
    ///   效果一次都没生效。改成「第一次尝试开背包时被挡下、然后这个 buff 消失」，
    ///   才真的对应「这一回合打不开」。
    ///
    /// 判定入口在 <see cref="Player.OpenItems"/>（读 <see cref="Buff.BlocksInventory"/>）；
    /// 有多个这样的 buff 时只消费一个 —— 一次尝试只该被挡一次。
    /// </summary>
    public sealed class MuddyWater(int stacks, int turns, bool survivesBattleEnd = false)
        : Buff(stacks, turns, survivesBattleEnd)
    {
        public override string Name => "搅乱泥沙";

        public override int Id => BuffIds.MuddyWater;

        /// <summary>对「能不能开背包」的回答：挂着就挡。</summary>
        public override bool BlocksInventory => true;

        public override string Describe() => "无法开背包 " + RemainingTurnsText;

        /// <summary>
        /// 故意**不递减**：时长由「玩家尝试开背包」消费（见类注释）。
        /// 若改成在这里 ConsumeTurn，1 回合的 buff 会在玩家动手前就消失。
        /// </summary>
        public override void OnTurnTick(Player owner)
        {
        }

        public override void OnTurnTick(Monster owner)
        {
        }
    }
}

using LostCompound.Monsters;
using LostCompound.Tools.Players;

namespace LostCompound.Buffs.Types
{
    /// <summary>
    /// 呼吸性碱中毒（洞潜僵尸的 4 种 debuff 之一，DESIGN §6）。
    /// 效果：**每回合 -2 血**（每份效果各 2 点）。
    ///
    /// 走 <see cref="Player.TakeDotDamage"/> 而不是 TakeDamage：这一跳是**周期性伤害**，
    /// 装备的「吸收 DOT」通道（`IDotAbsorber`）只在这条入口上生效
    /// （与 BleedBuff 同一口径）。
    ///
    /// 持续时间由施加方决定（用户裁定：**5 回合**，见 <see cref="DiverZombie"/>）。
    /// </summary>
    public sealed class RespiratoryAlkalosis(int stacks, int turns, bool survivesBattleEnd = false)
        : Buff(stacks, turns, survivesBattleEnd)
    {
        /// <summary>每份效果每回合扣多少血（文档：2）。</summary>
        public const int DamagePerStack = 2;

        public override string Name => "呼吸性碱中毒";

        public override int Id => BuffIds.RespiratoryAlkalosis;

        /// <summary>本跳扣血量。</summary>
        public int TickDamage => DamagePerStack * Stacks;

        public override string Describe() => "每回合" + TickDamage + "伤 " + RemainingTurnsText;

        public override void OnTurnTick(Player owner)
        {
            if (IsExpired) return;
            Console.WriteLine(" 你受到 " + TickDamage + " 点碱中毒伤害");
            owner.TakeDotDamage(TickDamage);
            ConsumeTurn();   // 先结算再递减：最后一回合也要真的疼一次
        }

        public override void OnTurnTick(Monster owner)
        {
            if (IsExpired) return;
            Console.WriteLine(owner.Name + " 受到 " + TickDamage + " 点碱中毒伤害");
            owner.TakeDamage(TickDamage);
            ConsumeTurn();
        }
    }
}

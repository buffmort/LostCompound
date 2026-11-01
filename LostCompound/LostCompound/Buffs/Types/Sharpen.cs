using LostCompound.Monsters;
using LostCompound.Tools;
using LostCompound.Buffs;
using LostCompound.Tools.Players;

namespace LostCompound.Buffs.Types;

public class Sharpen : Buff
{
    public Sharpen(int stacks, int turns, bool survivesBattleEnd = false) : base(stacks, turns, survivesBattleEnd)
    {
    }

    /// <summary>
    /// 每层 +2 ATK（**不是** STR）。
    ///
    /// 改版原因（Hyberty裁定「力量有点超模了」）：原先每层 +1 STR，且当时这个 buff 叫「力量」。
    /// 名字后来也按Hyberty要求改成了「锐化」—— 效果加的是 ATK 不是 STR，继续叫「力量」会误导。
    /// 伤害公式是 <c>2 + STR × √ATK</c> —— STR 在**根号外面**、线性生效，
    /// ATK 在**根号里面**、收益递减，所以「+1 STR」与「+2 ATK」的实际收益差一个数量级：
    ///
    ///   基础 STR 3 / ATK 20 → 伤害 15.42
    ///   +1 STR（旧）  → STR 4 / ATK 20 → 19.89（+4.47，约 +29%）
    ///   +2 ATK（新）  → STR 3 / ATK 22 → 16.07（+0.65，约  +4%）
    ///
    /// 也就是说这一改把「力量」从「强力增伤」压成了「小幅度增伤」。
    /// 数值是Hyberty定的（每层 2 点），要调就改下面那个 2。
    /// </summary>
    private const double AtkPerStack = 2;

    public override double AtkBonus => AtkPerStack * Stacks;

    public override string Name => "锐化";

    /// <summary>本 buff 的 ID（见 <see cref="BuffIds"/>）。</summary>
    public override int Id => BuffIds.Sharpen;

    public override void OnTurnTick(Player owner)
    {
        ConsumeTurn();
    }

    public override void OnTurnTick(Monster owner)
    {
        ConsumeTurn();
    }

    /// <summary>
    /// Buff 框里显示一行「ATK +N · 剩余M回合」。
    /// 必须写：基类默认只显示时长，而这个 buff 的效果量（Stacks）单位是 ATK，
    /// 不写出来玩家看不到「+2 攻击」这回事。
    /// </summary>
    public override string Describe() => "ATK +" + AtkBonus + " · " + RemainingTurnsText;
}
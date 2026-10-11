using LostCompound.Monsters;
using LostCompound.Tools;

namespace LostCompound.Buffs;

public class SuperMaleBuff : Buff
{
    public SuperMaleBuff(int stacks, int turns) : base(stacks, turns)
    {
    }
    public override double StrBonus => 1000;
    public override string Name => "超雄综合症";

    /// <summary>本 buff 的 ID（见 <see cref="BuffIds"/>）。</summary>
    public override int Id => BuffIds.SuperMale;

    /// <summary>
    /// 显示文案：本 buff 的效果量（Stacks）**不是伤害**，所以绝不能沿用流血那套
    /// 「每回合 N 伤害」的写法（那正是改造前 Buff 框显示
    /// 「超雄综合症: 每回合2147483647伤害」的原因）。
    /// 这里报的是真正在起作用的东西：STR 加成 = StrBonus。
    ///
    /// 时长段：永久时走 <see cref="Buff.RemainingTurnsText"/> 显示「永久」，
    /// 不把哨兵 -1 印出来。与流血同理，非永久分支保持原有的「剩余N」写法，逐字不变。
    /// </summary>
    public override string Describe() =>
        "力量 +" + StrBonus + " · 剩余" + (IsPermanent ? RemainingTurnsText : RemainingTurns.ToString());

    public override void OnTurnTick(Player owner)
    {
      if(IsExpired) return;
        owner.RecomputeStats();
        ConsumeTurn();
    }

    public override void OnTurnTick(Monster owner)
    {
        ConsumeTurn();
    }
    
}
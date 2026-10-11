using LostCompound.Monsters;
using LostCompound.Tools;

namespace LostCompound.Buffs;

public class Strength : Buff
{
    public Strength(int stacks, int turns) : base(stacks, turns)
    {
    }

    public override double StrBonus => Stacks;

    public override string Name => "力量";

    /// <summary>本 buff 的 ID（见 <see cref="BuffIds"/>）。</summary>
    public override int Id => BuffIds.Strength;

    public override void OnTurnTick(Player owner)
    {
        ConsumeTurn();
    }

    public override void OnTurnTick(Monster owner)
    {
        ConsumeTurn();
    }
}
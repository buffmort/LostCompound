using LostCompound.Monsters;
using LostCompound.Tools;

namespace LostCompound.Buffs;

public class SuperMaleBuff : Buff
{
    public SuperMaleBuff(int stacks, int turns) : base(stacks, turns)
    {
    }

    public override string Name { get; }
    public override void OnTurnTick(Player owner)
    {
        throw new NotImplementedException();
    }

    public override void OnTurnTick(Monster owner)
    {
    }
}
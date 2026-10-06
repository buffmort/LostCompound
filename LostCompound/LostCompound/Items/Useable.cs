using LostCompound.Monsters;
using LostCompound.Tools;

namespace LostCompound.Items
{
    public interface Useable
    {
        void Use(Player p, List<Monster> m);
        void ExplainBattleOnly(Player p);
    }
}

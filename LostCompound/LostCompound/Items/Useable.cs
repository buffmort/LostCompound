using LostCompound.Monsters;
using LostCompound.Tools;

namespace LostCompound.Items
{
    public interface Useable
    {
        void use(Player p, List<Monster> m);
        void ExpBattle(Player p);
    }
}

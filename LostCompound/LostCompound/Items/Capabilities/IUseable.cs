using LostCompound.Monsters;
using LostCompound.Tools;

using LostCompound.Tools.Players;

namespace LostCompound.Items.Capabilities
{
    public interface IUseable
    {
        void Use(Player p, List<Monster> m);
        void ExplainBattleOnly(Player p);
    }
}

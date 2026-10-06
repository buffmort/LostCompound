using LostCompound.MapEnvironment;
using LostCompound.Tools;

namespace LostCompound.Items
{
    public interface TurnCount
    {
        void onBattleStart(Player p, EnvType envType);
        void onBattleEnd(Player p, EnvType envType);
    }
}

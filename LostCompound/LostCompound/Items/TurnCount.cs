using LostCompound.MapEnvironment;
using LostCompound.Tools;

namespace LostCompound.Items
{
    public interface TurnCount
    {
        void OnBattleStart(Player p, EnvType envType);
        void OnBattleEnd(Player p, EnvType envType);
    }
}

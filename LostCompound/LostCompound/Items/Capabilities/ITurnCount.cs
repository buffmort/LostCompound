using LostCompound.MapEnvironment;
using LostCompound.Tools;

using LostCompound.Tools.Players;

namespace LostCompound.Items.Capabilities
{
    public interface ITurnCount
    {
        void OnBattleStart(Player p, EnvType envType);
        void OnBattleEnd(Player p, EnvType envType);
    }
}

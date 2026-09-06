package items;

import MapEnvironment.EnvType;
import tools.Player;

public interface TurnCount {
    void onBattleStart(Player p, EnvType envType);
    void onBattleEnd(Player p,EnvType envType);
}

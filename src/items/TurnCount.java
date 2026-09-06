package items;

import tools.Player;

public interface TurnCount {
    void onBattleStart(Player p);
    void onBattleEnd(Player p);
}

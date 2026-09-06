package items;

import monsters.Monster;
import tools.Player;

import java.util.ArrayList;

public interface Useable {
    void use(Player p, ArrayList<Monster> m);
    void ExpBattle(Player p);
}

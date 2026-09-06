package items;

import MapEnvironment.EnvType;
import tools.Player;

public class Charcoal extends Item implements TurnCount {
    public Charcoal(int count){
        this.id = 3;
        this.name ="木炭";
        this.maxStack = 99;
        this.Stackable = true;
        this.count = count;
        this.type = ItemType.CHARCOAL;
    }
    @Override
    public ItemType getType() {
        return null;
    }


    @Override
    public void onBattleStart(Player p, EnvType envType) {

    }

    @Override
    public void onBattleEnd(Player p,EnvType envType) {

    }
}

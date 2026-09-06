package items;

import tools.Player;

public class Charcoal extends Item implements TurnCount {
    public Charcoal(int count){
        System.out.println("你获得了一个碳!");
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
    public void onBattleStart(Player p) {

    }

    @Override
    public void onBattleEnd(Player p) {

    }
}

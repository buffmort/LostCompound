package items;

import MapEnvironment.EnvType;
import tools.Player;

public class Wood extends Item implements TurnCount , Changeable{
    int change = 3;
    public Wood(int count){
        this.name = "木头";
        this.id = 2;
        this.maxStack = 99;
        this.Stackable = true;
        this.count = count;
        this.type = ItemType.WOOD;
    }
    @Override
    public ItemType getType() {
        return ItemType.WOOD;
    }


    @Override
    public void onBattleStart(Player p, EnvType envType) {
        if (envType == EnvType.LAVA_WATERFALL) {
            change -= 1;
            System.out.println("木头:" + change + "回合之后...");
        }
    }

    @Override
    public void onBattleEnd(Player p,EnvType envType) {

    }
    @Override
    public boolean ShouldRemove(){
        return this.change <=0;

    }

    @Override
    public Item Become() {
        System.out.printf("木头变成了碳!");
        return new Charcoal(count);
    }
}

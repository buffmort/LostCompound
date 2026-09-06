package items;

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
    public void onBattleStart(Player p) {
        change -=1;
        System.out.println("木头:"+change+"回合之后...");
    }

    @Override
    public void onBattleEnd(Player p) {

    }
    @Override
    public boolean ShouldRemove(){
        return this.change <=0;

    }

    @Override
    public Item Become() {
        return new Charcoal(count);
    }
}

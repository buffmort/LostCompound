package items;

public class Sand extends Item{
    public Sand(int count){
        this.name = "沙子";
        this.id = 6;
        this.count = count;
        this.Stackable = true;
        this.maxStack = 99;
        this.type = ItemType.SAND;
    }

    @Override
    public ItemType getType() {
        return this.type;
    }
}

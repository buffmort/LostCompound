package items;

public class Sulphur extends Item{
    public Sulphur(int count){
        this.name = "硫磺";
        this.id = 1;
        this.maxStack = 999;
        this.Stackable = true;
        this.count = count;

    }

    @Override
    public ItemType getType() {
        return ItemType.SULPHUR;
    }
}

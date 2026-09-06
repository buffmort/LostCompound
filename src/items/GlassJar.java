package items;

public class GlassJar extends Item{
    public GlassJar(int count){
        this.name = "玻璃罐";
        this.id = 7 ;
        this.count = count;
        this.maxStack = 99;
        this.Stackable = true;
    }
    @Override
    public ItemType getType() {
        return ItemType.GLASS_JAR;
    }
}

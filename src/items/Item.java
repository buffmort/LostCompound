package items;

public abstract class Item {
    int id;
    String name;
    int maxStack;
    boolean Stackable;
    public int count;
    public abstract ItemType getType();

    public String toString() {
        return this.name+" x"+count;
    }

    public int getMaxStack() {
        return maxStack;
    }

    public boolean isStackable() {
        return Stackable;
    }

    ItemType type = null;

    public int getId() {
        return id;
    }

    public String getName() {
        return name;
    }

    public int getCount() {
        return count;
    }
}

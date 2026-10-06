namespace LostCompound.Items
{
    public abstract class Item
    {
        public int id;
        public string name;
        public int maxStack;
        public bool Stackable;
        public int count;

        public abstract ItemType? getType();

        public override string ToString()
        {
            return this.name + " x" + count;
        }

        public int getMaxStack()
        {
            return maxStack;
        }

        public bool isStackable()
        {
            return Stackable;
        }

        // Java 原为：ItemType type = null;
        // C# 用可空枚举 ItemType? 来对应 Java 里「枚举引用可以为 null」的语义。
        public ItemType? type = null;

        public int getId()
        {
            return id;
        }

        public string getName()
        {
            return name;
        }

        public int getCount()
        {
            return count;
        }
    }
}

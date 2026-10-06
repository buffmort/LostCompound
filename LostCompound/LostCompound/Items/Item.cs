namespace LostCompound.Items
{
    public abstract class Item(int id, string name, int maxStack, bool stackable, int count)
    {
        public int Id { get; } = id;
        public string Name { get; } = name;
        public int MaxStack { get; } = maxStack;
        public bool IsStackable { get; } = stackable;
        public int Count { get; set; } = count;          // 会被增减，必须可写

        // Java 原为：ItemType type = null;
        // C# 用可空枚举 ItemType? 来对应 Java 里「枚举引用可以为 null」的语义。
        public abstract ItemType? Type { get; }           // 保留「可能为 null」语义

        public override string ToString() => $"{Name} x{Count}";
    }
}

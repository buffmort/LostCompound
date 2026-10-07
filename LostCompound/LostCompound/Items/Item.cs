namespace LostCompound.Items
{
    public abstract class Item(int id, string name, int maxStack, bool stackable, int count)
    {
        public int Id { get; } = id;
        public string Name { get; } = name;
        public int MaxStack { get; } = maxStack;
        public bool IsStackable { get; } = stackable;
        public int Count { get; set; } = count;          // 会被增减，必须可写

        // 从 Java 移植时这里的 Type 是可为 null 的枚举引用，故写成 ItemType?。
        // 现有 18 个子类都返回各自的确定类型，不会真的返回 null —— 保留可空性只是
        // 为了让 AddItem 里的「Type 可能为 null」判断继续成立，等有空再收紧为 ItemType。
        public abstract ItemType? Type { get; }           // 可空：见上方说明

        public override string ToString() => $"{Name} x{Count}";
    }
}

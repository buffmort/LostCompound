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
        // 现有 21 个子类都返回各自的确定类型，不会真的返回 null —— 保留可空性只是
        // 为了让 AddItem 里的「Type 可能为 null」判断继续成立，等有空再收紧为 ItemType。
        // （数字已按 `: Item(` 的实际实现数核对：21 个，与 Items\ItemFactory.All 的表一致。
        //   这份注释上一版写的是 18，是加道具时漏更新的过时数字。）
        public abstract ItemType? Type { get; }           // 可空：见上方说明

        public override string ToString() => $"{Name} x{Count}";
    }
}

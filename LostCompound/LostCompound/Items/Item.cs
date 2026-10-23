namespace LostCompound.Items
{
    public abstract class Item(int id, string name, int maxStack, bool stackable, int count)
    {
        public int Id { get; } = id;
        public string Name { get; } = name;
        public int MaxStack { get; } = maxStack;
        public bool IsStackable { get; } = stackable;

        /// <summary>
        /// 数量。可堆叠道具是「这一格里放了几个」，会被 <c>AddItem</c> 合并、被消耗品增减。
        ///
        /// ⚠️ **不可堆叠道具（<see cref="MaxStack"/> = 1）恒为 1。**
        ///   一件装备就是一个实例：它的 DEF / 减伤 / 耐久 / 已承伤次数全部记在**实例字段**上，
        ///   没有任何一条生效通路会去读 <see cref="Count"/>（玩家侧的加成都按
        ///   <c>ForEachEquippedItem</c> 遍历实例，一个实例只调一次）。
        ///
        ///   所以曾经允许 `new Cloak(5)` 是个埋着的坑：你会得到一个标着「斗篷 x5」、
        ///   实际只有一件效果、并且一破损就把「五件」一起收走的幽灵堆叠 ——
        ///   DEF 只 +5（不是 +25）、免伤只 20%（不是 ×0.8⁵）、耐久 50 次（不是 250 次）。
        ///   那个 `5` 除了显示以外没有任何作用。既然它没有语义，就在构造时钉死。
        ///
        ///   注意：**只夹构造，不夹 setter**。压缩沙瓶这类不可堆叠消耗品正是靠
        ///   `Count -= 1` 走到 0 来「用完消失」的（见 <c>CompressedSandBottle.ShouldRemove</c>），
        ///   如果把 setter 也锁成 1，它就会变成无限使用的道具。
        /// </summary>
        public int Count { get; set; } = stackable ? count : 1;   // 会被增减，必须可写

        // 从 Java 移植时这里的 Type 是可为 null 的枚举引用，故写成 ItemType?。
        // 现有 27 个子类都返回各自的确定类型，不会真的返回 null —— 保留可空性只是
        // 为了让 AddItem 里的「Type 可能为 null」判断继续成立，等有空再收紧为 ItemType。
        // （数字已按 `: Item(ItemIds.` 的实际实现数核对：27 个，与 Items\ItemFactory.All 的表一致。
        //   这份注释上一版写的是 21，是加道具时漏更新的过时数字 —— 补全配方 0
        //   （布料/热硫罐/药/毒气弹/斗篷/压缩沙瓶）时又加了 6 个，故为 27。）
        public abstract ItemType? Type { get; }           // 可空：见上方说明

        public override string ToString() => $"{Name} x{Count}";
    }
}

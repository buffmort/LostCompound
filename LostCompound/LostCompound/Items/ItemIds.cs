namespace LostCompound.Items
{
    /// <summary>
    /// 道具 ID 的唯一来源。
    ///
    /// 为什么要有这个文件？
    ///   以前 ID 是硬编码在两个地方：道具类里写 Item(1, ...)，配方里写 [5, 1]。
    ///   同一个 ID 出现多次，改一处忘一处，编译器还不报错。
    ///   现在只在这里定义一次，其它地方一律引用常量。
    ///
    /// const 是什么？
    ///   相当于 Java 的 static final。
    ///   它是「编译期常量」—— 值必须在写代码时就能确定（数字、字符串），
    ///   编译器会把它内联到使用处。所以它天然是 static，不用再写 static。
    ///
    /// ID 分段（对应设计文档的配方层级）：
    ///   1-9   基础材料 + 配方 0
    ///   10-20 配方 1
    ///   21-29 配方 2
    ///   30-40 特殊 / fatal
    /// </summary>
    public static class ItemIds
    {
        // ---- 配方 0：基础材料与消耗品 ----

        /// <summary>硫磺</summary>
        public const int Sulphur = 1;

        /// <summary>木头</summary>
        public const int Wood = 2;

        /// <summary>木炭（文档中称「碳」）</summary>
        public const int Charcoal = 3;

        /// <summary>热硫</summary>
        public const int FushionSluphur = 4;

        /// <summary>白磷</summary>
        public const int WhitePhosphorus = 5;

        /// <summary>沙子</summary>
        public const int Sand = 6;

        /// <summary>玻璃罐</summary>
        public const int GlassJar = 7;

        // 8-30 尚未实现的道具，等真正做的时候在这里补：
        // public const int WaterBottle = 8;
        // public const int Metal = 9;
        // ...

        /// <summary>红磷</summary>
        public const int RedPhosphorus = 31;
    }
}

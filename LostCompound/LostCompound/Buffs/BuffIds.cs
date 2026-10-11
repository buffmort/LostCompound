namespace LostCompound.Buffs
{
    /// <summary>
    /// Buff 的 ID 常量表。与 <see cref="Items.ItemIds"/> 同一套做法。
    ///
    /// 为什么需要 ID：Buff 现在只能靠「new 一个具体类」来挂，
    /// 没有任何地方能「按编号引用一个 buff」—— 调试发放、将来的存档读档、
    /// 事件按 ID 施加状态，都需要这张表。
    ///
    /// 维护约定：新增 buff **必须**在这里补一个常量、并在 BuffFactory 注册，
    /// 否则它无法被按 ID 挂载。
    ///
    /// 这里没有 ID 分段（不像 ItemIds 按配方层级分）—— buff 目前数量很少，
    /// 且没有「一个 buff 属于哪一层」这种概念，编号从小到大连续排即可。
    /// </summary>
    public static class BuffIds
    {
        /// <summary>流血（BleedBuff）。</summary>
        public const int Bleed = 1;

        /// <summary>超雄综合症（SuperMaleBuff）。</summary>
        public const int SuperMale = 2;

        /// <summary>力量（Strength）。</summary>
        public const int Strength = 3;
    }
}

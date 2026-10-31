namespace LostCompound.Items.Capabilities
{
    /// <summary>
    /// 「这个道具有**实例状态**需要存档」的能力位。
    ///
    /// 背景：`ItemFactory.Create(id, count)` 只能还原 **ID + 数量**，
    /// 而有些道具的强度记在自己身上（耐久、已承伤次数、倒计时），
    /// 读档时如果只按 ID 重建，这些账会全部清零 ——
    /// 一把剩 2 点耐久的镐子读档后变回 20，
    /// 一件已经挨了 49 下的斗篷读档后变回全新。
    ///
    /// 为什么做成接口而不是在 `Item` 上加两个 `virtual` 方法：
    ///   与 `IChangeable` / `ITurnCount` / `IDamageReduction` 保持同一套做法 ——
    ///   存档代码只问「你有没有实例状态」，不认识任何具体道具类；
    ///   新道具要不要存档，由它自己实现不实现这个接口决定。
    ///
    /// 约定：
    ///   · **键名一旦发布就不能改**（改了等于把旧存档的这一项清零），
    ///     用英文短名，见各实现里的 `Key` 常量。
    ///   · 只放 `int`：这套状态都是次数 / 耐久，没有小数。
    ///     `Count` 不在这里 —— 它由 `Item` 基类统一保存，别重复存。
    /// </summary>
    public interface IItemState
    {
        /// <summary>导出实例状态。没有额外状态的实现可以返回空字典。</summary>
        Dictionary<string, int> SaveState();

        /// <summary>
        /// 灌回实例状态。**缺键时保持当前值**（不要当成 0）——
        /// 旧存档里没有的键，说明那一项是后加的，默认值才是对的。
        /// </summary>
        void LoadState(IReadOnlyDictionary<string, int> state);
    }
}

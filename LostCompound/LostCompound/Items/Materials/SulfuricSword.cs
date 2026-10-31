using LostCompound.Items.Capabilities;

namespace LostCompound.Items.Materials
{
    /// <summary>
    /// 硫酸剑（ID 44，配方 0：铁剑 + 硫酸）。
    ///
    /// 文档原文：「获得后立刻消失（提示：你的剑变成氢气飞走了，你不会获得硫酸剑的）」
    /// —— 它是个**玩笑道具**，没有任何实际效果，合成出来就没了。
    ///
    /// 实现方式：`ShouldRemove()` 恒为 true（与「消耗品用完即移除」同一套收尸通路），
    /// 那句话写在 `Become()` 里 —— 该方法**只在它被真正移除时调用一次**，
    /// 而 `Player.HandCraft` 在合成之后会立刻扫一次，所以玩家看到的是
    /// 「合成 → 提示 → 剑没了」，顺序正是文档描述的那一步。
    ///
    /// ⚠️ 为什么不把提示写在构造函数里：构造还可能发生在
    ///   `ItemFactory` 建表 / 调试发放 / 存档还原等场合，
    ///   那些时候说「你的剑变成氢气飞走了」是莫名其妙的；
    ///   `Become()` 只有「真的从背包消失」这一个触发点。
    /// </summary>
    public sealed class SulfuricSword(int count)
        : Item(ItemIds.SulfuricSword, "硫酸剑", 99, true, count), IChangeable
    {
        public override ItemType? Type => ItemType.SulfuricSword;

        /// <summary>恒为 true：它就是来消失的。</summary>
        public bool ShouldRemove() => true;

        /// <summary>消失时播报那句玩笑；不留下替代品。</summary>
        public Item Become()
        {
            Console.WriteLine("你的剑变成氢气飞走了，你不会获得硫酸剑的！");
            Console.WriteLine("你得到了一滩绿糊糊的东西 真恶心! 你给他扔了");
            return null;
        }
    }
}

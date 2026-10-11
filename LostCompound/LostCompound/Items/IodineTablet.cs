using LostCompound.Monsters;
using LostCompound.Tools;

namespace LostCompound.Items
{
    /// <summary>
    /// 碘片（配方 1，ID 15）：消耗品，3 耐久，免疫 3 次辐射扣血。
    /// 辐射系统尚未实现，所以目前「使用」只做提示与耐久扣减。
    ///
    /// 数量与耐久的关系：**一片碘片 = 3 次使用**（耐久 3、每次 -1），
    /// 堆叠上限 5 指的是最多能带 5 片。所以 <see cref="Use"/> 只扣耐久、不扣 <see cref="Item.Count"/>。
    /// </summary>
    public sealed class IodineTablet(int count) : Item(ItemIds.IodineTablet, "碘片", 5, true, count), IUseable, IChangeable
    {
        public override ItemType? Type => ItemType.IodineTablet;

        /// <summary>剩余耐久（文档：3 耐久，每次 -1）。</summary>
        public int Durability { get; private set; } = 3;

        public void Use(Player p, List<Monster> m)
        {
            if (Durability <= 0)
            {
                Console.WriteLine("这片碘片已经用完了。");
                return;
            }

            // ⚠️ 这里**只减耐久、不减数量**。
            // 文档（DESIGN 11 / 12.2）写的是「碘片：消耗品，5 堆叠，3 耐久，每次 -1」——
            // 「每次 -1」指的是**耐久**，也就是「一片碘片可用 3 次」。
            // 原先这里同时还写了 `Count -= 1`，于是 Count 先归零、IChangeable 立刻把整叠移出背包：
            // 实际可用次数成了 min(堆叠数, 3)，1 片碘片只能吃 1 次，
            // 「3 耐久」和「MaxStacks = 5」两条数据全都落空（耐久永远到不了 2）。
            // 改成只扣耐久后，一次 Use 的语义与文档一致：一片碘片 3 次。
            // 用光（Durability 归零）后仍然留在背包、再点会提示「已经用完了」——
            // 这部分行为与改动前一致（改动前是整叠被移除，但那条是上面这个 bug 的副作用）。
            Durability -= 1;
            Console.WriteLine("你服下碘片，可免疫 3 次辐射扣血（剩余耐久 " + Durability + "）。");
            // 辐射系统尚未实现，免疫效果先只记录，等放射性区域做出来再接。
        }

        public void ExplainBattleOnly(Player p)
        {
            Console.WriteLine("碘片只能在战斗中使用！");
        }

        public bool ShouldRemove() => Count == 0;

        public Item Become() => null;
    }
}

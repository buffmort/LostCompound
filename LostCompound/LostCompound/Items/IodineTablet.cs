using LostCompound.Monsters;
using LostCompound.Tools;

namespace LostCompound.Items
{
    /// <summary>
    /// 碘片（配方 1，ID 15）：消耗品，3 耐久，免疫 3 次辐射扣血。
    /// 辐射系统尚未实现，所以目前「使用」只做提示与耐久扣减。
    /// </summary>
    public sealed class IodineTablet(int count) : Item(ItemIds.IodineTablet, "碘片", 5, true, count), Useable, Changeable
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

            Durability -= 1;
            Count -= 1;
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

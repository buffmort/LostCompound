using LostCompound.Monsters;
using LostCompound.Tools;

namespace LostCompound.Items
{
    /// <summary>
    /// 镐子（配方 1，ID 12）：工具，20 耐久。
    /// 用途（文档）：逃离(1) / 采掘(2) / 打破二叉树（直接损坏，地图重置）。
    /// 矿洞与二叉树系统尚未实现，所以目前只有耐久记录与主动消耗。
    /// </summary>
    public sealed class Pickaxe(int count) : Item(ItemIds.Pickaxe, "镐子", 1, false, count), IUseable, IChangeable
    {
        public override ItemType? Type => ItemType.Pickaxe;

        /// <summary>剩余耐久（文档：20 耐久）。</summary>
        public int Durability { get; private set; } = 20;

        public void Use(Player p, List<Monster> m)
        {
            if (Durability <= 0)
            {
                Console.WriteLine("镐子已经损坏了。");
                return;
            }

            Durability -= 1;
            Console.WriteLine("你挥了挥镐子（剩余耐久 " + Durability + "）。");
            // 矿洞与二叉树系统尚未实现，采掘/打破二叉树等具体行为等做出来再接。
        }

        public void ExplainBattleOnly(Player p)
        {
            Console.WriteLine("镐子只能在战斗中使用！");
        }

        /// <summary>耐久归零即损坏。</summary>
        public bool ShouldRemove() => Durability <= 0;

        public Item Become() => null;
    }
}

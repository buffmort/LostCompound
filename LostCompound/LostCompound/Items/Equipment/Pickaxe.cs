using LostCompound.Monsters;
using LostCompound.Tools;
using LostCompound.Items;
using LostCompound.Items.Capabilities;

using LostCompound.Tools.Players;

namespace LostCompound.Items.Equipment
{
    /// <summary>
    /// 镐子（配方 1，ID 12）：工具，20 耐久。
    /// 用途（文档）：逃离(1) / 采掘(2) / 打破二叉树（直接损坏，地图重置）。
    ///
    /// 现状：采掘(2) 已接上 —— 在可开采的环境（<see cref="LostCompound.MapEnvironment.Environment.CanMine"/>）
    /// 走 <see cref="Mining.Mine"/>，不在可开采环境里不消耗耐久。
    /// TODO 逃离(1)：需要地图逃逸机制（尚未实现，且和「离开地图」的语义要一起定）。
    /// TODO 打破二叉树：需要二叉树地图（尚未实现），届时本道具要能直接损坏并触发地图重置。
    /// </summary>
    public sealed class Pickaxe(int count) : Item(ItemIds.Pickaxe, "镐子", 1, false, count), IUseable, IChangeable
    {
        public override ItemType? Type => ItemType.Pickaxe;

        /// <summary>剩余耐久（文档：20 耐久）。</summary>
        public int Durability { get; private set; } = 20;

        /// <summary>采掘一次消耗的耐久（设计文档第九章：采掘(2)）。</summary>
        private const int MineDurabilityCost = 2;

        public void Use(Player p, List<Monster> m)
        {
            if (Durability <= 0)
            {
                Console.WriteLine("镐子已经损坏了。");
                return;
            }

            // 可开采的环境才动手：这里问的是环境自己的能力（CanMine），不是写死的类型判断 ——
            // 将来矿洞(5.4) 的矿道、放射性区域(5.6) 覆写 CanMine 之后，本方法不用改
            if (p.CurrentEnvironment?.CanMine != true)
            {
                Console.WriteLine("这里没有可以开采的东西。");
                // 没东西可挖：不扣耐久，玩家可以留着到矿洞/水淹洞穴再用
                return;
            }

            Durability -= MineDurabilityCost;
            Console.WriteLine("你挥镐开采起来（剩余耐久 " + Durability + "）。");

            // 产物规则集中在 Tools.Mining（文档 5.4：3-5 个矿物，全类别随机，10% 放射矿），
            // 这里只负责「谁挖」和「代价是多少」。
            foreach (Item ore in Mining.Mine())
            {
                p.AddItem(ore);
            }
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

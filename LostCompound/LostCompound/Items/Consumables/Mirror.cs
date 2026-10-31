using LostCompound.Monsters;
using LostCompound.Save;
using LostCompound.Tools;
using LostCompound.Items;
using LostCompound.Items.Capabilities;

using LostCompound.Tools.Players;

namespace LostCompound.Items.Consumables
{
    /// <summary>
    /// 镜子（ID 45，消耗品，堆叠 1）。
    ///
    /// 文档依据（DESIGN §9 第 45 行，用户原话）：
    ///   「45 镜子 消耗 1 | 5 压缩沙瓶 + 白磷 + 玻璃罐 + 2 金属
    ///     使用效果：将自己的属性进行下列轮换(base轮换) str与atk / def与hp；
    ///     记录当前道具位置 将第 n 个道具数量变成第 n+1 个道具数量 最后一个与第一个交换；
    ///     提示: 你照了照镜子 你现在变成了(左旋或右旋各占1/2)-玩家名！」
    ///
    /// 三处裁定（DESIGN §11 第 7 条）：
    ///   ① 「左旋/右旋」**只是文案随机** —— 效果完全相同（两组对换没有方向之分，
    ///      所以那句左右旋不影响任何数值，只是让提示语读起来像在转身）。
    ///   ② 数量轮换超过堆叠上限时**夹到上限**（见 <see cref="Player.RotateInventoryCounts"/>），
    ///      这样非堆叠道具「Count 恒为 1」的不变量不会被破坏。
    ///   ③ 镜子**先把自己从背包移除，再轮换** —— 它不占位、也不会把数量转给别人。
    ///
    /// 为什么「移除」直接写 <c>p.Inventory.Remove(this)</c> 而不走 IChangeable 的收尸通路：
    ///   收尸发生在 Use 返回**之后**（OpenItems 里那句 RemoveConsumedItems），
    ///   而轮换必须发生在它之前 —— 否则镜子还占着格子，轮换会把别的道具的数量搬到镜子上，
    ///   然后镜子连同那个数量一起消失（数量凭空少了）。所以这里当场移除。
    /// </summary>
    public sealed class Mirror(int count) : Item(ItemIds.Mirror, "镜子", 1, false, count), IUseable
    {
        public override ItemType? Type => ItemType.Mirror;

        /// <summary>提示语里的两个方向词。各 1/2 —— 纯文案，不影响数值。</summary>
        private static readonly string[] Directions = ["左旋", "右旋"];

        public void Use(Player p, List<Monster> m)
        {
            // ① 先把自己拿走（裁定③）。不从背包里移除的话，它会参与下面的数量轮换。
            p.Inventory.Remove(this);

            // ② 两组基础属性对换（裁定：只动 Base，Hp 超上限就夹住）。
            p.SwapBaseStats();

            // ③ 背包数量轮换（先快照再写，超上限夹住）。
            p.RotateInventoryCounts();

            // ④ 提示语。名字取档案里的玩家名，没起名时回退成「玩家」。
            Console.WriteLine("你照了照镜子 你现在变成了" + Directions[Random.Shared.Next(Directions.Length)] + "-" + Profile.Name + "！");
        }

        /// <summary>镜子在背包里就能用（不是战斗专属），所以这个分支永远不会被调用。</summary>
        public void ExplainBattleOnly(Player p)
        {
            Console.WriteLine("镜子随时可以用！");
        }
    }
}

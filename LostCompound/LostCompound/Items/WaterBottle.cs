using LostCompound.MapEnvironment;
using LostCompound.Monsters;
using LostCompound.Tools;

namespace LostCompound.Items
{
    /// <summary>
    /// 水瓶：把自己所在环境（熔岩瀑布）的水量回满。
    /// 只在有水源的环境里生效；没有水源时不消耗。
    /// </summary>
    /// <remarks>
    /// 同时实现 Changeable：数量归零后由 Player.HandleChangeableItems 自动移出背包，
    /// 这与 Metal / WhitePhosphorus 等消耗品的做法一致（否则会留下「水瓶 x0」）。
    /// </remarks>
    public sealed class WaterBottle(int count) : Item(ItemIds.WaterBottle, "水瓶", 10, true, count), IUseable, IChangeable
    {
        public override ItemType? Type => ItemType.WaterBottle;

        public void Use(Player p, List<Monster> m)
        {
            // 通过 Player 的 internal CurrentEnvironment 拿到当前房间的环境对象
            if (p.CurrentEnvironment is LavaWaterfall lw)
            {
                lw.RefillWater();
                Console.WriteLine("你倒空了水瓶，水量回满！");
                Count -= 1;
            }
            else
            {
                Console.WriteLine("这里没有可以使用的水源。");
                // 没有水源：不消耗，玩家可以留着到熔岩瀑布再用
            }
        }

        public void ExplainBattleOnly(Player p)
        {
            Console.WriteLine("水瓶只能在战斗中使用！");
        }

        public bool ShouldRemove() => Count == 0;

        public Item Become() => null;
    }
}

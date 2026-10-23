// 与 Tools.cs 同样的别名：不加这行时 Environment 会同时命中
// System.Environment（隐式 using）与 LostCompound.MapEnvironment.Environment，产生 CS0104。
using Environment = LostCompound.MapEnvironment.Environment;
using LostCompound.MapEnvironment;
using LostCompound.Monsters;
using LostCompound.Tools;
using LostCompound.Items;
using LostCompound.Items.Capabilities;

using LostCompound.Tools.Players;

namespace LostCompound.Items.Consumables
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
            // 通过 Player 的 internal CurrentEnvironment 拿到当前房间的环境对象，
            // 然后只问它「这里能不能回水」——不判断具体是哪种环境。
            Environment env = p.CurrentEnvironment;
            if (env?.CanRefillWater == true)
            {
                // 顺序（设计文档六：水瓶可扣岩浆怪当前 70% 血量）：
                //   1. 先泼怪（问每只怪「你被泼水会怎样」）
                //   2. 再回满水量（水瓶原本的功能，保持不变）
                //   3. 最后才 Count -= 1
                // 为什么先泼再回水：两件事互不依赖，但先结算伤害可以保证「这一泼把怪打死了」
                // 也不会让回水被跳过；反之若先回水再泼，中途 return 就会漏掉原来的功能。
                PourWater(m);

                env.RefillWater();
                // 回水后必须重算派生值：StrModifier 是按当前水量现算的，水量变了，
                // 玩家的 STR 也得跟着回到「基础 + 新修正」，否则要等到下一回合才恢复。
                p.RecomputeStats();
                Console.WriteLine("你把水喝了，水量回满！");
                Count -= 1;
            }
            else
            {
                Console.WriteLine("这里没有可以使用的水源。");
                // 没有水源：不消耗，玩家可以留着到熔岩瀑布再用
            }
        }

        /// <summary>
        /// 把水泼向列表里第一只「肯接下这瓶水」的怪（<see cref="Monster.OnWaterPoured"/>）。
        ///
        /// 一瓶水只泼一只：原来这里写死 `mon is LavaSlime &amp;&amp; mon.IsAlive` 并直接读
        /// LavaSlime.WaterBottleHpLossRatio，道具因此必须认识岩浆怪这个类型和它的比例常量；
        /// 每加一种「怕水的怪」（水元素、火系怪……）都要回来改这个道具。
        /// 现在改为逐只提问、第一只返回 true 的用掉这瓶水，反应内容（扣当前 70% 血量 + 播报）
        /// 归 LavaSlime 自己（见 Monster.OnWaterPoured 的说明）。
        /// 行为不变：仍是列表里第一只**活着**的岩浆怪吃这 70%，其余怪不动。
        /// </summary>
        private static void PourWater(List<Monster> monsters)
        {
            foreach (Monster mon in monsters)
            {
                if (mon.OnWaterPoured()) return;
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

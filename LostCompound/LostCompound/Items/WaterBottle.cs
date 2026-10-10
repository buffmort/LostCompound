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
                // 顺序（设计文档六：水瓶可扣岩浆怪当前 70% 血量）：
                //   1. 先泼岩浆怪（扣它当前血量的 70%）
                //   2. 再回满水量（水瓶原本的功能，保持不变）
                //   3. 最后才 Count -= 1
                // 为什么先泼再回水：两件事互不依赖，但先结算伤害可以保证「这一泼把怪打死了」
                // 也不会让回水被跳过；反之若先回水再泼，中途 return 就会漏掉原来的功能。
                PourOnLavaSlime(m);

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

        /// <summary>
        /// 把水泼向列表里的岩浆怪：扣它「当前血量」的 70%（设计文档六）。
        ///
        /// 为什么不走 Monster.TakeDamage：那条路会依次过护盾、DEF 减免和岩浆怪的
        /// 30% 高温免疫，最后落地的伤害远低于 70%（0.7 × 0.7 再减 DEF），
        /// 与文档「扣当前 70% 血量」不符。所以这里直接改 Hp —— 与「按当前血量比例」
        /// 类效果（如除法运算符）口径一致。
        ///
        /// 为什么这里不调 Die()：死了的怪由 Tools.RunBattle 的 RemoveDeadMonsters 统一收尸，
        /// 在这里结算掉落会让它跑在战斗循环之外，还可能和循环里的收尸重复。
        /// 一瓶水只泼一只：列表里可能有多只岩浆怪，取第一只存活的。
        /// </summary>
        private static void PourOnLavaSlime(List<Monster> monsters)
        {
            foreach (Monster mon in monsters)
            {
                if (mon is LavaSlime && mon.IsAlive)
                {
                    double loss = mon.Hp * LavaSlime.WaterBottleHpLossRatio;
                    mon.Hp -= loss;
                    Console.WriteLine("你把水泼向" + mon.Name + "，它被浇掉 " + loss.ToString("F1") + " 点血！");
                    Calculator.Delay(250);
                    return;
                }
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

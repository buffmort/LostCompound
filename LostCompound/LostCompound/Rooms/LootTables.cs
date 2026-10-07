using LostCompound.Items;
using LostCompound.MapEnvironment;

namespace LostCompound.Rooms
{
    /**
     * Author:  Hyberty
     * OOP牛逼
     */
    public static class LootTables
    {
        /**
         * 根据环境类型返回该环境下可能掉落的道具列表。
         * 每次调用都新建一个 List 并放入新的道具实例，所以调用方（宝箱/空房）
         * 拿去加进背包后不会影响后续抽取。
         */
        public static List<Item> GetLootTable(EnvType envType)
        {
            List<Item> items = [];

            switch (envType)
            {
                case EnvType.None:
                    Console.WriteLine("请调试！ 这是一个空环境!");
                    break;
                case EnvType.LavaWaterfall:
                    // 熔岩瀑布环境：掉落的特殊道具
                    items.Add(new Sulphur(1));
                    items.Add(new Sulphur(2));
                    items.Add(new Charcoal(1));
                    items.Add(new WhitePhosphorus(1));
                    items.Add(new RedPhosphorus(1));
                    items.Add(new RedPhosphorus(2));
                    items.Add(new Wood(5));
                    items.Add(new Wood(5));
                    items.Add(new Wood(5));

                    // 临时：让新道具（8 水瓶 / 9 金属 / 10 硫酸）在本环境可获取，便于测试。
                    // 按设计文档它们的正式来源分别是「水淹洞穴空房间 / 矿洞开采 / 合成」，与熔岩瀑布无关，
                    // 等对应系统实现后应从这里移除。
                    items.Add(new WaterBottle(1));
                    items.Add(new Metal(1));
                    items.Add(new SulfuricAcid(1));

                    break;
                // 其余环境（水淹洞穴/沙漠/矿洞/墓地/放射性区域/雾都城市）尚未实现，
                // 等做出来后再在这里补各自的掉落表。
                default:
                    // 未知环境默认返回空列表
                    break;
            }
            return items;
        }
    }
}

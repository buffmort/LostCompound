using LostCompound.MapEnvironment;
using LostCompound.Items;

namespace LostCompound.Rooms
{
    /**
     * Author:  Hyberty
     * OOP牛逼
     */
    public class LootTables
    {
        /**
         * 根据环境类型返回该环境下可能掉落的道具列表。
         * 返回的是一个新创建的 ArrayList，避免外部修改影响内部数据。
         */
        public static List<Item> getLootTable(EnvType envType)
        {
            List<Item> items = new List<Item>();
            switch (envType)
            {
                case EnvType.NONE:
                    Console.WriteLine("请调试！ 这是一个空环境!");
                    break;
                case EnvType.LAVA_WATERFALL:
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

                    break;
                // 如果以后新增环境，在这里添加 case
                // case Mine:
                //     items.add(new WoodenBow());
                //     break;
                default:
                    // 未知环境默认返回空列表
                    break;
            }
            return items;
        }
    }
}

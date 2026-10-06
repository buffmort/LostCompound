using LostCompound.MapEnvironment;
using LostCompound.Tools;
using LostCompound.Items;

namespace LostCompound.Rooms
{
    public class EmptyRoom : Room
    {
        private static readonly List<Item> possibleItems = new List<Item>();


        public EmptyRoom(EnvType envType) : base(RoomType.EMPTY, envType)
        {
            possibleItems.AddRange(LootTables.getLootTable(this.EnvType));
        }

        public override void onEnter(Player p)
        {
            Console.WriteLine("这里没有怪物，可能找到些道具。");

            if (!cleaned)
            {
                // 60% 概率生成随机道具
                if (Random.Shared.NextDouble() < 0.6 && possibleItems.Count > 0)
                {
                    int index = (int)(Random.Shared.NextDouble() * possibleItems.Count);
                    Item item = possibleItems[index];
                    p.addItem(item);
                    Console.WriteLine("你找到了 " + item.getName() + "！");
                }
                else
                {
                    Console.WriteLine("你搜刮了一番，但什么也没找到。");
                }
                // 标记为已清理，避免重复获取
                cleaned = true;
            }
            else
            {
                Console.WriteLine("这里已经被搜过了。");
            }
        }

        public override void onLeave(Player p)
        {
        }
    }
}

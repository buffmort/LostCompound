using LostCompound.MapEnvironment;
using LostCompound.Tools;
using LostCompound.Items;

namespace LostCompound.Rooms
{
    public sealed class EmptyRoom : Room
    {
        private static readonly List<Item> _possibleItems = [];

        public EmptyRoom(EnvType envType) : base(RoomType.Empty, envType)
        {
            _possibleItems.AddRange(LootTables.GetLootTable(Environment));
        }

        public override void OnEnter(Player p)
        {
            Console.WriteLine("这里没有怪物，可能找到些道具。");

            if (!IsCleaned)
            {
                // 60% 概率生成随机道具
                if (Random.Shared.NextDouble() < 0.6 && _possibleItems.Count > 0)
                {
                    int index = (int)(Random.Shared.NextDouble() * _possibleItems.Count);
                    Item item = _possibleItems[index];
                    p.AddItem(item);
                    Console.WriteLine("你找到了 " + item.Name + "！");
                }
                else
                {
                    Console.WriteLine("你搜刮了一番，但什么也没找到。");
                }
                // 标记为已清理，避免重复获取
                IsCleaned = true;
            }
            else
            {
                Console.WriteLine("这里已经被搜过了。");
            }
        }

        public override void OnLeave(Player p)
        {
        }
    }
}

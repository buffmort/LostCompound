using LostCompound.Items;
using LostCompound.MapEnvironment;
using LostCompound.Tools;

namespace LostCompound.Rooms
{
    /// <summary>
    /// 宝藏房：进入时从当前环境的道具池中摇取两次，两次获得的道具都给玩家。
    /// 与 EmptyRoom 的区别：EmptyRoom 是「有 60% 概率抽一次」，这里是「必定抽两次」。
    /// 只能拿一次，之后重入只提示。
    /// </summary>
    public sealed class TreasureRoom : Room
    {
        public TreasureRoom(EnvType envType) : base(RoomType.Treasure, envType)
        {
        }

        public override void OnEnter(Player p)
        {
            Console.WriteLine("你发现了一个宝箱！");

            if (IsCleaned)
            {
                Console.WriteLine("宝箱已经空了。");
                return;
            }

            // 必定摇两次道具池：每次重新取表，因此两次是相互独立的抽取
            for (int roll = 1; roll <= 2; roll++)
            {
                List<Item> pool = LootTables.GetLootTable(Environment);
                if (pool.Count == 0)
                {
                    Console.WriteLine("这个环境里没有可掉落的道具。");
                    continue;
                }

                Item picked = pool[Random.Shared.Next(pool.Count)];
                p.AddItem(picked);
                Console.WriteLine("第 " + roll + " 次摇取：你获得了 " + picked.Name + " x" + picked.Count + "！");
            }

            IsCleaned = true;
        }

        public override void OnLeave(Player p)
        {
        }
    }
}

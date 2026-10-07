using Environment = LostCompound.MapEnvironment.Environment;
using LostCompound.MapEnvironment;
using LostCompound.Tools;
using LostCompound.Items;

namespace LostCompound.Rooms
{
    public sealed class EmptyRoom : Room
    {
        public EmptyRoom(Environment environment) : base(RoomType.Empty, environment)
        {
        }

        public override void OnEnter(Player p)
        {
            // 没有环境对象（例如该环境尚未实现）时，退回原来的随机拾取逻辑
            if (Environment == null)
            {
                FallbackLoot(p);
                return;
            }

            if (IsCleaned)
            {
                Console.WriteLine("这里已经被搜过了。");
                return;
            }

            Console.WriteLine("这里没有怪物，可能找到些道具。");

            // 掷一次骰子决定空房事件
            double roll = Random.Shared.NextDouble();
            if (roll < 0.3)
            {
                // 事件1：山壁岩浆涌下，蒸干一部分水，但会留下一份基础材料
                Environment.DrainWater(40);
                Console.WriteLine("山壁涌下的岩浆蒸干了 40 点水！");

                // 基础材料 ID 1-9（ItemIds 里的 1~9 段），随机给一种
                int id = Random.Shared.Next(1, 10);
                Item reward = id switch
                {
                    1 => new Sulphur(1),
                    2 => new Wood(1),
                    3 => new Charcoal(1),
                    4 => new FushionSluphur(1),
                    5 => new WhitePhosphorus(1),
                    6 => new Sand(1),
                    7 => new GlassJar(1),
                    8 => new WaterBottle(1),
                    9 => new Metal(1),
                    _ => null,
                };
                if (reward != null)
                {
                    p.AddItem(reward);
                }
            }
            else if (roll < 0.6)
            {
                // 事件2：捡到一个水瓶
                p.AddItem(new WaterBottle(1));
            }
            else
            {
                Console.WriteLine("你搜刮了一番，但什么也没找到。");
            }

            // 标记为已清理，避免重复获取
            IsCleaned = true;
        }

        /// <summary>
        /// 备用拾取逻辑：只在没有环境对象（<see cref="Room.Environment"/> 为 null）时调用，
        /// 保持原来的「60% 概率从当前环境掉落表随机取一个」行为。
        /// 注意：候选列表是每次现取的局部变量，不再是静态列表（静态列表会在所有实例间共享累加）。
        /// </summary>
        private void FallbackLoot(Player p)
        {
            Console.WriteLine("这里没有怪物，可能找到些道具。");

            if (!IsCleaned)
            {
                List<Item> possibleItems = LootTables.GetLootTable(EnvKind);

                // 60% 概率生成随机道具
                if (Random.Shared.NextDouble() < 0.6 && possibleItems.Count > 0)
                {
                    int index = (int)(Random.Shared.NextDouble() * possibleItems.Count);
                    Item item = possibleItems[index];
                    // 获得播报已统一由 Player.AddItem 负责，这里不再重复打印
                    p.AddItem(item);
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

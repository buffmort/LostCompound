using Environment = LostCompound.MapEnvironment.Environment;
using LostCompound.MapEnvironment;
using LostCompound.Items;
using LostCompound.Tools;

namespace LostCompound.Rooms
{
    public sealed class StartRoom : Room
    {
        private bool _startItemsGiven;   // 防止重复发放起始道具

        public StartRoom(Environment environment) : base(RoomType.Start, environment)
        {
            IsVisited = true;
        }

        public override void OnEnter(Player p)
        {
            if (!IsCleaned)
            {
                Console.WriteLine("这里是起始房间");
                IsCleaned = true;
            }
            else
            {
                Console.WriteLine("你回到了起始房间");
            }

            // 只在第一次进入时发放起始道具
            if (!_startItemsGiven)
            {
                GiveStartItems(p);
                _startItemsGiven = true;
            }
        }

        public override void OnLeave(Player p)
        {
            // 空实现
        }

        // ==================== 起始道具选择逻辑 ====================

        private void GiveStartItems(Player p)
        {
            // 起始道具池。道具与权重一一对应（顺序必须一致）。
            // 设计文档 2.2：从池中随机提供 5 种，玩家选 1 种，共执行三次。
            Item[] startItems = CreateStartItems();

            // 对应权重（与 startItems 顺序一致，数字越大越容易抽到）
            int[] weights = [10, 10, 8, 8, 5, 5, 5, 5];

            // 可用索引列表（初始包含所有道具索引）
            List<int> available = [];
            for (int i = 0; i < startItems.Length; i++)
            {
                available.Add(i);
            }

            Console.WriteLine("===== 初始物品选择 =====");

            // 每轮从 5 个候选里选 1 个
            const int OptionsPerRound = 5;

            // 进行三轮选择
            for (int round = 1; round <= 3; round++)
            {
                Console.WriteLine("第 " + round + " 轮，请选择一种道具（输入 1~" + OptionsPerRound + "）：");

                // 从当前可用索引中随机抽取 OptionsPerRound 个不重复的索引
                // 注意用 Math.Min 兜底：可用道具不足时不能硬抽，否则会拿到 -1 索引而崩
                List<int> roundIndices = [];
                List<int> tempPool = [.. available];
                int drawCount = Math.Min(OptionsPerRound, tempPool.Count);
                for (int i = 0; i < drawCount; i++)
                {
                    // 加权随机抽取一个索引。权重全为正数，所以随机值必定落在某个区间内，
                    // pickedIdx 一定会被赋到有效值。
                    int totalWeight = 0;
                    foreach (int idx in tempPool)
                    {
                        totalWeight += weights[idx];
                    }
                    int rand = Random.Shared.Next(totalWeight);
                    int pickedIdx = tempPool[^1];   // 兜底值：即使权重表写错也不会拿到 -1
                    int cumulative = 0;
                    foreach (int idx in tempPool)
                    {
                        cumulative += weights[idx];
                        if (rand < cumulative)
                        {
                            pickedIdx = idx;
                            break;
                        }
                    }
                    roundIndices.Add(pickedIdx);
                    tempPool.Remove(pickedIdx);  // 从临时池移除，防止本轮重复
                }

                // 展示本轮抽到的候选道具
                for (int i = 0; i < roundIndices.Count; i++)
                {
                    Item item = startItems[roundIndices[i]];
                    Console.WriteLine((i + 1) + ". " + item.Name + " x" + item.Count);
                }

                // 读取玩家选择（1 到本轮的候选数）
                int choice = -1;
                bool valid = false;
                while (!valid)
                {
                    Console.Write("输入你的选择: ");
                    string input = Console.ReadLine();
                    if (int.TryParse(input, out int parsedChoice))
                    {
                        choice = parsedChoice;
                        if (choice >= 1 && choice <= roundIndices.Count)
                        {
                            valid = true;
                        }
                        else
                        {
                            Console.WriteLine("无效，请输入 1~" + roundIndices.Count + "。");
                        }
                    }
                    else
                    {
                        Console.WriteLine("无效，请输入数字。");
                        // 非数字的整行已被 ReadLine 读走，不需要额外丢弃
                    }
                }

                // 获取玩家选中的道具
                int selectedIdx = roundIndices[choice - 1];
                Item selectedItem = startItems[selectedIdx];
                p.AddItem(selectedItem);
                // 获得播报已统一由 Player.AddItem 负责，这里不再重复打印

                // 从总可用池中移除该索引，防止后续轮次再次获得相同道具
                available.Remove(selectedIdx);

                Console.WriteLine();
            }
        }

        // 起始道具池。数量由设计调整为「原值 +1」。
        private Item[] CreateStartItems()
        {
            return
            [
                new Wood(2),
                new Wood(3),
                new Sulphur(3),
                new Sulphur(2),
                new GlassJar(2),
                new WhitePhosphorus(3),
                new RedPhosphorus(2),
                new Sand(6),
            ];
        }
    }
}

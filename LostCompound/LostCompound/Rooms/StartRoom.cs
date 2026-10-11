using Environment = LostCompound.MapEnvironment.Environment;
using LostCompound.MapEnvironment;
using LostCompound.Items;
using LostCompound.Tools;

namespace LostCompound.Rooms
{
    public sealed class StartRoom : Room
    {
        // 本层起始道具是否已发放。
        // 注意这是「本层已发」而不是「全局已发」：每层地图各有一个 StartRoom 实例，
        // 玩家走到下一层时是新实例、_startItemsGiven 重新为 false，所以「每层开始都有一次开局选择」
        // 是靠「每层一个新实例」实现的，而不是靠同一个房间反复发。
        // 因此这里的判断必须保留 —— 它防的是「玩家回起始房间重复领物资」。
        private bool _startItemsGiven;

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

            // 只在第一次进入（本层）时发放起始道具。
            // 下一层的 StartRoom 是新实例，会重新走一遍这里的发放逻辑。
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
            // 起始道具池按本层环境决定（用户裁定：StartRoom 每层都有，所以每层都能重新选）。
            // 池子与权重都来自 LootTables 的两张表，不再在本类里写死。
            // 设计文档 2.2：从池中随机提供 5 种，玩家选 1 种，共执行三次。
            List<Item> startItems = LootTables.GetStartPool(EnvKind);
            List<int> weights = LootTables.GetStartWeights(EnvKind);

            // ⚠️ 权重表与池子必须一一对应，而错位时编译器不会报错（权重会张冠李戴）。
            // 这里用 Math.Min 兜底：万一某个环境的权重表写短了，
            // 至少保证后面所有 weights[idx]（idx < startItems.Count）都取得到值、不会越界崩掉。
            int usableWeights = Math.Min(weights.Count, startItems.Count);
            if (usableWeights < weights.Count || weights.Count < startItems.Count)
            {
                Console.WriteLine("警告：起始道具池与权重表长度不一致（池 " + startItems.Count
                                  + " 项 / 权重 " + weights.Count + " 项），已按较短的一侧截断。");
            }

            // 可用索引列表（初始包含所有权重有效的道具索引）
            List<int> available = [];
            for (int i = 0; i < usableWeights; i++)
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

                // 候选数为 0（权重表为空，或长度不匹配被截断到 0）时必须在这里收场：
                // 下面一个候选都抽不出来 → roundIndices.Count == 0 → 输入校验的条件会退化成
                // `choice >= 1 && choice <= 0`，**永假**，无论玩家输入什么都会死循环（这正是本轮要修的坑）。
                // 用 return 而不是 continue：可用池本来就是空的，后面几轮同样一个候选都抽不出来，
                // 继续转轮次只会把这句提示重复三遍。
                // 说明：当前 LootTables.GetStartWeights 的 default 回落到 8 项基础权重，走不到这里；
                //       这是把「池子与权重表耦合」这件事做成的显式防线。
                if (drawCount == 0)
                {
                    Console.WriteLine("没有可选的起始道具（权重表为空），跳过初始物品选择。");
                    return;
                }

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
                    if (input == null)
                    {
                        // 输入流结束（管道关闭 / 重定向读完 / Ctrl+Z）：ReadLine 之后每次都返回 null，
                        // 而 int.TryParse(null) 恒为 false —— 原先这个 while (!valid) 就永远出不来，
                        // 开局直接卡死在这里（这是全项目唯一一处裸用 ReadLine 的地方，连 ?. 兜底都没有）。
                        // 处理：按「取默认」算 —— 默认本轮第 1 个候选，后面的发道具流程照旧走完。
                        // ⚠️ 空行（""）不在这里：空行仍然走下面的「无效，请输入数字」重问，
                        //    那是玩家有意不选，与「输入流没了」不是一回事。
                        Console.WriteLine();
                        Console.WriteLine("输入已结束，默认选择第 1 项。");
                        choice = 1;
                        valid = true;
                        break;
                    }
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
    }
}

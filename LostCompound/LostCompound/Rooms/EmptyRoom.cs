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

                // 基础材料 ID 1-9（ItemIds 里的 1~9 段），随机给一种。
                // 候选集从 ItemFactory.All 按 ID 段筛出来，不再手抄一份「1..9 → 道具」的 switch：
                // 那份 switch 与 ItemIds/ItemFactory 三处重复维护，改一处忘一处编译器还不报错。
                // 顺序即 ItemFactory.All 的顺序，候选集与原先逐条一致（1..9 全部，无权重差异）。
                var basics = ItemFactory.All
                    .Where(entry => entry.Id >= ItemIds.Sulphur && entry.Id <= ItemIds.Metal)
                    .ToList();
                Item reward = basics.Count > 0 ? basics[Random.Shared.Next(basics.Count)].Create(1) : null;
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
        /// 备用拾取逻辑：只在没有环境对象（<see cref="Room.Environment"/> 为 null）时调用。
        /// 注意：候选列表是每次现取的局部变量，不再是静态列表（静态列表会在所有实例间共享累加）。
        ///
        /// ⚠️ 关于那句「保持原来的 60% 概率」的说法（原注释这里写错了，现改正）：
        ///   本方法的实际行为是「60% 概率 × 可能为空的掉落表」——
        ///   环境为 null 时 <see cref="EnvKind"/> 就是 <see cref="EnvType.None"/>，
        ///   而 <see cref="LootTables.GetLootTable"/> 对 None 返回的是一张**空表**，
        ///   于是 <c>possibleItems.Count > 0</c> 恒为假，永远是「什么也没找到」。
        ///   所以 60% 这个概率在这个分支里**根本不起作用** —— 原来那句注释描述的是一种不存在的现象。
        ///
        /// 为什么不在这里补一份兜底掉落表：正常玩法走不到本方法。
        ///   选中环境的两条入口（GameLobby.SelectEnvironment / DebugMenu.SelectEnvironment）
        ///   都只列 EnvironmentCatalog.All 中已实现的环境，None 进不去；
        ///   只有「catalog 里加了名字、EnvironmentFactory 却忘了加分支」这种**违反既定约定**的
        ///   状态才会落到这里。那时正确做法是把工厂补上（catalog 的注释就是这么要求的），
        ///   而不是让「环境没实现」继续安静地发道具。故保留空表、只把注释改成与代码相符。
        /// </summary>
        private void FallbackLoot(Player p)
        {
            Console.WriteLine("这里没有怪物，可能找到些道具。");

            if (!IsCleaned)
            {
                List<Item> possibleItems = LootTables.GetLootTable(EnvKind);

                // 60% 概率生成随机道具（注意：可能的候选表为空时下面这个分支永远走不到 else）
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

using Environment = LostCompound.MapEnvironment.Environment;
using LostCompound.MapEnvironment;
using LostCompound.Tools;
using LostCompound.Items;
using LostCompound.Rooms;

using LostCompound.Tools.Players;

namespace LostCompound.Rooms.Types
{
    public sealed class EmptyRoom : Room
    {
        public EmptyRoom(Environment environment) : base(RoomType.Empty, environment)
        {
        }

        public override void OnEnter(Player p)
        {
            if (IsCleaned)
            {
                Console.WriteLine("这里已经被搜过了。");
                return;
            }

            Console.WriteLine("这里没有怪物，可能找到些道具。");

            // 空房间里**会发生什么**由环境决定（Environment.RollEmptyRoomEvent）。
            // 本类只负责执行：把播报写进日志、把道具塞进背包。
            //
            // 为什么不再在这里写 if：
            //   原先这里是三个写死的分支，其中「山壁岩浆涌下 → 蒸干 40 点水」是熔岩瀑布专属的，
            //   却不看环境，于是在水淹洞穴也会照发 —— 而那边没有「水量」这个东西，
            //   基类的 DrainWater 又是空实现，玩家白白看到一句虚构的损失。
            //   每加一个新环境还要回来补一个 if，漏补的那次只会安静地串台词。
            //
            // 没有环境时（EnvironmentFactory 对未实现的环境返回 null）：
            //   EnvKind 是 None → 通用掉落表为空 → 与改造前完全一致（「什么也没找到」）。
            EmptyRoomEvent ev = Environment?.RollEmptyRoomEvent();
            if (ev != null)
            {
                if (!string.IsNullOrEmpty(ev.Message))
                {
                    Console.WriteLine(ev.Message);
                }
                foreach (Item item in ev.Items)
                {
                    p.AddItem(item);   // 获得播报统一由 Player.AddItem 负责
                }
            }
            else
            {
                LootFromTable(p);
            }

            // 标记为已清理，避免重复获取
            IsCleaned = true;
        }

        /// <summary>
        /// 通用搜刮：60% 从**本环境**的掉落表里抽一件，否则什么也没有。
        ///
        /// 这条路径只在环境没有声明专属事件时才走（目前是水淹洞穴，以及将来那些
        /// 还没写空房间事件的环境）。掉落表由 <see cref="LootTables.GetLootTable"/> 按环境给，
        /// 所以不会出现「水淹洞穴掉出硫磺」这种串环境的掉落。
        ///
        /// 注意：候选表每次现取（GetLootTable 每次都 new 一份），
        /// 不要改成静态列表 —— 静态列表会在所有房间实例之间共享累加。
        /// </summary>
        private void LootFromTable(Player p)
        {
            List<Item> possibleItems = LootTables.GetLootTable(EnvKind);

            // 60% 概率生成随机道具；候选表为空时（如 EnvType.None）永远走 else
            if (Random.Shared.NextDouble() < 0.6 && possibleItems.Count > 0)
            {
                int index = (int)(Random.Shared.NextDouble() * possibleItems.Count);
                p.AddItem(possibleItems[index]);
            }
            else
            {
                Console.WriteLine("你搜刮了一番，但什么也没找到。");
            }
        }

        public override void OnLeave(Player p)
        {
        }
    }
}

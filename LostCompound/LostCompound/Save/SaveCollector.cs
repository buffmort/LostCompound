using Environment = LostCompound.MapEnvironment.Environment;
using LostCompound.Buffs;
using LostCompound.Items;
using LostCompound.Items.Capabilities;
using LostCompound.MapEnvironment;
using LostCompound.Maps;
using LostCompound.Rooms;
using LostCompound.Rooms.Types;
using LostCompound.Tools.Players;

namespace LostCompound.Save
{
    /// <summary>
    /// 采集：把**正在运行的**游戏对象拍成一份纯数据快照。
    ///
    /// 这一层只做「读」，不改任何游戏状态 —— 所以存档失败也不会影响正在玩的对局。
    /// 与它对称的是 <see cref="SaveRestorer"/>（只做「写」）。
    /// </summary>
    public static class SaveCollector
    {
        /// <summary>拍一张快照。</summary>
        /// <param name="p">玩家</param>
        /// <param name="map">当前地图（环境对象可从任意房间拿到，同一层共享一个实例）</param>
        /// <param name="tmg">生成器 —— 存档要它的种子，而不是地图本身</param>
        public static SaveData Capture(Player p, Room[][] map, TreeMazeGenerator tmg)
        {
            // 环境对象：TreeMazeGenerator 建图时创建一次、所有房间共享，
            // 所以取哪一格都一样。取 [0][0] 是因为它一定存在（起点房间）。
            Environment env = map[0][0].Environment;
            int cols = map.Length > 0 ? map[0].Length : 0;

            SaveData data = new()
            {
                // 层数取生成器里那份快照，而不是硬编码 —— 读档时它就是「玩家在第几层」。
                Layer = tmg.Layer,
                EnvType = (env?.Type ?? EnvType.None).ToString(),
                EnvState = env?.SaveState() ?? [],

                // 地图只存"能重建它"的参数：种子 + 尺寸 + 算法版本
                Seed = tmg.Seed,
                GeneratorVersion = TreeMazeGenerator.GeneratorVersion,
                Rows = map.Length,
                Cols = cols,

                Player = CapturePlayer(p),
            };

            CaptureProgress(data, map, cols);
            return data;
        }

        /// <summary>
        /// 采集**进度**（哪些格子走过、哪些搜过、开局道具发过没）。
        ///
        /// 这些是玩家玩出来的状态，种子推不出来，必须存。
        /// 但只存「变了的那几格」的编号 —— 没有访问过的格子不占任何空间。
        /// </summary>
        private static void CaptureProgress(SaveData data, Room[][] map, int cols)
        {
            for (int r = 0; r < map.Length; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    Room room = map[r][c];
                    int index = r * cols + c;   // 压平编号，与 SaveData.Visited 的约定一致

                    if (room.IsVisited)
                    {
                        data.Visited.Add(index);
                    }
                    if (room.IsCleaned)
                    {
                        data.Cleaned.Add(index);
                    }
                    // 全图只有一个 StartRoom，命中即记
                    if (room is StartRoom start && start.StartItemsGiven)
                    {
                        data.StartItemsGiven = true;
                    }
                }
            }
        }

        private static PlayerData CapturePlayer(Player p)
        {
            PlayerData d = new()
            {
                Name = p.Name ?? "",
                Hp = p.Hp,
                Shield = p.Shield,
                BaseHp = p.BaseHp,
                BaseStr = p.BaseStr,
                BaseAtk = p.BaseAtk,
                BaseDef = p.BaseDef,
                InventoryCapacity = p.InventoryCapacity,
                AccessorySlots = p.AccessorySlots,
                FightCount = p.FightCount,
                SkipTurn = p.SkipTurn,
                IsDead = p.IsDead,
                Row = p.PlayerRow,
                Col = p.PlayerCol,
                Tier1RecipesUnlocked = p.Tier1RecipesUnlocked,
            };

            foreach (Item item in p.Inventory) d.Inventory.Add(CaptureItem(item));
            foreach (Item item in p.Accessories) d.Accessories.Add(CaptureItem(item));
            foreach (Buff buff in p.Buffs)
            {
                d.Buffs.Add(new BuffData
                {
                    Id = buff.Id,
                    Stacks = buff.Stacks,
                    Turns = buff.RemainingTurns,
                    SurvivesBattleEnd = buff.SurvivesBattleEnd,   // 实例级标记，必须存
                });
            }
            return d;
        }

        /// <summary>一件道具：ID + 数量 + 只有实现了 <see cref="IItemState"/> 的道具才有的实例状态。</summary>
        private static ItemData CaptureItem(Item item)
        {
            ItemData d = new() { Id = item.Id, Count = item.Count };
            if (item is IItemState stateful)
            {
                d.State = stateful.SaveState();
            }
            return d;
        }
    }
}

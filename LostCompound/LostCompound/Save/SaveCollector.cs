using Environment = LostCompound.MapEnvironment.Environment;
using LostCompound.Buffs;
using LostCompound.Items;
using LostCompound.Items.Capabilities;
using LostCompound.MapEnvironment;
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
        public static SaveData Capture(Player p, Room[][] map)
        {
            // 环境对象：TreeMazeGenerator 建图时创建一次、所有房间共享，
            // 所以取哪一格都一样。取 [0][0] 是因为它一定存在（起点房间）。
            Environment env = map[0][0].Environment;

            SaveData data = new()
            {
                Layer = 1,   // 层数系统未实现，恒为 1（DTO 里预留）
                EnvType = (env?.Type ?? EnvType.None).ToString(),
                EnvState = env?.SaveState() ?? [],
                Player = CapturePlayer(p),
                Map = CaptureMap(map),
            };
            return data;
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
                d.Buffs.Add(new BuffData { Id = buff.Id, Stacks = buff.Stacks, Turns = buff.RemainingTurns });
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

        private static MapData CaptureMap(Room[][] map)
        {
            MapData d = new()
            {
                Rows = map.Length,
                Cols = map.Length > 0 ? map[0].Length : 0,
            };

            // 行优先展平：JSON 里二维数组不好看也不好读，一维列表 + 行列数足够
            foreach (Room[] row in map)
            {
                foreach (Room room in row)
                {
                    d.Rooms.Add(new RoomData
                    {
                        Type = room.Type.ToString(),
                        Doors = room.GetDoors(),
                        Visited = room.IsVisited,
                        Cleaned = room.IsCleaned,
                        // 只有起始房间有这个状态，其它房间是 false
                        StartItemsGiven = room is StartRoom start && start.StartItemsGiven,
                    });
                }
            }
            return d;
        }
    }
}

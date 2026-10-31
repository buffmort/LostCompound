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
    /// 还原：把一份快照灌回游戏对象。与 <see cref="SaveCollector"/> 对称。
    ///
    /// 原则：**遇到坏数据就降级，不崩**。
    ///   未知道具 ID → 跳过那一件并提示（其余照常读）；
    ///   未知房间类型 → BlankRoom 兜底；
    ///   房间数量对不上 → 整档拒绝（地形都错位了，读进来只会更糟）。
    /// </summary>
    public static class SaveRestorer
    {
        /// <summary>
        /// 把存档灌进 <paramref name="player"/>，并重建出地图。
        /// 返回 false 表示存档不可用（调用方负责提示）。
        /// </summary>
        public static bool TryRestore(SaveData data, Player player, out Room[][] map)
        {
            map = null;
            if (data == null || data.Player == null || data.Map == null)
            {
                Console.WriteLine("[存档] 存档内容不完整，无法读取。");
                return false;
            }

            // ---- 1. 环境 ----
            // 枚举按**名字**还原；解析不出来就当作「没有环境」而不是随便挑一个
            if (!Enum.TryParse(data.EnvType, out EnvType envType))
            {
                envType = EnvType.None;
            }
            Environment env = EnvironmentFactory.Create(envType);
            env?.LoadState(data.EnvState ?? []);

            // ---- 2. 地图 ----
            if (!TryBuildMap(data.Map, env, out map))
            {
                return false;
            }

            // ---- 3. 玩家 ----
            List<Item> inventory = BuildItems(data.Player.Inventory, "背包");
            List<Item> accessories = BuildItems(data.Player.Accessories, "饰品栏");
            List<Buff> buffs = BuildBuffs(data.Player.Buffs);

            player.RestoreFromSave(
                data.Player.Name,
                data.Player.Hp,
                data.Player.Shield,
                data.Player.BaseHp,
                data.Player.BaseStr,
                data.Player.BaseAtk,
                data.Player.BaseDef,
                data.Player.InventoryCapacity,
                data.Player.AccessorySlots,
                data.Player.FightCount,
                data.Player.SkipTurn,
                data.Player.IsDead,
                inventory,
                accessories,
                buffs);

            // ---- 4. 配方 ----
            // 配方表本身不可序列化（里面是 Func<Item>），只存了「解锁到第几档」。
            // 顺序固定：先配方 0，再按档位补配方 1 —— 与开局时的解锁顺序一致。
            player.AddDefaultRecipes();
            if (data.Player.Tier1RecipesUnlocked)
            {
                player.AddTier1Recipes();
            }

            return true;
        }

        private static bool TryBuildMap(MapData data, Environment env, out Room[][] map)
        {
            map = null;

            if (data.Rows <= 0 || data.Cols <= 0)
            {
                Console.WriteLine("[存档] 地图尺寸无效，无法读取。");
                return false;
            }

            // 数量必须严丝合缝：少了说明文件被截断，多了说明格式对不上。
            // 地形错位的地图比「读档失败」更糟 —— 玩家会走进一堵不存在的墙。
            if (data.Rooms.Count != data.Rows * data.Cols)
            {
                Console.WriteLine($"[存档] 房间数量不匹配（应有 {data.Rows * data.Cols}，实有 {data.Rooms.Count}），无法读取。");
                return false;
            }

            Room[][] grid = new Room[data.Rows][];
            int index = 0;
            for (int r = 0; r < data.Rows; r++)
            {
                grid[r] = new Room[data.Cols];
                for (int c = 0; c < data.Cols; c++)
                {
                    RoomData rd = data.Rooms[index++];

                    // 房间类型按名字还原；认不出来就用 BlankRoom 兜底（它什么都不做，最安全）
                    if (!Enum.TryParse(rd.Type, out RoomType type))
                    {
                        Console.WriteLine($"[存档] 未知房间类型「{rd.Type}」，按空房间处理。");
                        type = RoomType.Blank;
                    }

                    Room room = RoomFactory.Create(type, env);
                    room.SetDoors(rd.Doors);
                    room.IsVisited = rd.Visited;
                    room.IsCleaned = rd.Cleaned;

                    // 起始房间的「开局道具已发放」必须恢复，否则读档后再回起点会再发一轮
                    if (room is StartRoom start)
                    {
                        start.RestoreStartItemsGiven(rd.StartItemsGiven);
                    }

                    grid[r][c] = room;
                }
            }

            map = grid;
            return true;
        }

        private static List<Item> BuildItems(List<ItemData> source, string where)
        {
            List<Item> result = [];
            foreach (ItemData d in source ?? [])
            {
                Item item = ItemFactory.Create(d.Id, d.Count);
                if (item == null)
                {
                    Console.WriteLine($"[存档] {where}里有未知道具 ID {d.Id}，已跳过。");
                    continue;
                }
                // 实例状态（耐久 / 已承伤次数 / 倒计时）—— 只有实现了 IItemState 的道具才有
                if (item is IItemState stateful && d.State != null)
                {
                    stateful.LoadState(d.State);
                }
                result.Add(item);
            }
            return result;
        }

        private static List<Buff> BuildBuffs(List<BuffData> source)
        {
            List<Buff> result = [];
            foreach (BuffData d in source ?? [])
            {
                Buff buff = BuffFactory.Create(d.Id, d.Stacks, d.Turns);
                if (buff == null)
                {
                    Console.WriteLine($"[存档] 未知 buff ID {d.Id}，已跳过。");
                    continue;
                }
                result.Add(buff);
            }
            return result;
        }
    }
}

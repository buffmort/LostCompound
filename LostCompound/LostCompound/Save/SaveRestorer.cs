using Environment = LostCompound.MapEnvironment.Environment;
using LostCompound.Buffs;
using LostCompound.Items;
using LostCompound.Items.Capabilities;
using LostCompound.MapEnvironment;
using LostCompound.Maps;
using LostCompound.Rooms;
using LostCompound.Rooms.Types;
using LostCompound.Tools;
using LostCompound.Tools.Players;

namespace LostCompound.Save
{
    /// <summary>
    /// 还原：**先用种子把地重算出来**，再把进度点上去，最后把玩家塞进去。
    ///
    /// 原则：**遇到坏数据就降级，不崩**。
    ///   未知道具 ID → 跳过那一件并提示（其余照常读）；
    ///   越界的格子编号 → 跳过并提示；
    ///   算法版本不符 → 整档拒绝（同一个种子在新算法下是另一张图）。
    /// </summary>
    public static class SaveRestorer
    {
        /// <summary>
        /// 把存档灌进 <paramref name="player"/>，并重建地图与生成器。
        /// 返回 false 表示存档不可用（调用方负责提示）。
        /// </summary>
        /// <param name="tmg">重建出来的生成器 —— 主循环画地图要用它的 <c>ToDisplayString()</c></param>
        public static bool TryRestore(SaveData data, Player player, out Room[][] map, out TreeMazeGenerator tmg)
        {
            map = null;
            tmg = null;

            if (data == null || data.Player == null)
            {
                Console.WriteLine("[存档] 存档内容不完整，无法读取。");
                return false;
            }

            // ---- 1. 算法版本必须先对 ----
            // 种子只有在「同一份建图算法」下才有意义。版本不符时算出来的是另一张图，
            // 静默读进来会让玩家站进墙里、或者让宝箱房凭空消失，而且全程不报错。
            if (data.GeneratorVersion != TreeMazeGenerator.GeneratorVersion)
            {
                Console.WriteLine(
                    $"[存档] 地图生成算法已更新（存档 {data.GeneratorVersion} / 当前 {TreeMazeGenerator.GeneratorVersion}），" +
                    "旧存档的地形无法还原，本次无法继续。");
                return false;
            }

            if (data.Rows <= 0 || data.Cols <= 0)
            {
                Console.WriteLine("[存档] 地图尺寸无效，无法读取。");
                return false;
            }

            if (!Enum.TryParse(data.EnvType, out EnvType envType))
            {
                envType = EnvType.None;   // 认不出来就当没有环境，而不是随便挑一个
            }

            // ---- 2. 用种子重建地形 ----
            // 注意：Generate() 必须和「新开局」走同一条路径，否则读档和开局会算出两张图。
            // 层数用存档里记的，并且**先写回 GameProgress 再建图** —— 顺序反了的话
            // 建出来的图会记着旧的层数（快照），之后刷怪就按错层算血量。
            GameProgress.Restore(data.Layer);
            tmg = new TreeMazeGenerator(data.Rows, data.Cols, envType, data.Seed, GameProgress.Layer);
            tmg.Generate();
            map = tmg.Grid;

            // 环境对象由建图过程内部创建（同一层所有房间共享一个），这里把状态灌回去
            Environment env = map[0][0].Environment;
            env?.LoadState(data.EnvState ?? []);

            // ---- 3. 点亮进度 ----
            ApplyProgress(data, map);

            // ---- 4. 玩家 ----
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

            // ---- 5. 配方 ----
            // 配方表不可序列化（里面是 Func<Item>），只存了「解锁到第几档」，
            // 这里按档位重跑一次，与开局时的解锁顺序一致。
            player.AddDefaultRecipes();
            if (data.Player.Tier1RecipesUnlocked)
            {
                player.AddTier1Recipes();
            }

            return true;
        }

        /// <summary>
        /// 把「走过 / 搜过 / 开局道具已发」点回地图上。
        ///
        /// 编号是压平的（`row * Cols + col`），越界说明存档坏了 —— 跳过并提示，
        /// 因为一格越界不该让整局读不进来。
        /// </summary>
        private static void ApplyProgress(SaveData data, Room[][] map)
        {
            int cols = map.Length > 0 ? map[0].Length : 0;
            int total = map.Length * cols;
            int skipped = 0;

            foreach (int index in data.Visited ?? [])
            {
                if (index < 0 || index >= total) { skipped++; continue; }
                map[index / cols][index % cols].IsVisited = true;
            }
            foreach (int index in data.Cleaned ?? [])
            {
                if (index < 0 || index >= total) { skipped++; continue; }
                map[index / cols][index % cols].IsCleaned = true;
            }

            // 起始房间的「开局道具已发放」：不恢复的话，读档后再回起点会再发一轮三选一
            if (data.StartItemsGiven)
            {
                foreach (Room[] row in map)
                {
                    foreach (Room room in row)
                    {
                        if (room is StartRoom start)
                        {
                            start.RestoreStartItemsGiven(true);
                        }
                    }
                }
            }

            if (skipped > 0)
            {
                Console.WriteLine($"[存档] 有 {skipped} 个格子编号超出地图范围，已忽略。");
            }
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
                // 实例级标记：BuffFactory 只会传 Id/Stacks/Turns，这里把"是否跨战斗"补回去
                buff.RestoreSurvivesBattleEnd(d.SurvivesBattleEnd);
                result.Add(buff);
            }
            return result;
        }
    }
}

namespace LostCompound.Save
{
    /// <summary>
    /// 存档的**纯数据**形态（DTO = Data Transfer Object）。
    ///
    /// 这一层刻意「傻」：只有属性、没有方法、没有 Func、不读控制台、不认识任何游戏类。
    /// 原因：
    ///   · 能被 JSON 序列化的只有**公开属性 + 基本类型**（int / double / string / List / 字典）；
    ///     带行为的对象（`Recipe` 里是 `Func<Item>`、`Room` 里有环境引用）根本写不进文件。
    ///   · 存档格式一旦发布就要兼容旧档，所以格式与游戏内部结构解耦，改代码不用改格式。
    ///
    /// 命名上 `XxxData` 与游戏类一一对应，但**不是**同一个东西：
    /// `PlayerData` 是「玩家状态的一份快照」，`Player` 是「会跑的游戏对象」。
    /// </summary>
    public sealed class SaveData
    {
        /// <summary>格式版本。**只增不改语义**：加字段 = 升版本，读旧档走兼容分支。</summary>
        public int Version { get; set; }

        /// <summary>存档时间（ISO 8601 字符串）。只用于显示「上次存档：…」。</summary>
        public string SavedAt { get; set; } = "";

        /// <summary>
        /// 层数。层数系统还没做，目前恒为 1 —— 先占位，
        /// 等「一局多张地图」实现后这里就是「第几层」，不用改存档格式。
        /// </summary>
        public int Layer { get; set; } = 1;

        /// <summary>
        /// 环境类型，**存枚举名而不是数字**（如 "LavaWaterfall"）。
        /// 存数字的话，将来有人在 `EnvType` 中间插一个成员，旧档就会整体错位 ——
        /// 而且不报错，只是「存档里的水淹洞穴读出来变成熔岩瀑布」。
        /// </summary>
        public string EnvType { get; set; } = "";

        /// <summary>环境自己的状态（水量 / 水位 / 堵住回合），见 <see cref="MapEnvironment.Environment.SaveState"/>。</summary>
        public Dictionary<string, int> EnvState { get; set; } = [];

        public PlayerData Player { get; set; } = new();

        public MapData Map { get; set; } = new();
    }

    /// <summary>玩家状态快照。</summary>
    public sealed class PlayerData
    {
        /// <summary>
        /// ⚠️ 这里**没有** Str / Atk / Def。
        /// 它们是派生值，读档后调一次 `RecomputeStats()` 就会从「基础值 + 环境 + buff + 装备」算回来。
        /// 存了反而危险：万一存档里的 Str 和来源不一致，玩家会带着一个「来源解释不了的数值」继续玩，
        /// 而下一次重算又会把它冲掉 —— 表现为「读档后力量莫名其妙变了一下」。
        /// </summary>
        public string Name { get; set; } = "";

        public double Hp { get; set; }
        public double Shield { get; set; }

        public double BaseHp { get; set; }
        public double BaseStr { get; set; }
        public double BaseAtk { get; set; }
        public double BaseDef { get; set; }

        public int InventoryCapacity { get; set; }
        public int AccessorySlots { get; set; }
        public int FightCount { get; set; }
        public int SkipTurn { get; set; }
        public bool IsDead { get; set; }

        /// <summary>玩家坐标。读档要站回原位，而不是回到起始房间。</summary>
        public int Row { get; set; }
        public int Col { get; set; }

        /// <summary>
        /// 配方解锁到第几档（0 = 只有配方 0；1 = 也解锁了配方 1）。
        /// 不存 `Recipes` 本身 —— 那是 `List<ICraftable>`，里面是 `Func<Item>`，不可序列化。
        /// 读档时按这个档位重新调 `AddDefaultRecipes()` / `AddTier1Recipes()`。
        /// </summary>
        public bool Tier1RecipesUnlocked { get; set; }

        public List<ItemData> Inventory { get; set; } = [];
        public List<ItemData> Accessories { get; set; } = [];
        public List<BuffData> Buffs { get; set; } = [];
    }

    /// <summary>一件道具：ID + 数量 + 实例状态。</summary>
    public sealed class ItemData
    {
        public int Id { get; set; }
        public int Count { get; set; }

        /// <summary>
        /// 实例状态（耐久 / 已承伤次数 / 倒计时）。
        /// 只有实现了 `IItemState` 的道具才有内容，其余道具这里是空字典。
        /// </summary>
        public Dictionary<string, int> State { get; set; } = [];
    }

    /// <summary>一个 buff：ID + 效果量 + 剩余回合（正好是 `BuffFactory.Create` 的三个参数）。</summary>
    public sealed class BuffData
    {
        public int Id { get; set; }
        public int Stacks { get; set; }
        public int Turns { get; set; }
    }

    /// <summary>地图快照。二维数组在 JSON 里表示起来很啰嗦，所以展平成一维 + 行列数。</summary>
    public sealed class MapData
    {
        public int Rows { get; set; }
        public int Cols { get; set; }

        /// <summary>按**行优先**展平：索引 = row * Cols + col。</summary>
        public List<RoomData> Rooms { get; set; } = [];
    }

    /// <summary>一个格子。</summary>
    public sealed class RoomData
    {
        /// <summary>房间类型，存名字（理由同 <see cref="SaveData.EnvType"/>）。</summary>
        public string Type { get; set; } = "";

        /// <summary>4 扇门的位掩码：上=1 下=2 左=4 右=8（见 <see cref="Rooms.Room.GetDoors"/>）。</summary>
        public int Doors { get; set; }

        public bool Visited { get; set; }
        public bool Cleaned { get; set; }

        /// <summary>
        /// 只有起始房间用它：「本层开局道具是否已发放」。
        /// 不存的话读档后再回起点会**再发一轮开局三选一**。
        /// 其它房间忽略这个字段。
        /// </summary>
        public bool StartItemsGiven { get; set; }
    }
}

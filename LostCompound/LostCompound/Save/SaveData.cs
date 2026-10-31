namespace LostCompound.Save
{
    /// <summary>
    /// 存档的**纯数据**形态（DTO = Data Transfer Object）。
    ///
    /// 这一层刻意「傻」：只有属性、没有方法、没有 Func、不读控制台、不认识任何游戏类。
    ///
    /// ─────────────────────────────────────────────────────────────
    /// 【设计核心】地图**不存**，只存「种子 + 进度」。
    ///
    /// 地形（房间类型、门的连通关系）完全由 `TreeMazeGenerator` 的种子决定：
    /// 种子 + 同一版本的算法 → 必然算出同一张图。既然算得出来，就没必要占存档。
    ///
    /// 但**进度算不出来** —— 「走过哪几格」「搜刮过哪几格」「开局道具发过没」
    /// 是玩家玩出来的，只能存。它们的体积很小：压平成「格子编号」的数组就是了。
    ///
    /// 于是存档从 15.9 KB 掉到 1 KB 左右，而且不会出现
    /// 「存档里的地形和种子算出来的地形不一致」这种鬼故事 —— 地形只有一个真相源。
    ///
    /// ⚠️ 代价：**改了建图算法就会让旧存档的图变形**。
    ///    所以存了 `GeneratorVersion`，不匹配就明确拒绝（见 SaveRestorer）。
    /// ─────────────────────────────────────────────────────────────
    /// </summary>
    public sealed class SaveData
    {
        /// <summary>格式版本。**加了/改了字段语义就 +1**，读旧档走兼容或明确拒绝。</summary>
        public int Version { get; set; }

        /// <summary>存档时间（`yyyy-MM-dd HH:mm:ss`）。只用于显示「上次存档：…」。</summary>
        public string SavedAt { get; set; } = "";

        /// <summary>
        /// 层数。现在是真值了（`GameProgress.Layer`），不再是占位。
        /// </summary>
        public int Layer { get; set; } = 1;

        /// <summary>
        /// 本局**总种子**（`GameProgress.RunSeed`）。每层的种子由它 + 层数派生
        /// （`GameProgress.SeedForLayer`），所以读档后继续往下打时必须能拿回它，
        /// 否则第 N+1 层的种子算不出来。
        ///
        /// 旧存档没有这个字段 → 读出来是 0，读档时退化成用 <see cref="Seed"/> 当总种子。
        /// </summary>
        public long RunSeed { get; set; }

        /// <summary>
        /// 环境类型，**存枚举名而不是数字**（如 "LavaWaterfall"）。
        /// 存数字的话，将来有人在 `EnvType` 中间插一个成员，旧档就会整体错位 ——
        /// 而且不报错，只是「存档里的水淹洞穴读出来变成熔岩瀑布」。
        /// </summary>
        public string EnvType { get; set; } = "";

        /// <summary>环境自己的状态（水量 / 水位 / 堵住回合），见 <see cref="MapEnvironment.Environment.SaveState"/>。</summary>
        public Dictionary<string, int> EnvState { get; set; } = [];

        // ==================== 地图重建参数 ====================

        /// <summary>
        /// 本局的地图种子。**这是地图的全部来源** —— 房间类型与门的连通关系都由它重算。
        /// </summary>
        public long Seed { get; set; }

        /// <summary>
        /// 建图算法的版本（`TreeMazeGenerator.GeneratorVersion`）。
        /// 与当前代码不一致时，读档会**拒绝**：同一个种子在新算法下是另一张图，
        /// 硬读进来会让玩家站进墙里或让宝箱房凭空消失，而且全程不报错。
        /// </summary>
        public int GeneratorVersion { get; set; }

        /// <summary>地图行数 / 列数。目前固定 10×10，但存下来才能诚实地重建。</summary>
        public int Rows { get; set; }
        public int Cols { get; set; }

        /// <summary>
        /// 已访问的格子。**压平成编号**：`下标 = row * Cols + col`。
        ///
        /// 为什么压平：存 `{"Row":3,"Col":7}` 这种对象，一格就是 30 多个字符；
        /// 存一个整数只要 1~3 个字符。100 格的图里这个差距就是十几 KB。
        /// 读取时用同一个公式还原，不需要额外约定。
        /// </summary>
        public List<int> Visited { get; set; } = [];

        /// <summary>已搜刮 / 事件已触发的格子（同样是压平编号）。</summary>
        public List<int> Cleaned { get; set; } = [];

        /// <summary>
        /// 起始房间的「本层开局道具是否已发放」。
        /// 每层只有一个起始房间，所以一个 bool 就够，不必塞进坐标数组。
        /// 不存的话读档后再回起点会**再发一轮开局三选一**。
        /// </summary>
        public bool StartItemsGiven { get; set; }

        public PlayerData Player { get; set; } = new();
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

        /// <summary>
        /// 镜子(45) 留下的朝向（"" / "左旋" / "右旋"）。**只属于这一局**，所以进 save 而不进 profile：
        /// 不存的话存档退出再读档，朝向会丢，下一次照镜子又变回随机，
        /// 而玩家眼前的名字（「左旋-张三」）却已经被存档按旧值还原过 —— 两者会对不上。
        /// </summary>
        public string MirrorSide { get; set; } = "";

        // ⚠️ 下面这些字段为什么**不升存档版本**也要有初值：
        //   SaveFile 采用「纯加字段不升版本」，旧存档缺这些字段时反序列化会保留 C# 属性初值。
        //   若初值是 0，读旧档就会得到 Hp=0（当场判定死亡）或容量 0（背包塞不进任何东西、
        //   读档时道具全丢）。所以初值必须与 `Player` 的默认值一致 —— 缺字段时退回到
        //   「一个刚出生的玩家」，而不是 0。改这些默认值时请同步 `Player` / `Player.Inventory` /
        //   `Player.Equipment` 里的字段初值。
        public double Hp { get; set; } = 200;
        public double Shield { get; set; }

        public double BaseHp { get; set; } = 200;
        public double BaseStr { get; set; } = 3.0;
        public double BaseAtk { get; set; } = 20;
        public double BaseDef { get; set; } = 5;

        public int InventoryCapacity { get; set; } = 15;
        public int AccessorySlots { get; set; } = 5;
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

        /// <summary>
        /// 是否**跨战斗保留**（<see cref="Buffs.Buff.SurvivesBattleEnd"/>）。
        /// 必须存：它是实例级的，`BuffFactory` 重建时只知道 Id/Stacks/Turns，默认 false。
        /// 不存的话，跨战斗 buff 读档后会变成战斗内临时 buff，下一场就没了。
        /// 旧存档没有这个字段 → 反序列化成 false，对现有 buff 全部正确（它们都不是跨战斗的）。
        /// </summary>
        public bool SurvivesBattleEnd { get; set; }

        /// <summary>
        /// buff 的「总回合数」（<see cref="Buffs.Buff.TotalTurns"/>）。
        ///
        /// 为什么必须存：缺氧的衰减量是 `-0.1 × (总回合 − 剩余回合)`，
        /// 而 <see cref="Buffs.BuffFactory"/> 重建时只能传「剩余回合」。若不把总回合存下来，
        /// 读档后总回合 = 当时剩余回合 → 已累计的衰减量归零（玩家靠存读档白赚属性）。
        ///
        /// 旧存档没有这个字段 → 反序列化成 0 = 「不知道」；读档时按当前剩余回合处理，
        /// 与加这个字段之前的行为一致（不是把总回合当成 0）。
        /// </summary>
        public int TotalTurns { get; set; }
    }
}

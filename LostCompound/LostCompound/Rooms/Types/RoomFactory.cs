using Environment = LostCompound.MapEnvironment.Environment;

namespace LostCompound.Rooms.Types
{
    /// <summary>
    /// 按 <see cref="RoomType"/> 造房间。
    ///
    /// 为什么需要它：房间的构造散在 `TreeMazeGenerator.InitializeRooms` 里
    /// （`new StartRoom(env)` / `new FightRoom(env)` …），**没有任何地方能「按类型造一个房间」**。
    /// 而读档必须能重建地图 —— 存档里存的是「这一格是 Fight、门朝上下」这种数据，
    /// 不是对象引用（引用没法写进文件）。
    ///
    /// 放在 `Rooms.Types` 而不是 `Rooms`：这样它只依赖同包的兄弟类，
    /// 不会让 `Rooms` 反过来依赖 `Rooms.Types`。
    ///
    /// 维护约定：`RoomType` 加成员时**必须**在这里补一个分支，
    /// 否则读档会把它变成 BlankRoom（兜底项），表现为「存档里的合成台读档后没了」。
    /// </summary>
    public static class RoomFactory
    {
        /// <summary>
        /// 造一个空房间实例（只有类型，没有任何进度）。
        /// 进度（门 / 访问过 / 搜过 / 玩家在不在）由调用方读档后逐项灌回去 ——
        /// 构造函数不接收这些参数是有意的：它们在游戏过程中本来就会变，
        /// 塞进构造器只会让「新开一局」也要传一堆 false。
        /// </summary>
        public static Room Create(RoomType type, Environment env) => type switch
        {
            RoomType.Start => new StartRoom(env),
            RoomType.Boss => new BossRoom(env),
            RoomType.Fight => new FightRoom(env),
            RoomType.Empty => new EmptyRoom(env),
            RoomType.Craft => new CraftRoom(env),
            RoomType.Treasure => new TreasureRoom(env),
            // Blank 与「枚举里没见过的值」都归到这里：BlankRoom 是唯一什么都不做的房间，
            // 拿它兜底不会让存档里的数据静默变成某种有事件的房间。
            _ => new BlankRoom(env),
        };
    }
}

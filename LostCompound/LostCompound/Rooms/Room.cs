using Environment = LostCompound.MapEnvironment.Environment;
using LostCompound.MapEnvironment;
using LostCompound.Tools;

using LostCompound.Tools.Players;

namespace LostCompound.Rooms
{
    public abstract class Room
    {
        public RoomType Type { get; }
        private readonly bool[] _doors = new bool[4]; // 0上 1下 2左 3右
        public bool IsVisited { get; set; }
        public bool IsCleaned { get; set; }
        public bool IsPlayerHere { get; set; }
        // 环境对象引用（可为 null：该环境尚未实现或本层没有环境）
        public Environment Environment { get; }

        /// <summary>
        /// 这间房是不是**高温房间**（Hyberty 2026-11-01 裁定：房间级开关，随机抽约 25% 的房间）。
        ///
        /// 改了这一个位，三件事一起变（它们都只看「当前房间热不热」）：
        ///   · 玩家水量消耗 +50%（<see cref="MapEnvironment.Environments.LavaWaterfall"/>）；
        ///   · 木头在这里打 3 场就烤成碳（<see cref="Items.Materials.Wood"/>）；
        ///   · 硫磺在这里打 3 场就烤成热硫（<see cref="Items.Materials.Sulphur"/>）。
        ///
        /// 为什么不是 <see cref="RoomType"/> 的新成员：高温是**横切**在房型上的属性
        /// （一间战斗房、一间空房都可以热），做成房型就得为「热的战斗房 / 不热的战斗房」
        /// 各开一个类型，房型会立刻翻倍。做成 bool 之后，生成器随机挑房间置位即可。
        ///
        /// ⚠️ 地图**不存**，靠种子重建，所以这个位必须由生成器**确定性地**掷出来
        /// （用注入的 `_rng`，不能用 `Random.Shared`），否则同一存档读两次高温房会换位置。
        /// ⚠️ 只有环境自己声明了 <see cref="MapEnvironment.Environment.HasHotRooms"/> 才会置位，
        ///    目前只有熔岩瀑布（设计文档 §5.1 把高温房间归给它）。
        /// </summary>
        public bool IsHot { get; set; }

        /// <summary>
        /// 这间房的**辐射间隔**（秒）：站在这里每多少秒被辐射扣 1 血。
        /// 0 = 这间房不辐射（非放射性区域的层永远是 0）。
        ///
        /// Hyberty 2026-11-22 裁定：不再用全层统一的 20 秒，改成**每间房一个值**，
        /// 由柏林噪声铺在地图上，范围 5~30 秒（保留小数）。
        /// 噪声是连续函数，所以相邻房间的数值接近 —— 玩家能感到「往某个方向越走越毒」，
        /// 而不是每间房各摇一个互不相关的随机数（那样地图读不出任何形状）。
        ///
        /// 为什么放在房间上而不是环境里：它和 <see cref="IsHot"/> 同一个性质 ——
        /// 「横切在房型上的属性」，且必须由生成器**确定性地**算出来（地图不存档、读档靠种子重建）。
        /// 环境只负责读它，不负责掷它。
        /// </summary>
        public double RadiationSeconds { get; set; }

        /// <summary>
        /// 这间房有没有**矿脉**（设计文档 §5.6：放射性区域「最少 3 个房间可采」）。
        /// 有矿脉的房间才能用镐子开采，挖一次就消耗掉（见 RadioactiveArea.ConsumeOreVein）。
        ///
        /// 为什么是房级标记而不是「全层共用一个可采次数」：
        ///   文档说的是**房间**可采，玩家得在地图上找出那几间房 —— 这是这条路线的探索目标。
        ///   做成「全层能挖 N 次」的话，玩家随便找个房间连挖 N 下就完事，找矿的过程没了。
        ///
        /// 与 <see cref="RadiationSeconds"/> 同一条纪律：必须由生成器**确定性地**掷出来
        /// （用注入的 `_rng`，不能用 `Random.Shared`），地图不存档、读档靠种子重建。
        /// </summary>
        public bool HasOreVein { get; set; }

        // 便捷属性：原来需要枚举的地方（掉落表、怪物生成、环境相关结算等）用它取环境类型
        public EnvType EnvKind => Environment?.Type ?? EnvType.None;

        public Room(RoomType type, Environment environment)
        {
            Type = type;
            Environment = environment;
        }

        // 子类实现具体的进入事件
        public abstract void OnEnter(Player p);

        public virtual void OnLeave(Player p)
        {
            IsPlayerHere = false;
            // 可以在这里清理或触发离开事件
        }

        /// <summary>
        /// 只做「玩家已到达」的标记（地图据此渲染 * 与已访问状态），<b>不</b>触发 OnEnter。
        /// 把「显示地图」与「房间事件」拆开，使输出顺序可以变成：地图 → 房间类型 → 事件。
        /// </summary>
        public void MarkArrived()
        {
            IsVisited = true;
            IsPlayerHere = true;
        }

        public void LeaveSetup(Player p)
        {
            IsPlayerHere = false;
            OnLeave(p);
        }

        // 检查某个方向是否有门
        public bool HasDoor(Direction direction)
        {
            int index = (int)direction;
            if (index >= 0 && index < _doors.Length)
            {
                return _doors[index];
            }
            return false;
        }

        // 设置门状态
        public void SetDoor(Direction direction, bool hasDoor)
        {
            int index = (int)direction;
            if (index >= 0 && index < _doors.Length)
            {
                _doors[index] = hasDoor;
            }
        }
    }
}

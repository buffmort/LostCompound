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

        /// <summary>
        /// 把 4 扇门压成一个 4 位掩码：上=1，下=2，左=4，右=8。
        ///
        /// 为什么要有它：`_doors` 是私有的、又只有 <see cref="SetDoor"/> 一个写入口，
        /// 存档时**读不出来**。存档要的是「这一格的地形」，掩码比 4 个 bool 好存也好比对。
        /// </summary>
        public int GetDoors()
        {
            int mask = 0;
            for (int i = 0; i < _doors.Length; i++)
            {
                if (_doors[i])
                {
                    mask |= 1 << i;   // 位号 == (int)Direction，与 _doors 的索引约定一致
                }
            }
            return mask;
        }

        /// <summary>读档专用：按掩码恢复 4 扇门（与 <see cref="GetDoors"/> 互为逆操作）。</summary>
        public void SetDoors(int mask)
        {
            for (int i = 0; i < _doors.Length; i++)
            {
                _doors[i] = (mask & (1 << i)) != 0;
            }
        }
    }
}

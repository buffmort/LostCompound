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
    }
}

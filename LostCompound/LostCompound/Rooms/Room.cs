using LostCompound.MapEnvironment;
using LostCompound.Tools;

namespace LostCompound.Rooms
{
    public abstract class Room
    {
        public RoomType Type { get; }
        private readonly bool[] _doors = new bool[4]; // 0上 1下 2左 3右
        public bool IsVisited { get; set; }
        public bool IsCleaned { get; set; }
        public bool IsPlayerHere { get; set; }
        public EnvType Environment { get; }

        public Room(RoomType type, EnvType environment)
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

        // 统一进入流程：先标记已访问，再触发事件
        public void EnterSetup(Player p)
        {
            IsVisited = true;
            IsPlayerHere = true;
            OnEnter(p);
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

        // 检查是否还有空闲门（供生成器使用，保留）
        public bool HasFreeDoor()
        {
            foreach (bool d in _doors)
            {
                if (!d) return true;
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

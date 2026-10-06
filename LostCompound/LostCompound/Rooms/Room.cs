using LostCompound.MapEnvironment;
using LostCompound.Tools;

namespace LostCompound.Rooms
{
    public abstract class Room
    {
        public RoomType type;
        public bool[] doors = new bool[4]; // 0上 1下 2左 3右
        public bool visited = false;
        public bool cleaned = false;
        private bool playerOnHere = false;
        public const int UP = 0;
        public const int DOWN = 1;
        public const int LEFT = 2;
        public const int RIGHT = 3;
        public EnvType EnvType;

        public Room(RoomType type, EnvType type1)
        {
            this.type = type;
            this.EnvType = type1;
        }

        // 子类实现具体的进入事件
        public abstract void onEnter(Player p);
        public virtual void onLeave(Player p)
        {
            playerOnHere = false;
            // 可以在这里清理或触发离开事件
        }
        public bool isPlayerOnHere()
        {
            return playerOnHere;
        }
        public void setPlayerOnHere(bool b)
        {
            this.playerOnHere = b;
        }
        // 统一进入流程：先标记已访问，再触发事件
        public void enterSetup(Player p)
        {
            visited = true;
            playerOnHere = true;
            onEnter(p);
        }
        public void leaveSetup(Player p)
        {
            playerOnHere = false;
            onLeave(p);
        }

        // 检查某个方向是否有门
        public bool isDoor(int direction)
        {
            if (direction >= 0 && direction < doors.Length)
            {
                return doors[direction];
            }
            return false;
        }

        // 检查是否还有空闲门（供生成器使用，保留）
        public bool hasFreeDoor()
        {
            foreach (bool d in doors)
            {
                if (!d) return true;
            }
            return false;
        }

        // 设置门状态
        public void setDoor(int direction, bool hasDoor)
        {
            if (direction >= 0 && direction < doors.Length)
            {
                doors[direction] = hasDoor;
            }
        }

        public RoomType getRoomType()
        {
            return type;
        }
        public bool isVisited()
        {
            return visited;
        }
        public void setVisited(bool visited)
        {
            this.visited = visited;
        }
    }
}

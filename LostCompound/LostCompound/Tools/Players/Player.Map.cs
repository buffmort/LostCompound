using Environment = LostCompound.MapEnvironment.Environment;
using LostCompound.MapEnvironment;
using LostCompound.Rooms;
using LostCompound.Items;
using LostCompound.Monsters;
using LostCompound.Maps;
using LostCompound.UI;
using LostCompound.Buffs;
using LostCompound.Items.Capabilities;
using LostCompound.Items.Materials;
using LostCompound.Items.Consumables;
using LostCompound.Items.Containers;
using LostCompound.Items.Equipment;
using LostCompound.Rooms.Types;
using LostCompound.Tools.Debug;
using LostCompound.Buffs.Types;

namespace LostCompound.Tools.Players
{
    public partial class Player
    {
        private Room[][] _map;      // 当前地图
        public int PlayerRow { get; private set; }     // 玩家所在行
        public int PlayerCol { get; private set; }     // 玩家所在列

        /// <summary>
        /// 玩家当前所在房间的环境对象（可能为 null：没进地图、或该层没有环境）。
        /// 用 internal 而不是 public：只给同一程序集内的道具（水瓶）读取，不扩大对外公开 API。
        /// </summary>
        internal Environment CurrentEnvironment
        {
            get
            {
                if (_map == null) return null;
                Room room = _map[PlayerRow][PlayerCol];
                return room?.Environment;
            }
        }

        /// <summary>
        /// 玩家当前所在的房间（可能为 null：还没进地图 / 坐标失效）。
        /// 为什么要它：**高温**是挂在房间上的（<see cref="Room.IsHot"/>），而环境对象整层共享、
        /// 不知道玩家站在哪 —— 木头的倒计时、硫磺的倒计时、水量消耗都得问「当前这一间热不热」。
        /// 与 <see cref="CurrentEnvironment"/> 同一套：internal，只给同程序集的道具 / 战斗循环用。
        /// </summary>
        internal Room CurrentRoom => _map == null ? null : _map[PlayerRow][PlayerCol];

        /// <summary>
        /// 从当前房间朝 <paramref name="dir"/> 走一步，能不能真的走过去。
        ///
        /// 三个条件缺一不可：方向有门、目标格在界内、**目标格是一间真房间**。
        /// 第三条最容易被漏掉：`Room` 数组里每格都非 null（空格子是 <see cref="BlankRoom"/>，
        /// 只是没有门、走不进去），所以「邻格 != null」证明不了任何事 ——
        /// 只判前两条的话，一扇指向空格子的门会把玩家放进一片没有出口的空地，永久困死。
        ///
        /// 菜单显示（<see cref="ShowAvailableDirections"/>）与移动（<see cref="Move"/>）
        /// 必须共用这一个判据：两边各写一份，早晚出现「菜单里没有这个方向，但输入之后真走过去了」。
        /// </summary>
        private bool CanStep(Direction dir)
        {
            if (_map == null) return false;
            Room here = _map[PlayerRow][PlayerCol];
            if (here == null || !here.HasDoor(dir)) return false;

            int nr = PlayerRow + DRows[(int)dir];
            int nc = PlayerCol + DCols[(int)dir];
            if (nr < 0 || nr >= _map.Length || nc < 0 || nc >= _map[0].Length) return false;

            Room next = _map[nr][nc];
            return next != null && next.Type != RoomType.Blank;
        }

        /// <summary>方向对应的行列偏移，按 (int)Direction 索引：0 上、1 下、2 左、3 右。</summary>
        private static readonly int[] DRows = [-1, 1, 0, 0];
        private static readonly int[] DCols = [0, 0, -1, 1];

        public void ShowAvailableDirections()
        { //检查可以走的方向
            Console.WriteLine("可行动方向：");
            if (_map == null) return;

            // 判据统一走 CanStep —— 只列**真能走过去**的方向。
            if (CanStep(Direction.Up)) Console.WriteLine("0.上");
            if (CanStep(Direction.Down)) Console.WriteLine("1.下");
            if (CanStep(Direction.Left)) Console.WriteLine("2.左");
            if (CanStep(Direction.Right)) Console.WriteLine("3.右");
        }

        public bool Move(Direction direction)
        {
            if (!Enum.IsDefined(direction))
            {
                Console.WriteLine("无效方向！");
                return false;
            }
            Room current = _map[PlayerRow][PlayerCol];
            if (!current.HasDoor(direction))
            {
                Console.WriteLine("这个方向没有门！");
                return false;
            }

            int newRow = PlayerRow, newCol = PlayerCol;
            switch (direction)
            {
                case Direction.Up: newRow--; break; // 上
                case Direction.Down: newRow++; break; // 下
                case Direction.Left: newCol--; break; // 左
                case Direction.Right: newCol++; break; // 右
            }

            // 边界检查
            if (newRow < 0 || newRow >= _map.Length || newCol < 0 || newCol >= _map[0].Length)
            {
                Console.WriteLine("撞墙了！");
                return false;
            }

            // 离开旧房间（清除玩家标记，触发 OnLeave）
            current.LeaveSetup(this);

            // 更新坐标
            PlayerRow = newRow;
            PlayerCol = newCol;

            // 只标记「玩家已到达」，不在这里触发 OnEnter。
            // 事件改由 StartGame 在打印地图之后调用，输出顺序变为：地图 → 房间类型 → 房间事件。
            _map[PlayerRow][PlayerCol].MarkArrived();

            return true;
        }
        // ========== 加入地图（自动寻找 StartRoom） ==========
        public void JoinMap(Room[][] map)
        {
            _map = map;
            for (int y = 0; y < map.Length; y++)
            {
                for (int x = 0; x < map[0].Length; x++)
                {
                    if (map[y][x] is StartRoom)
                    {
                        PlayerRow = y;
                        PlayerCol = x;
                        // 只标记到达；起始房间的事件由 StartGame 在打印第一张地图后触发
                        map[y][x].MarkArrived();
                        return;
                    }
                }
            }
            throw new InvalidOperationException("地图中没有起始房间！");
        }

        /// <summary>
        /// 读档专用重载：把玩家放进**指定格子**，而不是去找起始房间。
        ///
        /// 为什么不能复用「先 JoinMap(找起点) 再改坐标」：
        ///   那样起始房间会被 <see cref="Room.MarkArrived"/> 标成「玩家在此」，
        ///   玩家又站到别处，地图上就会同时出现两个 `*`。
        ///   这里直接落在目标格，标记一次到位。
        ///
        /// 为什么不能复用 <see cref="Move"/>：Move 要求「相邻 + 有门」，还会触发
        ///   `LeaveSetup`（离开事件的副作用）—— 读档不是「走了一步」，是「本来就在这里」。
        ///
        /// 坐标由调用方按地图尺寸夹过（<see cref="GameLobby"/> 的 ContinueRun）。
        /// </summary>
        public void JoinMap(Room[][] map, int row, int col)
        {
            _map = map;
            PlayerRow = Math.Clamp(row, 0, map.Length - 1);
            PlayerCol = Math.Clamp(col, 0, map[0].Length - 1);
            map[PlayerRow][PlayerCol].MarkArrived();
        }
    }
}

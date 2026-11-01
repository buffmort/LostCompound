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

        public void ShowAvailableDirections()
        { //检查可以走的方向
            Console.WriteLine("可行动方向：");
            // 上
            if (PlayerRow - 1 >= 0 &&
                    _map[PlayerRow][PlayerCol].HasDoor(Direction.Up) &&
                    _map[PlayerRow - 1][PlayerCol] != null)
            {
                Console.WriteLine("0.上");
            }
            // 下
            if (PlayerRow + 1 < _map.Length &&
                    _map[PlayerRow][PlayerCol].HasDoor(Direction.Down) &&
                    _map[PlayerRow + 1][PlayerCol] != null)
            {
                Console.WriteLine("1.下");
            }
            // 左
            if (PlayerCol - 1 >= 0 &&
                    _map[PlayerRow][PlayerCol].HasDoor(Direction.Left) &&
                    _map[PlayerRow][PlayerCol - 1] != null)
            {
                Console.WriteLine("2.左");
            }
            // 右
            if (PlayerCol + 1 < _map[0].Length &&
                    _map[PlayerRow][PlayerCol].HasDoor(Direction.Right) &&
                    _map[PlayerRow][PlayerCol + 1] != null)
            {
                Console.WriteLine("3.右");
            }
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

            // 稀疏地图里房间之间隔着一格**走廊**（IsaacMazeGenerator 的 CorridorRoom）。
            // 玩家不该停在走廊里 —— 走廊不画在地图上，`*` 会凭空消失，而且「当前房间」会变成空。
            // 所以碰到走廊就顺势再走一格：**一次移动 = 从一间房直接到下一间房**。
            Room firstStep = _map[newRow][newCol];
            if (firstStep is not null && firstStep.Type is null)
            {
                if (!firstStep.HasDoor(direction))
                {
                    Console.WriteLine("前面的过道走不通。");
                    return false;
                }
                int farRow = newRow + direction switch
                {
                    Direction.Up => -1,
                    Direction.Down => 1,
                    _ => 0,
                };
                int farCol = newCol + direction switch
                {
                    Direction.Left => -1,
                    Direction.Right => 1,
                    _ => 0,
                };
                if (farRow < 0 || farRow >= _map.Length || farCol < 0 || farCol >= _map[0].Length)
                {
                    Console.WriteLine("撞墙了！");
                    return false;
                }
                Room far = _map[farRow][farCol];
                if (far is null || far.Type is null || far.Type == RoomType.Blank)
                {
                    Console.WriteLine("过道那头没有房间。");
                    return false;
                }
                newRow = farRow;
                newCol = farCol;
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

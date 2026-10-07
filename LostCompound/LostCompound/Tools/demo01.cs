using LostCompound.MapEnvironment;
using LostCompound.Maps;
using LostCompound.Rooms;

namespace LostCompound.Tools
{
    /// <summary>
    /// 调试入口：生成一张 11×13 的熔岩瀑布地图并直接进入探索。
    /// 注意：本类只被手动调用（改 Main 里的调用方），主流程走 GameLobby，不经过这里。
    /// </summary>
    public class demo01
    {
        public static void Run(string[] args)
        {
            TreeMazeGenerator generator = new TreeMazeGenerator(11, 13, EnvType.LavaWaterfall);
            generator.Generate();
            Room[][] map = generator.Grid;
            generator.PrintDebugMap(); // 先在日志里打一张 ASCII 地图，便于对照
            Player player = new Player();
            player.StartGame(map, generator); // 自动寻找 StartRoom
        }
    }
}

using LostCompound.MapEnvironment;
using LostCompound.Maps;
using LostCompound.Rooms;

namespace LostCompound.Tools
{
    public class demo01
    {
        public static void Run(string[] args)
        {
            TreeMazeGenerator generator = new TreeMazeGenerator(11, 13, EnvType.LavaWaterfall);
            generator.Generate();
            Room[][] map = generator.Grid;
            generator.PrintDebugMap(); // 打印地图（可选）
            Player player = new Player();
            player.StartGame(map, generator); // 自动寻找 StartRoom
        }
    }
}

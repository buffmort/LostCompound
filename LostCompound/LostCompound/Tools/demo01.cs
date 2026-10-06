using LostCompound.MapEnvironment;
using LostCompound.Maps;
using LostCompound.Rooms;
using LostCompound.Items;
using LostCompound.Monsters;

namespace LostCompound.Tools
{
    public class demo01
    {
        public static void Run(string[] args)
        {
            TreeMazeGenerator generator = new TreeMazeGenerator(11, 13, EnvType.LAVA_WATERFALL);
            generator.generate();
            Room[][] map = generator.getGrid();
            generator.DebugMap(); // 打印地图（可选）
            Player player = new Player();
            player.startGame(map, generator); // 自动寻找 StartRoom
        }
    }
}

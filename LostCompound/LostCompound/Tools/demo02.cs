using LostCompound.MapEnvironment;
using LostCompound.Monsters;

namespace LostCompound.Tools
{
    /// <summary>
    /// 调试入口：不打地图，直接对作孽双子跑一场战斗。
    /// 注意：本类只被手动调用（改 Main 里的调用方），主流程走 GameLobby，不经过这里。
    /// </summary>
    public class demo02
    {
        public static void Run(string[] args)
        {
            Player player = new Player();
            // 1. 创建共享状态（记录哥俩本回合是否攻击）
            TwistedPairState state = new TwistedPairState();
            // 2. 创建 P1
            TwistedPairP1 p1 = new TwistedPairP1(state);
            // 3. 创建 P2
            TwistedPairP2 p2 = new TwistedPairP2(state, p1);
            List<Monster> monsters = [p1, p2];
            // 需要起始材料时在这里补 AddItem，例如：player.AddItem(new Wood(5));
            player.AddDefaultRecipes();
            Tools.RunBattle(player, monsters, EnvironmentFactory.Create(EnvType.LavaWaterfall));
        }

    }
}

using LostCompound.MapEnvironment;
using LostCompound.Monsters;

namespace LostCompound.Tools
{
    public class demo02
    {
        public static void Run(string[] args)
        {
            Player player = new Player();
            //// 1. 创建共享状态（记录哥们俩本回合是否攻击）
            TwistedPairState state = new TwistedPairState();
            //// 2. 创建 P1
            TwistedPairP1 p1 = new TwistedPairP1(state);
            //// 3. 创建 P2
            TwistedPairP2 p2 = new TwistedPairP2(state, p1);
            List<Monster> monsters = [p1, p2];
            //        player.addItem(new Wood(5));
            //        player.addItem(new Charcoal(5));
            //        player.addItem(new WhitePhosphorus(5));
            //        player.addItem(new Sulphur(5));
            //        player.addItem(new Sand(5));
            player.AddDefaultRecipes();
            Tools.RunBattle(player, monsters, EnvironmentFactory.Create(EnvType.LavaWaterfall));
        }

    }
}

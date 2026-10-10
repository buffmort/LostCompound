using LostCompound.Items;
using LostCompound.MapEnvironment;
using LostCompound.Monsters;

namespace LostCompound.Tools
{
    /// <summary>
    /// 调试入口：不打地图，直接跑一场战斗。
    ///
    /// ⚠️ 注意本入口**没有地图**：玩家不在任何房间里，所以
    /// Player.CurrentEnvironment 恒为 null —— 「依赖环境」的道具（硫酸、镐子、水瓶的
    /// 环境分支）在这里测不出真实行为，会走「不适用」分支。
    /// 要测那些道具，请用主流程（GameLauncher.Main → GameLobby）真的走到对应环境里。
    ///
    /// 恢复为本类的 Main（正式入口是 GameLauncher.Main）：
    /// 之前为了在 Rider 里单跑它，曾把 GameLauncher.Main 改名成 Run、把这里改名成 Main，
    /// 那会让打出来的 exe 直接进战斗、没有 banner 与主菜单。
    /// </summary>
    public class demo02
    {
        public static void Run(string[] args)
        {
            Player player = new Player();
            // 1. 创建共享状态（记录哥俩本回合是否攻击）
            Dummy dummy = new Dummy();
            List<Monster> monsters = [dummy];
            // 需要起始材料时在这里补 AddItem，例如：player.AddItem(new Wood(5));
            player.AddDefaultRecipes();
            player.AddItem(new PhosphorusSword(1));
            player.AddItem(new SulfuricAcid(2));
            Tools.RunBattle(player, monsters, new FloodedCavern());
        }
    }
}

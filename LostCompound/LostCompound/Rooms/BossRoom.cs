using LostCompound.MapEnvironment;
using LostCompound.Monsters;
using LostCompound.Tools;

namespace LostCompound.Rooms
{
    public class BossRoom : Room
    {
        public BossRoom(EnvType envType) : base(RoomType.BOSS, envType)
        {
        }

        public override void onEnter(Player p)
        {
            if (!cleaned)
            {
                Console.WriteLine("Boss出现了!");
                List<Monster> monsters = new List<Monster>();
                Dragon d = new Dragon();
                monsters.Add(d);
                // 注意：类 Tools 与命名空间 LostCompound.Tools 同名，简单名 Tools 会被解析成
                // 命名空间，因此这里必须写全限定名 LostCompound.Tools.Tools。
                LostCompound.Tools.Tools.Fight(p, monsters, this.EnvType);
                this.cleaned = true;
                Console.WriteLine("感谢游玩!");
                Console.WriteLine("现在执行退出方法!");
                Environment.Exit(114514);
            }
            else
            {
                Console.WriteLine("你回到了boss房间");
            }
        }

        public override void onLeave(Player p)
        {
        }
    }
}

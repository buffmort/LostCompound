using Environment = LostCompound.MapEnvironment.Environment;
using LostCompound.MapEnvironment;
using LostCompound.Monsters;
using LostCompound.Tools;

namespace LostCompound.Rooms
{
    public sealed class BossRoom : Room
    {
        public BossRoom(Environment environment) : base(RoomType.Boss, environment)
        {
        }

        public override void OnEnter(Player p)
        {
            if (!IsCleaned)
            {
                Console.WriteLine("Boss出现了!");
                List<Monster> monsters = new();
                Dragon d = new();
                monsters.Add(d);
                // 注意：类 Tools 与命名空间 LostCompound.Tools 同名，简单名 Tools 会被解析成
                // 命名空间，因此这里必须写全限定名 LostCompound.Tools.Tools。
                LostCompound.Tools.Tools.RunBattle(p, monsters, Environment);
                IsCleaned = true;
                Console.WriteLine("感谢游玩!");
                Console.WriteLine("现在执行退出方法!");
                // 注意：基类 Room 新增了 Environment 属性，简单名 Environment 会被解析成该属性，
                // 因此这里必须写全限定名 System.Environment（退出码与原实现一致）。
                System.Environment.Exit(114514);
            }
            else
            {
                Console.WriteLine("你回到了boss房间");
            }
        }

        public override void OnLeave(Player p)
        {
        }
    }
}

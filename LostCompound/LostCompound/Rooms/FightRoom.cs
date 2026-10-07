using Environment = LostCompound.MapEnvironment.Environment;
using LostCompound.Items;
using LostCompound.MapEnvironment;
using LostCompound.Monsters;
using LostCompound.Tools;

namespace LostCompound.Rooms
{
    public sealed class FightRoom : Room
    {
        public FightRoom(Environment environment) : base(RoomType.Fight, environment)
        {
        }

        public override void OnEnter(Player p)
        {
            if (!IsCleaned)
            {
                Console.WriteLine("敌人出现了!");
                // 注意：类 Tools 与命名空间 LostCompound.Tools 同名，简单名 Tools 会被解析成
                // 命名空间，因此这里必须写全限定名 LostCompound.Tools.Tools。
                LostCompound.Tools.Tools.RunBattle(p, MobSpawner.Spawn(EnvKind), Environment);

                // 战斗结束后的环境额外掉落（如熔岩瀑布必掉硫磺）
                if (Environment != null)
                {
                    foreach (Item it in Environment.GetBattleLoot())
                    {
                        p.AddItem(it);
                    }
                }

                IsCleaned = true;
            }
            else
            {
                Console.WriteLine("这是一个战斗过后的遗迹");
            }
        }
    }
}

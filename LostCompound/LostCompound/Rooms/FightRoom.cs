using LostCompound.MapEnvironment;
using LostCompound.Monsters;
using LostCompound.Tools;

namespace LostCompound.Rooms
{
    public sealed class FightRoom : Room
    {
        public FightRoom(EnvType envType) : base(RoomType.Fight, envType)
        {
        }

        public override void OnEnter(Player p)
        {
            if (!IsCleaned)
            {
                Console.WriteLine("敌人出现了!");
                // 注意：类 Tools 与命名空间 LostCompound.Tools 同名，简单名 Tools 会被解析成
                // 命名空间，因此这里必须写全限定名 LostCompound.Tools.Tools。
                // 原先另有一个私有 envType 字段，与基类 Environment 属性完全重复，已删除。
                LostCompound.Tools.Tools.RunBattle(p, MobSpawner.Spawn(Environment), Environment);
                IsCleaned = true;
            }
            else
            {
                Console.WriteLine("这是一个战斗过后的遗迹");
            }
        }
    }
}

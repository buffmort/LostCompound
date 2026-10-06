using LostCompound.MapEnvironment;
using LostCompound.Monsters;
using LostCompound.Tools;

namespace LostCompound.Rooms
{
    public class FightRoom : Room
    {
        private EnvType envType;   // 新增成员变量

        public FightRoom(EnvType envType) : base(RoomType.FIGHT, envType)
        {
            this.envType = envType; // 保存参数到成员变量
        }

        public override void onEnter(Player p)
        {
            if (!cleaned)
            {
                Console.WriteLine("敌人出现了!");
                // 注意：类 Tools 与命名空间 LostCompound.Tools 同名，简单名 Tools 会被解析成
                // 命名空间，因此这里必须写全限定名 LostCompound.Tools.Tools。
                LostCompound.Tools.Tools.Fight(p, MobSpawner.spawn(envType), this.envType);
                this.cleaned = true;
            }
            else
            {
                Console.WriteLine("这是一个战斗过后的遗迹");
            }
        }
    }
}

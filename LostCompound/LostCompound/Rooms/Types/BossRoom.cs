using Environment = LostCompound.MapEnvironment.Environment;
using LostCompound.MapEnvironment;
using LostCompound.Monsters;
using LostCompound.Tools;
using LostCompound.Monsters.Boss;
using LostCompound.Rooms;

using LostCompound.Tools.Players;

namespace LostCompound.Rooms.Types
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
                // 层数硬编码为 1：与 FightRoom 同一口径（层数系统还没做，BossRoom 现在是 Exit 占位）。
                const int CurrentLayer = 1;

                // Boss 战也吃「战斗计数 → 多出怪」这套规则（概率 ×0.5，用户裁定）。
                // 组合方式是 **[龙] + 额外普通小怪**，不是「龙本身可能有多只」：
                //   · Boss 是剧情/数值上的唯一单位（MonsterRank.Boss、毒气弹的「非 Boss」判定、
                //     将来的掉落与通关奖励都按「一只 Boss」记账），复制多只龙会直接改变 Boss 战数值；
                //   · 用户要的是「怪变多」带来的场面压力，额外的小怪完全能达到这个效果；
                //   · SpawnExtras 已把额外小怪夹到 MaxMonsters - 1 = 6，加上龙总共不超过 7 行。
                List<Monster> monsters = new() { new Dragon() };
                monsters.AddRange(MobSpawner.SpawnExtras(EnvKind, CurrentLayer, p.FightCount));

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

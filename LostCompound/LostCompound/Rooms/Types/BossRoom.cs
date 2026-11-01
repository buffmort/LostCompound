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
                // 层数与 FightRoom 同一口径：读全局进度，本房间只读不写。
                int currentLayer = GameProgress.Layer;

                // Boss 战也吃「战斗计数 → 多出怪」这套规则（概率 ×0.5，Hyberty裁定）。
                // 组合方式是 **[龙] + 额外普通小怪**，不是「龙本身可能有多只」：
                //   · Boss 是剧情/数值上的唯一单位（MonsterRank.Boss、毒气弹的「非 Boss」判定、
                //     将来的掉落与通关奖励都按「一只 Boss」记账），复制多只龙会直接改变 Boss 战数值；
                //   · Hyberty要的是「怪变多」带来的场面压力，额外的小怪完全能达到这个效果；
                //   · SpawnExtras 已把额外小怪夹到 MaxMonsters - 1 = 6，加上龙总共不超过 7 行。
                List<Monster> monsters = new() { new Dragon() };
                monsters.AddRange(MobSpawner.SpawnExtras(EnvKind, currentLayer, p.FightCount));

                // 注意：类 Tools 与命名空间 LostCompound.Tools 同名，简单名 Tools 会被解析成
                // 命名空间，因此这里必须写全限定名 LostCompound.Tools.Tools。
                LostCompound.Tools.Tools.RunBattle(p, monsters, Environment);
                IsCleaned = true;

                // ── 打赢之后：要么通关，要么进下一层 ──
                // 判据与战斗房一致：IsAlive（Hp > 0）且没 EOF —— 输掉 / 输入断了都不算赢。
                // 输掉就算了：整局结束，RunMainLoop 的 finally 会走死亡那条路（删档 + 记死亡）。
                if (p.IsAlive && !p.InputClosed)
                {
                    if (GameProgress.Layer >= GameProgress.FinalLayer)
                    {
                        // 第 10 层 Boss = 通关。
                        Console.WriteLine("恭喜通关！你走完了全部 " + GameProgress.FinalLayer + " 层。");
                        p.MarkGameCleared();
                    }
                    else
                    {
                        // 举牌而不是当场换图：地图/生成器/分栏界面都在 RunMainLoop 手里，
                        // 见 Player.NextLayerRequested 的注释。
                        Console.WriteLine("通往下一层的阶梯出现了……");
                        p.RequestNextLayer();
                    }
                }
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

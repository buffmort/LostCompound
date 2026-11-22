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
                // Boss 按**环境**选，而不是写死龙（Hyberty 2026-11-22：沙葬者是沙漠层专属 Boss）。
                // 为什么放这里而不是 MobSpawner：Boss 是「这一层的主角」，与随机刷怪的池子不是一回事；
                // 将来每张地图加专属 Boss，就在 CreateBoss 里加一条，别的地方都不用动。
                List<Monster> monsters = new() { CreateBoss() };
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
                        //
                        // ⚠️ 这里**不再打印「通往下一层的阶梯出现了」**（Hyberty 2026-11-22）：
                        //   换层现在要玩家在主循环里输入 -1 才发生（B 方案），
                        //   那句话由主循环在真正等待输入时打 —— 打在这里会被主循环开头的
                        //   清屏擦掉（每屏都清），玩家根本看不到。
                        p.RequestNextLayer();
                    }
                }
            }
            else
            {
                Console.WriteLine("你回到了boss房间");
            }
        }

        /// <summary>
        /// 这一层的 Boss 是谁。**按环境查表**（Hyberty 2026-11-22：沙葬者是沙漠层专属 Boss）。
        ///
        /// 为什么按 <see cref="Environment.Type"/> 而不是按层数：
        ///   层数与环境的对应不是死的（第 2 层起玩家可以自选环境，见 EnvironmentCatalog.ForLayer），
        ///   同一个层数在不同局里可能是不一样的图。按环境选才能保证「进沙漠 → 打沙葬者」。
        ///
        /// 为什么默认是龙：龙是第 1 层（熔岩瀑布）的 Boss，也是其余所有还没配专属 Boss 的环境的兜底。
        ///   将来每张图加专属 Boss，在这里加一条 case 即可，别的地方不用动。
        /// </summary>
        private Monster CreateBoss()
        {
            return EnvKind switch
            {
                EnvType.Desert => new SandBurial(),
                EnvType.FloodedCavern => new Tidecaller(),
                _ => new Dragon(),
            };
        }

        public override void OnLeave(Player p)
        {
        }
    }
}

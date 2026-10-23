using Environment = LostCompound.MapEnvironment.Environment;
using LostCompound.Items;
using LostCompound.MapEnvironment;
using LostCompound.Monsters;
using LostCompound.Tools;
using LostCompound.Rooms;

namespace LostCompound.Rooms.Types
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
                // ⚠️ 层数硬编码为 1：层数系统还没做（BossRoom 现在是 Environment.Exit 占位）。
                // 等层数系统落地后，这个 1 要换成 Player/GameState 里的当前层数。
                const int CurrentLayer = 1;

                // 注意：类 Tools 与命名空间 LostCompound.Tools 同名，简单名 Tools 会被解析成
                // 命名空间，因此这里必须写全限定名 LostCompound.Tools.Tools。
                // 多怪概率用的 p.FightCount 是**本场开打前**的累计值：RunBattle 里的
                // SetInBattle(true) 在 Spawn 之后才把这一场计入（所以第一场是 0）。
                LostCompound.Tools.Tools.RunBattle(
                    p, MobSpawner.Spawn(EnvKind, CurrentLayer, p.FightCount), Environment);

                // 战斗结束后的环境额外掉落（如熔岩瀑布必掉硫磺）。
                //
                // ⚠️ 只在「真的打赢了」时发（本次修掉的一个真 bug）：
                //   原先 RunBattle 一返回就无条件发掉落，可它就是「这场战斗已经结束」的意思，
                //   并不代表赢了 —— 玩家可能被打死（Hp ≤ 0），也可能是输入流结束（EOF）
                //   导致战斗中止（Tools.RunBattle 特意说这种不算赢也不算输）。
                //   于是出现「熔岩瀑布战斗房打输 / 输入没了，照样获得硫磺 2-5」，
                //   甚至死亡结算之后还在往背包里塞东西。
                //   判据用 Player.IsAlive（Hp > 0）与 Player.InputClosed（EOF 标志），两者都是 public。
                //
                // 注意 IsCleaned 仍要无条件置 true（放在 if 外面，无论胜负）：
                //   这房间**打过了**，再进来就是「战斗过后的遗迹」，不能因为输过就允许重打刷怪。
                if (p.IsAlive && !p.InputClosed && Environment != null)
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

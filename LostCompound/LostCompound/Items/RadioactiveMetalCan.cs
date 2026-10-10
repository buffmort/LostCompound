using LostCompound.MapEnvironment;
using LostCompound.Tools;

namespace LostCompound.Items
{
    /// <summary>
    /// 放射金属罐（配方 1，ID 18）：饰品/容器，堆叠 10。
    /// 合成材料：2 放射矿 + 金属罐。
    /// 设计文档第九章原文：继承 11 重写（10 回合掉 2），饰品栏时屏蔽非环境辐射伤害。
    ///
    /// 「继承 11」= 和放射性矿物一样是「背包时定期扣血」，只是参数不同（10 回合 / 2 血）。
    /// </summary>
    /// <remarks>
    /// 为什么不实现 IEquippable：它没有被动数值加成（不加 ATK/DEF/护盾），
    /// 被动清单见 CONVENTIONS 2.4 —— 乱实现会让它出现在饰品栏可装备列表里并打印假的加成描述。
    /// </remarks>
    public sealed class RadioactiveMetalCan(int count) : Item(ItemIds.RadioactiveMetalCan, "放射金属罐", 10, true, count), ITurnCount
    {
        /// <summary>每多少回合扣一次血（覆盖放射性矿物的 5 回合）。</summary>
        public const int DamageInterval = 10;

        /// <summary>每次发作扣几点血（覆盖放射性矿物的 1 点）。</summary>
        public const int DamagePerTick = 2;

        /// <summary>距离下一次扣血还剩几个回合；每个实例各自计时，与放射性矿物同理。</summary>
        public int Timer { get; set; } = DamageInterval;

        public override ItemType? Type => ItemType.RadioactiveMetalCan;

        /// <summary>
        /// 与 RadioactiveOre 用完全相同的钩子：ITurnCount.OnBattleStart 由
        /// Player.HandleChangeableItems 在每个战斗回合调用一次（证据见 RadioactiveOre 的注释）。
        /// 两个类保持同一套写法，避免同类效果散落成两套实现（CONVENTIONS 2.5）。
        /// </summary>
        public void OnBattleStart(Player p, EnvType envType)
        {
            Timer -= 1;
            if (Timer > 0) return;

            Timer = DamageInterval;

            // 用了和放射性矿物相同的取舍：走 p.TakeDamage（计入护盾/防御，与 Tools.ApplyBuffs
            // 里的玩家流血一致），并且逐点结算（1 点一次，保证 DEF 最低取 1 时每点都真的扣到）。
            Console.WriteLine($"放射金属罐的辐射在发作！（每 {DamageInterval} 回合 {DamagePerTick} 点）");
            int hits = Count * DamagePerTick;   // 可叠加：同一格里 Count 份，每份扣 DamagePerTick 点
            for (int i = 0; i < hits; i++)
            {
                p.TakeDamage(1);
            }
        }

        public void OnBattleEnd(Player p, EnvType envType)
        {
            // 无需处理。
        }

        // TODO（辐射系统）：文档要求「饰品栏时屏蔽非环境辐射伤害」—— 依赖辐射系统，
        // 目前辐射伤害本身尚未实现，故此处只留说明，不写任何假逻辑。
    }
}

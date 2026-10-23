using LostCompound.MapEnvironment;
using LostCompound.Tools;
using LostCompound.Items;
using LostCompound.Items.Capabilities;

namespace LostCompound.Items.Materials
{
    /// <summary>
    /// 放射性矿物（配方 0，ID 11）：材料，堆叠 99。
    /// 设计文档第九章原文：背包时每 5 回合扣 1（困难 2）血，可叠加。
    ///
    /// 获取途径：文档写的是「Mine / 放射性区域开采」，这两个系统目前都还没做 ——
    /// 所以本类暂时无法从正常游戏流程拿到（只能调试/以后接开采系统）。
    /// 按约定不擅自加掉落表，缺口在实现报告里单独列出。
    /// </summary>
    public sealed class RadioactiveOre(int count) : Item(ItemIds.RadioactiveOre, "放射性矿物", 99, true, count), ITurnCount
    {
        /// <summary>每多少回合扣一次血（困难模式的「2 点伤害」要等困难模式系统，暂记 TODO）。</summary>
        public const int DamageInterval = 5;

        /// <summary>
        /// 距离下一次扣血还剩几个回合。每个道具实例各算各的计时器，
        /// 而 Player.AddItem 会把同类型可堆叠道具合并进同一个条目（Count 累加），
        /// 所以「可叠加」实际体现为：同一格里的 Count 份，每次结算各扣 1 点。
        /// </summary>
        public int Timer { get; set; } = DamageInterval;

        public override ItemType? Type => ItemType.RadioactiveOre;

        /// <summary>
        /// ITurnCount 的真实调用时机（不是「每场战斗一次」）：
        ///   Tools.RunBattle 每个大回合开头调用 p.OnTurnStart(...)（Tools.cs:51）
        ///   → Player.OnTurnStart 调 HandleChangeableItems（Player.cs:191）
        ///   → 遍历背包，对实现了 ITurnCount 的条目调 OnBattleStart（Player.cs:604-606）。
        /// 即：每个战斗回合调用一次。方法名叫 OnBattleStart 是历史命名，语义是「回合推进」。
        /// 注意它只遍历 Inventory，不含饰品栏 —— 本道具没实现 IEquippable，本来也进不了饰品栏。
        /// </summary>
        public void OnBattleStart(Player p, EnvType envType)
        {
            Timer -= 1;
            if (Timer > 0) return;

            Timer = DamageInterval;   // 满 5 回合结算一次，然后重新计时

            // 为什么用 p.TakeDamage 而不是直接 p.Hp -= 1：
            //   与项目现有的「每回合固定扣血」写法保持一致 —— Tools.ApplyBuffs
            //   结算玩家流血时用的就是 p.TakeDamage，它会走护盾/防御结算。
            //   若直接改 Hp 就会绕开护盾，出现「同是定期扣血、行为却不一致」的静默偏差。
            // 为什么扣 Count 次 1 点，而不是一次扣 Count 点：
            //   设计 7.1 是「每层格挡免疫一次伤害」，1 点就是「一层辐射」的粒度；
            //   且 Calculator.ApplyDefense 最低取 1，逐点结算才能保证每份真的扣到 1 血，
            //   合并成一次会被 DEF 白吃一部分。
            Console.WriteLine($"放射性矿物正在侵蚀你！（每 {DamageInterval} 回合发作一次）");
            for (int i = 0; i < Count; i++)
            {
                p.TakeDamage(1);
            }

            // TODO（困难模式）：困难下这里应为每次扣 2 血 —— 等困难模式系统做出来再接。
        }

        public void OnBattleEnd(Player p, EnvType envType)
        {
            // 无需处理：计时器跨战斗保留（与热硫的「跨战斗重置」不同，文档没要求重置）。
        }
    }
}

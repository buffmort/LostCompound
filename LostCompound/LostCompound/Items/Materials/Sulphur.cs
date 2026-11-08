using LostCompound.Buffs;
using LostCompound.Items;
using LostCompound.Items.Capabilities;
using LostCompound.Items.Consumables;
using LostCompound.MapEnvironment;
using LostCompound.Monsters;
using LostCompound.Tools;
using LostCompound.Tools.Players;

namespace LostCompound.Items.Materials
{
    /// <summary>
    /// 硫磺（ID 1，材料，堆叠 99）。
    ///
    /// 两条规则（Hyberty 2026-11-01 裁定）：
    ///   ① **在高温房间被烤成热硫** —— 设计文档 §9 行 1「高温房间 3 回合 → 热硫」，
    ///      实现方式逐字照抄木头（<see cref="Wood"/>）：只有环境自己声明了
    ///      <see cref="MapEnvironment.Environment.BurnsSulphur"/> 时倒计时才推进，
    ///      推进到 0 就整叠变成热硫。判定挂在环境的能力位上，不在道具里比环境枚举。
    ///   ② **能使用**（只能在战斗内）：随机挑一个**负面**状态，剩余回合 -2。
    ///      只抽自己身上的、只抽 <see cref="Buff.IsDebuff"/> 为真且不是永久的那些；
    ///      身上没有可削的负面状态时**不消耗**（白扣一个硫磺只会让人不敢用）。
    ///
    /// 为什么「随机挑一个」而不是「全部 -2」：Hyberty明确说的是随机。
    /// 随机也让它不能当万能解药 —— 中了三个 debuff 时它只削掉其中一个。
    /// </summary>
    public sealed class Sulphur(int count)
        : Item(ItemIds.Sulphur, "硫磺", 99, true, count),
          IUseable, ITurnCount, IChangeable, IItemState
    {
        public override ItemType? Type => ItemType.Sulphur;

        /// <summary>高温房间里还要撑几场战斗才变成热硫（文档：3 回合）。</summary>
        public const int HeatTurnsToMelt = 3;

        /// <summary>使用一次削掉多少剩余回合。</summary>
        public const int TurnsReducedPerUse = 2;

        /// <summary>还有几场高温战斗会被烤成热硫。</summary>
        public int Change { get; private set; } = HeatTurnsToMelt;

        // ==================== 规则 ②：使用 ====================

        public void Use(Player p, List<Monster> m)
        {
            // 只抽「负面的、还在生效的、不是永久的」—— 永久 debuff 削不动，
            // 抽到它等于这次使用白费，所以直接排除在候选之外。
            List<Buff> candidates = p.Buffs
                .Where(b => b.IsDebuff && !b.IsPermanent && !b.IsExpired)
                .ToList();

            if (candidates.Count == 0)
            {
                Console.WriteLine("你身上没有能削弱的负面状态，硫磺没有反应（本次不消耗）。");
                return;
            }

            Console.WriteLine("====你使用了硫磺!====");
            Buff target = candidates[Random.Shared.Next(candidates.Count)];
            target.AddTurns(-TurnsReducedPerUse);

            if (target.IsExpired)
            {
                p.Buffs.Remove(target);
                Console.WriteLine("硫磺刺激之下，" + target.Name + " 被压制住了（效果消除）。");
            }
            else
            {
                Console.WriteLine("硫磺刺激之下，" + target.Name + " 的剩余回合 -" +
                                  TurnsReducedPerUse + "（现在剩余 " + target.RemainingTurns + " 回合）。");
            }

            Count -= 1;
        }

        /// <summary>Hyberty裁定：只能在战斗内使用（地图上选中它会走这条提示）。</summary>
        public void ExplainBattleOnly(Player p)
        {
            Console.WriteLine("硫磺只能在战斗中使用！");
        }

        // ==================== 规则 ①：高温烤成热硫 ====================

        public void OnBattleStart(Player p, EnvType envType)
        {
            // 高温现在是**房间级**的（<see cref="Room.IsHot"/>，设计文档 §5.1）：
            // 只有站在高温房间里打，倒计时才推进 —— 与木头同一口径。
            if (p.CurrentRoom?.IsHot != true) return;

            Change -= 1;
            Console.WriteLine(Change > 0
                ? "硫磺:" + Change + "回合之后会被烤成热硫..."
                : "硫磺被高温烤成了热硫！");
        }

        public void OnBattleEnd(Player p, EnvType envType)
        {
            // 倒计时只在战斗开头推进，结束时不用做什么。
        }

        // ==================== 收尸 / 变身 ====================

        public bool ShouldRemove() => Change <= 0 || Count <= 0;

        /// <summary>
        /// 烤化了 → 整叠变成热硫；单纯的「用光了（Count 0）」→ 不给东西。
        /// 两种情形都可能走到这里，所以必须判一下，否则会用光后凭空造出「热硫 x0」。
        /// </summary>
        public Item Become() => Change <= 0 && Count > 0 ? new FushionSluphur(Count) : null;

        /// <summary>存档键名。**发布后不能改**（改了等于把旧存档里的倒计时清零）。</summary>
        private const string StateKeyChange = "change";

        public Dictionary<string, int> SaveState() => new() { [StateKeyChange] = Change };

        public void LoadState(IReadOnlyDictionary<string, int> state)
        {
            // 缺键 = 旧存档没这一项，保持默认值，不要当成 0（否则读档瞬间「硫磺变热硫」）
            if (state.TryGetValue(StateKeyChange, out int change))
            {
                // 夹到 [0, HeatTurnsToMelt]：负数会立刻烤成热硫，
                // 超过 3 等于白送回合。
                Change = Math.Clamp(change, 0, HeatTurnsToMelt);
            }
        }
    }
}

using LostCompound.Monsters;
using LostCompound.Tools;

namespace LostCompound.Items
{
    /// <summary>
    /// 药（ID 20，消耗品，堆叠 5）：+5 最大生命、+15 当前血，可用 3 次。
    ///
    /// 文档依据：DESIGN 第九章「20 药 消耗品 5 磷 + 水瓶 | +5 最大生命 +15 血，可用 3 次」。
    ///
    /// 配方：**红磷 × 1 + 水瓶 × 1**（配方 0，见 <see cref="Player.AddDefaultRecipes"/>）。
    ///   文档写的单字「磷」已由用户裁定为**红磷（ID 31）**，歧义解除，配方已接上。
    ///   数量文档没写，按 DESIGN 第九章的通用规则取 × 1。
    ///
    /// 耐久模型（与碘片一致，见 <see cref="IodineTablet"/> 的注释口径）：
    ///   **一颗药 = 3 次使用**（<see cref="Durability"/> 3、每次 -1），堆叠上限 5 指的是最多带 5 颗。
    ///   用满 3 次就把这一颗从堆叠里扣掉（<c>Count -= 1</c>）并把耐久重置给下一颗；
    ///   最后一颗用光时 Count 归零，<see cref="ShouldRemove"/> 成立 → 整叠移出背包。
    ///   这样「用 3 次后移除」与「一叠里有多颗」两条同时成立 ——
    ///   碘片那边是「一叠共用一个耐久计数、且耐久归零也不移除」，那是个已知的缺口，不照抄。
    ///
    /// 「+5 最大生命」为什么走 <see cref="Player.GrowBaseHp"/>：
    ///   BaseHp 是**血量上限**、不属于 RecomputeStats 的派生值（见该属性注释）。
    ///   直接写 <c>p.Hp = p.Hp + 5</c> 只加当前血、上限不变；GrowBaseHp 会把上限与当前血一起抬，
    ///   这正是「最大生命 +5」要的效果，也是这个方法当初留出来的唯一用途。
    ///
    /// 「+15 血」为什么必须夹取：
    ///   Hp 是资源、上限是 BaseHp，超额回血会让血量条越过上限（状态栏显示 215/205 这种）。
    ///   所以按 <c>Math.Min(Hp + 15, BaseHp)</c> 处理 —— 满血时这一条**不浪费上限**，只是不溢出。
    /// </summary>
    public sealed class Medicine(int count) : Item(ItemIds.Medicine, "药", 5, true, count), IUseable, IChangeable
    {
        public override ItemType? Type => ItemType.Medicine;

        /// <summary>一颗药的可用次数。</summary>
        public const int UsesPerTablet = 3;

        /// <summary>每次使用提升的最大生命。</summary>
        public const double MaxHpGain = 5;

        /// <summary>每次使用回复的血量。</summary>
        public const double HealAmount = 15;

        /// <summary>当前这颗药剩下的可用次数。</summary>
        public int Durability { get; private set; } = UsesPerTablet;

        public void Use(Player p, List<Monster> m)
        {
            if (Durability <= 0)
            {
                // 防御：正常路径下耐久归零时 ShouldRemove 已经把它移出背包，
                // 走到这里说明有别的调用点绕过了收尸 —— 提示而不是白扣次数。
                Console.WriteLine("这颗药已经用完了。");
                return;
            }

            Console.WriteLine("====你使用了药!====");
            Calculator.Delay(250);

            // 先抬上限（同时抬 5 点当前血），再补 15 点当前血并夹到上限。
            p.GrowBaseHp(MaxHpGain);
            p.Hp = Math.Min(p.Hp + HealAmount, p.BaseHp);
            // ⚠️ 这里**不能**顺手刷新状态栏：Player.RefreshStatus() 是 private，
            //   而本项目没有「给道具用的公开刷新入口」。加一个 public 方法属于改 public API
            //   （CONVENTIONS 1.2「先问」），所以不做。
            //   影响：本回合左侧状态栏的血量要等下一次刷新才更新 —— 下一回合开头的
            //   OnTurnStart（RecomputeStats + RefreshStatus）、或怪物打中你时的 TakeDamage
            //   都会刷到新值，所以最多滞后一个动作，且下面这行日志已经把新血量打出来了。

            Durability -= 1;
            Console.WriteLine("你服下了药：最大生命 +" + MaxHpGain + "，回复 " + HealAmount +
                              " 点血量（当前 " + p.Hp.ToString("F1") + "/" + p.BaseHp.ToString("F1") +
                              "，这颗药剩余 " + Durability + " 次）");

            if (Durability <= 0)
            {
                // 这一颗用完了：从堆叠里扣掉一颗，并把耐久重置给下一颗。
                // 继续用 Count 计价（而不是直接整叠移除）是为了让「堆叠 5」真的等于「5 颗药」。
                Count -= 1;
                Durability = UsesPerTablet;
            }
        }

        public void ExplainBattleOnly(Player p)
        {
            Console.WriteLine("药只能在战斗中使用！");
        }

        /// <summary>整叠用光才移出背包；耐久归零在 <see cref="Use"/> 里已经换成「扣掉一颗」。</summary>
        public bool ShouldRemove() => Count == 0;

        public Item Become() => null;
    }
}

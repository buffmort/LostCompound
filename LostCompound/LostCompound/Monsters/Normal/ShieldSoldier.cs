using LostCompound.Tools;
using LostCompound.Tools.Players;

namespace LostCompound.Monsters.Normal
{
    /// <summary>
    /// 盾兵（DESIGN §6.2）—— 纯辅助单位。
    ///
    /// 数值：`hp 10 × 当前层数`，`def 0`，`str 0`，`atk 0`，出现层数 `[1, 5]`。
    /// 行为：**自己不输出**，每回合给**随机一只其它怪**（不含自己）加 `5 × 当前层数` 的盾。
    /// 掉落：1 金属（权重 0.5，见 <see cref="MonsterLootTable"/>）。
    ///
    /// 三条口径（Hyberty裁定，见 DESIGN §6.2）：
    ///   ① 掉落里的「铁」= 金属（项目里没有「铁」这个道具）；
    ///   ② **不包括自己** —— 它只保护同伴；
    ///   ③ **每只盾兵各自独立触发** —— 场上两只就每回合给两次盾，
    ///      两次的目标可以随机到同一只怪（那只怪一回合吃两份盾）。
    ///
    /// 「仅在多怪物时出现」这一条**不在本类里判断**：单只出场的过滤在
    /// `MobSpawner.SpawnMany` 做（抽完一只发现是盾兵就重抽）——
    /// 因为「场上还有没有别的怪」只有生成侧知道，怪物自己看不到生成过程。
    /// </summary>
    public sealed class ShieldSoldier : Monster
    {
        /// <summary>每层血量。文档：`10 × 当前层数`。</summary>
        public const double HpPerLayer = 10;

        /// <summary>每层给盾量。文档：`5 × 当前层数`。</summary>
        public const double ShieldPerLayer = 5;

        /// <summary>本场战斗的层数（由 <see cref="OnSpawn"/> 注入，默认按第 1 层算）。</summary>
        private int _layer = 1;

        public ShieldSoldier()
        {
            Type = MonsterType.ShieldSoldier;
            Name = "盾兵";

            // 构造函数里只写「第 1 层」的数值：真正的血量在 OnSpawn 里按层数重算。
            // 为什么不在构造函数里写死：池子里存的是无参工厂，构造函数拿不到层数；
            // 写死会把「第 1 层」固化进去，第 5 层也还是 10 血。
            Hp = HpPerLayer;
            Atk = 0;
            Str = 0;
            Def = 0;
            Shield = 0;
            SkipTurn = 0;
        }

        public override MonsterRank Rank => MonsterRank.Normal;

        /// <summary>按层数缩放血量。由 `MobSpawner.SpawnOne` 在 `Create()` 之后调用。</summary>
        public override void OnSpawn(int layer)
        {
            _layer = Math.Max(layer, 1);
            Hp = HpPerLayer * _layer;
        }

        /// <summary>
        /// 「攻击」= 给队友加盾（它 atk 为 0，不造成伤害）。
        ///
        /// 目标：**除自己以外**、还活着的怪里随机一只。
        /// 没有任何队友时（理论上生成侧已经排除了单只出场，但调试/手工构造仍可能遇到）
        /// 就什么都不做 —— 不能因此崩，也不能去给玩家加盾。
        /// </summary>
        public override void Attack(Player p)
        {
            List<Monster> candidates = [];
            foreach (Monster ally in BattleAllies)
            {
                // 排除自己：用引用比较，不用 Name/Type —— 场上可能有多只盾兵
                if (ReferenceEquals(ally, this)) continue;
                if (!ally.IsAlive) continue;
                candidates.Add(ally);
            }

            if (candidates.Count == 0)
            {
                Console.WriteLine(Name + " 举着盾，但身边没有可以保护的同伴。");
                return;
            }

            Monster target = candidates[Random.Shared.Next(candidates.Count)];
            double amount = ShieldPerLayer * _layer;
            target.AddShield(amount);
            Console.WriteLine(Name + "为" + target.Name + "提供了" + amount + "点护盾！");
        }

        public override void OnMonsterTurn(Player p)
        {
            // 跳过检查与其它怪同一套（被热硫/压缩沙瓶跳过时不该还能加盾）
            if (TryConsumeSkipTurn())
            {
                Console.WriteLine(Name + " 的回合被跳过了。");
                return;
            }

            Attack(p);
        }

        public override void TakeDamage(double dmg)
        {
            // 与其它普通怪同一条骨架：DEF → 日志 → 死亡判定都在基类里
            TakeDamageWithDefaultLog(dmg);
        }

        protected override int RollIndex(int count) => Random.Shared.Next(count);
    }
}

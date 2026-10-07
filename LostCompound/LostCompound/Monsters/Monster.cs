using LostCompound.Items;
using LostCompound.Tools;
using LostCompound.Buffs;

namespace LostCompound.Monsters
{
    public abstract class Monster
    {
        public double Hp { get; set; }
        public double Atk { get; set; }
        public double Str { get; set; }
        public double Def { get; set; }
        public string Name { get; set; }
        // Java 的 protected 同时允许同包访问；C# 的 protected 不允许通过其它子类实例访问，
        // 故此处用 protected internal（= 派生类 or 同一程序集），等价于 Java 语义。
        public int Block { get; protected internal set; }
        public double DotDamage { get; set; }
        public double DotDuration { get; set; }
        public int SkipTurn { get; set; }

        // 注意：原 setDOTdur 做的是 DOTdur += v（累加），绝不能写成属性，否则会变成覆盖。
        public void AddDotDuration(double duration) => DotDuration += duration;

        // 注意：原 setDOTdmg 做的是 DOTdmg += v（累加），同样不能写成属性。
        public void AddDotDamage(double damage) => DotDamage += damage;

        public void AddSkipTurn(int count) => SkipTurn += count;

        // 从 Java 移植时枚举是引用类型，未赋值即为 null；C# 枚举是值类型，默认值是 0
        // （= Dragon），会改变掉落行为，所以这里用可空 MonsterType?。
        // 现在所有怪物子类都在构造时显式赋了 Type，实际不会为 null。
        public MonsterType? Type { get; set; }

        public abstract void Attack(Player p);
        public abstract void TakeDamage(double dmg);
        public abstract void OnMonsterTurn(Player p);
        protected abstract int RollIndex(int count);

        // 原 6 个子类各自重写的 isAlive() 实现完全相同（都是 hp > 0），
        // 故合并为基类上的一个计算属性，子类不再重写。
        public bool IsAlive => Hp > 0;

        public override string ToString() => $"{Name}({Math.Max(Hp, 0):F1}) ";

        private bool _lootDropped;

        public List<Item> Die(Player player)
        {
            if (IsAlive || _lootDropped)
            {
                return [];
            }
            _lootDropped = true;

            List<Item> loots = MonsterLootTable.Drop(Type);
            Console.WriteLine(Name + " 死了。");
            foreach (Item item in loots)
            {
                // 掉落播报已统一由 Player.AddItem 负责，这里不再重复打印
                player.AddItem(item);
            }
            return loots;
        }

        // DOT 递减（你现有的 setDOTdur 是 += 累加，不能当递减用）
        public void DecreaseDotDuration()
        {
            if (DotDuration > 0) DotDuration--;
        }

        public void ClearDot()
        {
            DotDuration = 0;
            DotDamage = 0;
        }

        // ==================== 状态效果（buff，如流血） ====================
        // 注意：与 Player 一样，DotDamage / DotDuration 保持原样不动，
        // 旧的 DOT 结算（Tools.ApplyDotEffects）行为完全不变；buff 是并行机制。
        /// <summary>挂在怪物身上的状态效果。结算时机见 Tools.ApplyBuffs（大回合开场，与 DOT 同一时机）。</summary>
        public List<Buff> Buffs { get; } = [];

        /// <summary>
        /// 给怪物挂一个状态效果。同类 buff 叠加到已有对象上（与 Player.AddBuff 同一口径），
        /// 避免同一只怪身上出现两个「流血 x1」互相独立结算。
        /// </summary>
        public void AddBuff(Buff b)
        {
            if (b == null) return;
            foreach (Buff exist in Buffs)
            {
                if (exist.GetType() == b.GetType())
                {
                    exist.AddStacks(b.Stacks);
                    return;
                }
            }
            Buffs.Add(b);
        }

        // 跳过回合：返回"这次是否被跳过"，顺手扣一次计数
        public bool TryConsumeSkipTurn()
        {
            if (SkipTurn <= 0) return false;
            SkipTurn--;
            Console.WriteLine(Name + " 被跳过回合！");
            return true;
        }
    }
}

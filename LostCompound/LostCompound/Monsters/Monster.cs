using LostCompound.Items;
using LostCompound.Tools;

namespace LostCompound.Monsters
{
    public abstract class Monster
    {
        protected double hp;
        protected double atk;
        protected double str;
        protected double def;
        protected string name;
        // Java 的 protected 同时允许同包访问；C# 的 protected 不允许通过其它子类实例访问，
        // 故此处用 protected internal（= 派生类 or 同一程序集），等价于 Java 语义。
        protected internal int block;
        protected double DOTdmg;
        protected double DOTdur;
        protected int skipTurn;

        public void addSkipTurn(int count)
        {
            skipTurn += count;
        }
        // Java 枚举是引用类型，未赋值时为 null（Zombie 就从未设置 monsterType）；
        // C# 枚举是值类型，默认值是 0（= DRAGON），会改变掉落行为，故用可空 MonsterType?。
        public MonsterType? monsterType;

        public void setDOTdur(double DOTdur)
        {
            this.DOTdur += DOTdur;
        }

        public void setDOTdmg(double DOTdmg)
        {
            this.DOTdmg += DOTdmg;
        }

        public double getHp()
        {
            return hp;
        }

        public double getStr()
        {
            return str;
        }

        public double getAtk()
        {
            return atk;
        }

        public string getName()
        {
            return name;
        }

        public int getBlock()
        {
            return block;
        }

        public double getDef()
        {
            return def;
        }

        public double getDOTdur()
        {
            return DOTdur;
        }

        public double getDOTdmg()
        {
            return DOTdmg;
        }

        public void setStr(double str)
        {
            this.str = str;
        }

        public void setAtk(double atk)
        {
            this.atk = atk;
        }

        public abstract void Attack(Player p);
        public abstract void takeDmg(double dmg);
        public abstract void onMonsterTurn(Player p);
        protected abstract int rdChoice(int i);
        // 注意：Java 的方法默认就是「虚方法」，子类可以直接 @Override。
        // C# 不是 —— 基类方法必须显式写 virtual，子类才能 override，
        // 否则子类写 override 会报 CS0506。这里 6 个怪物子类都重写了 isAlive。
        public virtual bool isAlive()
        {
            return this.hp > 0;
        }

        public override string ToString()
        {
            double displayhp = Math.Max(this.hp, 0);
            return this.name + "(" + displayhp.ToString("F1") + ") ";
        }
        private bool lootDropped = false;

        public List<Item> die(Player player)
        {
            if (isAlive() || lootDropped)
            {
                return new List<Item>();
            }
            lootDropped = true;

            List<Item> loots = MonsterLootTable.drop(this.monsterType);
            Console.WriteLine(name + " 死了。");
            foreach (Item item in loots)
            {
                Console.WriteLine(name + " 掉落了：" + item.getName());
                player.addItem(item);
            }
            return loots;
        }
        // DOT 递减（你现有的 setDOTdur 是 += 累加，不能当递减用）
        public void decreaseDOTdur()
        {
            if (DOTdur > 0) DOTdur--;
        }

        public void clearDOT()
        {
            DOTdur = 0;
            DOTdmg = 0;
        }

        // 跳过回合：返回"这次是否被跳过"，顺手扣一次计数
        public bool tryConsumeSkipTurn()
        {
            if (skipTurn <= 0) return false;
            skipTurn--;
            Console.WriteLine(name + " 被跳过回合！");
            return true;
        }
    }
}

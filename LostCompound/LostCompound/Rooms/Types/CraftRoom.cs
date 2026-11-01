using Environment = LostCompound.MapEnvironment.Environment;
using LostCompound.MapEnvironment;
using LostCompound.Items.Capabilities;
using LostCompound.Tools;
using LostCompound.Rooms;
using LostCompound.Tools.Players;

namespace LostCompound.Rooms.Types
{
    /// <summary>
    /// 合成房（DESIGN §4.2「合成台」）—— 有合成台，所以能做**徒手做不了**的配方。
    ///
    /// 规则（Hyberty裁定）：
    ///   · 进房时把「**全部已实现配方 − 玩家当前已有的配方**」这个**补集**临时加进背包配方表；
    ///   · 在这里可以按「标准合成」把它们做出来（<see cref="Player.CraftWithStation"/>）；
    ///   · **离开时把这些临时配方删掉** —— 理由：徒手没有工具，做不了它们很合理。
    ///     合出来的**产物照常留在背包**（被收回的只是「配方」，不是「东西」）。
    ///
    /// 为什么配方的持有者仍是 <see cref="Player.Recipes"/>而不是本房间：
    ///   合成界面（列表 / 选择 / 材料校验 / 收尾）全都只认 `Player.Recipes`，
    ///   房间再存一份就成了两份真相。房间只负责「进出时增删」，中间过程完全不插手。
    ///
    /// ⚠️ 目前全集只有配方 0（12 条）+ 配方 1（9 条），而配方 0 开局就有、配方 1 第 5 次通关解锁 ——
    ///   所以补集大多数时候是**空的**（进房间只会说「没有额外的配方」）。
    ///   等配方 2/3 做出来，合成台才真正体现价值。
    /// </summary>
    public sealed class CraftRoom : Room
    {
        /// <summary>本房间临时加进玩家配方表的那几条（离开时要原样删掉）。</summary>
        private readonly List<ICraftable> _temporaryRecipes = [];

        public CraftRoom(Environment environment) : base(RoomType.Craft, environment)
        {
        }

        public override void OnEnter(Player p)
        {
            Console.WriteLine("这里有一个合成台。");

            // 先清掉上一轮可能残留的临时配方（例如存档读回来的半截状态）。
            // 不清的话反复进出会让同一个配方在列表里出现多份。
            RemoveTemporaryRecipes(p);

            List<ICraftable> universe = Player.BuildAllImplementedRecipes();

            // 补集：全集里「玩家手上还没有的」。按名字比对 ——
            // Recipe 没有重写 Equals，按引用比会把每一条都当成新的（全集里是 new 出来的实例）。
            foreach (ICraftable recipe in universe)
            {
                bool alreadyHave = false;
                foreach (ICraftable owned in p.Recipes)
                {
                    if (owned.Name == recipe.Name)
                    {
                        alreadyHave = true;
                        break;
                    }
                }
                if (alreadyHave) continue;

                p.Recipes.Add(recipe);
                _temporaryRecipes.Add(recipe);
            }

            if (_temporaryRecipes.Count == 0)
            {
                Console.WriteLine("（合成台上没有你还不会的配方。）");
                return;
            }

            Console.WriteLine("合成台让你能做这些平时做不了的东西：" + string.Join("、", _temporaryRecipes.Select(r => r.Name)));
            p.CraftWithStation();
        }

        public override void OnLeave(Player p)
        {
            RemoveTemporaryRecipes(p);
        }

        /// <summary>把本房间加进去的配方逐条摘掉，并清空记录（幂等：没加过就什么都不做）。</summary>
        private void RemoveTemporaryRecipes(Player p)
        {
            foreach (ICraftable recipe in _temporaryRecipes)
            {
                p.Recipes.Remove(recipe);
            }
            _temporaryRecipes.Clear();
        }
    }
}

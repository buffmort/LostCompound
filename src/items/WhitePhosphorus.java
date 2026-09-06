package items;

import monsters.Monster;
import tools.Calculator;
import tools.Player;

import java.util.ArrayList;
import java.util.Scanner;

public class WhitePhosphorus extends Item implements Useable,Changeable {
    Scanner sc = new Scanner(System.in);

    public WhitePhosphorus(int count) {
        this.name = "白磷";
        this.id = 5;
        this.maxStack = 99;
        this.Stackable = true;
        this.count = count;
    }

    @Override
    public ItemType getType() {
        return ItemType.WHITE_PHOSPHORUS;
    }

    @Override
    public void use(Player p, ArrayList<Monster> m) {
        double dotDmg = 20;
        int DUR = 3;
        System.out.println("====你使用了白磷!====");
        Calculator.delay(250);
        int choice = p.selectTarget(m);
        if (choice == -1) {
            System.out.println("你输入了错误的序号 跳过");
        }
        Monster target = m.get(choice);
        target.setDOTdmg(dotDmg);
        target.setDOTdur(DUR);
        System.out.println("你向" + target.getName() + "投掷了一个白磷!造成" + target.getDOTdur() + "回合持续时间" +
                "与 " + target.getDOTdmg() + "每回合伤害");
        for (Monster monster : m) {
            if (monster == target) {
                continue;
            }
            monster.setDOTdmg(10);
            monster.setDOTdur(3);
        }
        System.out.println("你对所有怪物造成10点溅射伤害!");
        count -=1;
    }

    @Override
    public void ExpBattle(Player p) {
        System.out.println("你不能单独用他!");

    }

    @Override
    public boolean ShouldRemove() {
        return count == 0;
    }

    @Override
    public Item Become() {
        return null;
    }
}

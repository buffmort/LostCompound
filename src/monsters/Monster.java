package monsters;
import tools.*;

public abstract class Monster {
    protected double hp;
    protected double atk;
    protected double str;
    protected double def;
    protected String name;
    protected int block;
    protected double DOTdmg;
    protected double DOTdur;
    protected int skipTurn;

    public void addSkipTurn(int count) {
        skipTurn += count;
    }

    public void setDOTdur(double DOTdur) {
        this.DOTdur += DOTdur;
    }

    public void setDOTdmg(double DOTdmg) {
        this.DOTdmg += DOTdmg;
    }

    public double getHp() {
        return hp;
    }

    public double getStr() {
        return str;
    }

    public double getAtk() {
        return atk;
    }

    public String getName() {
        return name;
    }

    public int getBlock() {
        return block;
    }

    public double getDef() {
        return def;
    }

    public double getDOTdur() {
        return DOTdur;
    }

    public double getDOTdmg() {
        return DOTdmg;
    }

    public void setStr(double str) {
        this.str = str;
    }

    public void setAtk(double atk) {
        this.atk = atk;
    }

    public abstract void Attack(Player p);
    public abstract void takeDmg(double dmg);
    public abstract void onMonsterTrun(Player p);
    protected abstract int rdChoice(int i);
    public abstract boolean isAlive();


    @Override
    public String toString() {
        double displayhp = Math.max(this.hp,0);
        return this.name+"("+String.format("%.1f",displayhp)+") ";
    }
}

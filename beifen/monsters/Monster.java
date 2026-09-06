package monsters;

public abstract class Monster {
    protected double hp;
    protected double atk;
    protected double str;
    protected double def;
    protected String name;
    protected boolean block = false;

    protected abstract void Attack(Player p);
    protected abstract void takeDmg(double dmg);
    protected abstract void onMonsterTrun(Player p);
    protected abstract int rdChoice(int i);
    protected abstract boolean isAlive();


    @Override
    public String toString() {
        double displayhp = Math.max(this.hp,0);
        return this.name+String.format("%.1f",displayhp);
    }
}

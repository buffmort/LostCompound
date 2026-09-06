

public class Calculator {
    public static double Attack(double str,double atk){
        double OutPutDmg = 2+str*Math.sqrt(atk);
        return OutPutDmg;
    }

    public static double Define(double def,double dmg){
        double outPutDmg = (dmg*40) / (def+40);
        return outPutDmg;
    }
    public static void delay(int time){
        try{
            Thread.sleep(time);
        } catch (InterruptedException e){
            e.printStackTrace();
        }
    }
}

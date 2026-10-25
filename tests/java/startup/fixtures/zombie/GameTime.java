package zombie;
public final class GameTime {
    public static GameTime instance=new GameTime();
    public int year=2001,month=11,day=30,hour=23,minutes=59;public double worldHours=99999;
    public static GameTime getInstance(){return instance;}
    public int getYear(){return year;}public int getMonth(){return month;}public int getDay(){return day;}public int getHour(){return hour;}public int getMinutes(){return minutes;}public double getWorldAgeHours(){return worldHours;}
}

package ozl.startup;

import java.util.*;

/** Decorative waiting captions. This clock never changes observed loading state. */
final class LoadingHumor {
    private static final String[] PHRASES={
        "狗正在跨越栅栏","正在摧毁模组","正在添加bug","正在鞭策浣熊","正在给僵尸系鞋带","正在说服直升机绕路",
        "正在把罐头藏到最后一格","正在检查烤箱有没有关","正在教僵尸礼貌敲门","正在给撬棍办理退休返聘","正在寻找那把十毫米扳手",
        "正在把发电机搬离卧室","正在给窗帘安排夜班","正在劝角色别喝漂白水","正在把消防斧借给伐木工","正在寻找最后一瓶番茄酱",
        "正在为水龙头预留最后一滴水","正在让警报器保持冷静","正在往后备箱塞第五张椅子","正在给薯片计算保质期","正在向树枝解释车辆保险",
        "正在为安全屋补上第七把锁","正在把袜子配成一对","正在说服僵尸排队领号","正在给乌鸦更新导航","正在确认这口蘑菇能不能吃",
        "正在把书签夹回木工第二卷","正在给棒球棍补一颗钉子","正在装作没听见远处的枪声","正在为下次停电练习摸黑",
        "正在为斯皮佛熨平围裙","正在把开罐器列为传家宝","正在替最后一罐豆子写遗嘱","正在向空冰箱索要精神赔偿","正在给发电机补一堂室外课",
        "正在劝车喇叭保持低调","正在给破窗玻璃安排带薪休假","正在把床单升职为安保主管","正在为雨水桶举办接水仪式","正在给萝卜苗安排保镖",
        "正在与卷心菜讨论晚饭人选","正在向蚯蚓介绍钓鱼岗位","正在说服鱼先别下班","正在检查陷阱里的胡萝卜考勤","正在给木工节目留前排座",
        "正在为电视雪花配学习笔记","正在把技能书读成睡前故事","正在寻找书架上失踪的第一卷","正在给螺丝刀申请电工学位","正在为汽车搭线练习深呼吸",
        "正在向空油箱许愿","正在给轮胎讲避坑指南","正在替引擎祈祷再响一次","正在检查钥匙是不是又在僵尸兜里","正在给车座上的微波炉系安全带",
        "正在为后备箱举行断舍离会议","正在把垃圾袋供上雨水桶神坛","正在给胶带授予抢修勋章","正在劝钉子别从背包里扎出来","正在给大锤安排拆迁面试",
        "正在为撬棍拒绝退休写申诉","正在请消防斧兼任园艺顾问","正在给围墙补一份请勿投喂告示","正在教邻居把敲门改成预约","正在为门外邻居取消聚餐邀请",
        "正在让尸潮走游客通道","正在给直升机推荐别的景点","正在把远处枪声归为邻里装修","正在为警报声寻找音量旋钮","正在给潜行鞋底贴静音标签",
        "正在把备用袜子藏进急救包","正在劝绷带别再兼职抹布","正在为消毒水保留正经工作","正在请角色不要徒手庆祝碎玻璃","正在给僵尸的牙齿发送休假通知",
        "正在与疲劳小人协商再走两步","正在劝饥饿小人等水开","正在给惊恐小人泡杯想象中的茶","正在把负重小人请下肩膀","正在对背包说这台电视真的很小",
        "正在为罐头山寻找第二个开罐器","正在给冷掉的炖菜写热评","正在向过期酸奶婉拒合作","正在为最后一包薯片安排护送","正在把平底锅调到近战频道",
        "正在为罗斯伍德规划安静路线","正在劝西点镇减少欢迎队伍","正在给河畔镇的鱼发邀请函","正在把马尔德劳写进绕行清单","正在请斯皮佛代收末日快递"
    };
    private static final Random RANDOM=new Random();
    private record Key(long operation,LoadingModel.Context context) {}
    private static final Map<Key,Deck> DECKS=new LinkedHashMap<>(16,.75f,true);
    private static Key currentKey;
    private static Deck currentDeck;
    /** One shuffled tour per loading session. Repainting never advances or reshuffles it. */
    static final class Deck {
        private final String[] phrases=PHRASES.clone();
        final int delay;
        Deck(Random random){
            for(int i=phrases.length-1;i>0;i--){int j=random.nextInt(i+1);String swap=phrases[i];phrases[i]=phrases[j];phrases[j]=swap;}
            delay=6+random.nextInt(2);
        }
        String phrase(long slot){return phrases[Math.floorMod(slot,phrases.length)];}
    }
    private static synchronized Deck deck(long operation,LoadingModel.Context context){
        if(currentKey!=null&&currentKey.operation==operation&&currentKey.context==context)return currentDeck;
        Key key=new Key(operation,context);Deck deck=DECKS.get(key);
        if(deck==null){deck=new Deck(RANDOM);DECKS.put(key,deck);if(DECKS.size()>16)DECKS.remove(DECKS.keySet().iterator().next());}
        currentKey=key;currentDeck=deck;return deck;
    }
    record Caption(String text,int opacity) {}
    static Caption at(double seconds,long operation,LoadingModel.Context context,boolean reduced){
        if(!reduced&&!Double.isFinite(seconds))return new Caption("",0);
        Deck deck=deck(operation,context);
        if(reduced)return new Caption(deck.phrase(0),155);
        double elapsed=Math.max(0,seconds)-deck.delay;
        if(elapsed<0)return new Caption("",0);
        long block=(long)Math.floor(elapsed/22.2);elapsed%=22.2;int slot=0;
        double[] periods={7.0,7.4,7.8};while(slot<2&&elapsed>=periods[slot])elapsed-=periods[slot++];
        if(elapsed>=5)return new Caption("",0);
        double fade=Math.min(1,Math.min(elapsed/.32,(5-elapsed)/.32));
        int opacity=(int)Math.round(fade*12)*13;
        if(opacity==0)return new Caption("",0);
        return new Caption(deck.phrase((block%PHRASES.length)*3+slot),opacity);
    }
}

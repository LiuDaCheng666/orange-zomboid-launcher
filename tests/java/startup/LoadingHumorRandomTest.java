package ozl.startup;

import java.util.*;

/** Real caption selection with a synthetic clock; never starts the game. */
public final class LoadingHumorRandomTest {
    static int checks;
    static void check(boolean value,String why){if(!value)throw new AssertionError(why);checks++;}
    static double time(int slot){return 9+(slot/3)*22.2+new double[]{0,7,14.4}[slot%3];}
    static List<String> tour(long operation,LoadingModel.Context context){
        List<String> phrases=new ArrayList<>();Set<String> seen=new HashSet<>();
        for(int i=0;i<=110;i++){
            LoadingHumor.Caption caption=LoadingHumor.at(time(i),operation,context,false);
            check(caption.opacity()>0&&!caption.text().isEmpty(),"each synthetic sample reaches a readable real caption");
            check(caption.equals(LoadingHumor.at(time(i)+.05,operation,context,false)),"rendering another frame does not roll a new phrase");
            if(!seen.add(caption.text())){
                check(caption.text().equals(phrases.get(0)),"every phrase appears once before a deck repeats");
                return phrases;
            }
            phrases.add(caption.text());
        }
        throw new AssertionError("caption repertoire must remain bounded");
    }
    static void actualDisplay(){
        for(LoadingModel.Context context:new LoadingModel.Context[]{LoadingModel.Context.BOOT,LoadingModel.Context.SAVE,LoadingModel.Context.SERVER}){
            long operation=500+context.ordinal();List<String> phrases=tour(operation,context);
            check(phrases.size()>=80,"a full actual loading session has substantially more PZ humor than the old 30 phrases");
            for(String required:new String[]{"狗正在跨越栅栏","正在摧毁模组","正在添加bug","正在鞭策浣熊"})check(phrases.contains(required),"requested original remains reachable: "+required);
            String previous="";
            for(int i=0;i<phrases.size()*3;i++){
                String current=LoadingHumor.at(time(i),operation,context,false).text();
                check(!current.equals(previous),"adjacent captions never repeat, including deck rollover");previous=current;
            }
            LoadingHumor.Caption still=LoadingHumor.at(0,operation,context,true);
            check(!still.text().isEmpty(),"reduced motion keeps a decorative static caption");
            for(double time:new double[]{1,30,300,1200})check(still.equals(LoadingHumor.at(time,operation,context,true)),"reduced motion never cycles captions");
            for(String phrase:phrases)check(phrase.length()<=22,"captions stay short enough for the existing small viewport lane");
        }
        check(LoadingHumor.at(0,999,LoadingModel.Context.BOOT,false).text().isEmpty(),"short loading remains quiet");
        for(double invalid:new double[]{Double.NaN,Double.NEGATIVE_INFINITY,Double.POSITIVE_INFINITY})check(LoadingHumor.at(invalid,999,LoadingModel.Context.BOOT,false).text().isEmpty(),"invalid visual clocks do not show captions");
    }
    static void independentSessions(){
        int count=tour(800,LoadingModel.Context.BOOT).size();
        LoadingHumor.Deck firstDeck=new LoadingHumor.Deck(new Random(20261027)),nextDeck=new LoadingHumor.Deck(new Random(20261028));
        List<String> first=new ArrayList<>(),next=new ArrayList<>();
        for(int i=0;i<count;i++){first.add(firstDeck.phrase(i));next.add(nextDeck.phrase(i));}
        List<String> cycle=new ArrayList<>(first);cycle.addAll(first);
        check(Collections.indexOfSubList(cycle,next.subList(0,12))<0,"new loading sessions reshuffle the order instead of rotating the same fixed sequence");
    }
    public static void main(String[] args){actualDisplay();independentSessions();System.out.println("PASS "+checks+" shuffled-caption display/repertoire/reduced-motion checks");}
}

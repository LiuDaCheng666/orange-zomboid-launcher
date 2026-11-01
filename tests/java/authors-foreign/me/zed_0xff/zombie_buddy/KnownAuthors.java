package me.zed_0xff.zombie_buddy;
public final class KnownAuthors {
 public static int calls;public static final java.util.concurrent.CountDownLatch entered=new java.util.concurrent.CountDownLatch(1);
 private static String fetchRemoteBody(){calls++;entered.countDown();return "foreign-untrusted-body";}
}

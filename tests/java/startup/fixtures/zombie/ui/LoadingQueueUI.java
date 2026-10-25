package zombie.ui;
public final class LoadingQueueUI {public int renders;public void render(){renders++;zombie.core.SpriteRenderer.instance.queue.add("queue-controls");}}

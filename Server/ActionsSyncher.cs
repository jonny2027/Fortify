using System.Collections.Concurrent;

namespace PersistenceServer
{
    public class ActionsSyncher
    {
        public readonly ConcurrentQueue<Action> ConQ = new();

        public async Task Tick()
        {
            while (true)
            {
                process_queue:
                if (ConQ.TryDequeue(out var result))
                {
                    await Task.Run(result);
                    goto process_queue;
                }
                await Task.Delay(8);
            }
        }
    }
}

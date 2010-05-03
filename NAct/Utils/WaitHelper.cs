using System;
using System.Threading;

namespace NAct
{
    public static class WaitHelper
    {
        /// <summary>
        /// Sometimes it's just too much effort to code something asynchronously, and you'd rather block your thread.
        /// 
        /// Don't use this much or your performance will suck!!!
        /// </summary>
        public static T InvokeAndWait<T>(Action<Action<T>> toDo)
        {
            object waiter = new object();
            T toReturn = default(T);

            lock (waiter)
            {
                toDo(
                    delegate(T result)
                        {
                            lock (waiter)
                            {
                                toReturn = result;
                                Monitor.Pulse(waiter);
                            }
                        });

                Monitor.Wait(waiter);

                return toReturn;
            }
        }
    }
}

using System;
using System.Threading;

namespace NAct.Utils
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
            ManualResetEvent mre = new ManualResetEvent(false);
            T toReturn = default(T);

            toDo(
                result =>
                    {
                        toReturn = result;
                        mre.Set();
                    });

            mre.WaitOne();

            return toReturn;
        }
    }
}

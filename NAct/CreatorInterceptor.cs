using System.Threading;
using Castle.Core.Interceptor;

namespace NAct
{
    /// <summary>
    /// Creates an object, makes a ThreaderInterceptor to wrap it, then delegates to the ThreaderInterceptor
    /// </summary>
    class CreatorInterceptor : IInterceptor
    {
        private ThreaderInterceptor m_ThreaderInterceptor;

        /// <summary>
        /// Something to lock on while co-ordinating construction
        /// </summary>
        private readonly object m_Sync = new object();

        /// <summary>
        /// Creates an interceptor for an object that doesn't exist yet.
        /// 
        /// This will trigger its asynchronous construction.
        /// </summary>
        public CreatorInterceptor(ObjectCreator<IActor> creator)
        {
            ThreadPool.QueueUserWorkItem(
                delegate
                    {
                        lock (m_Sync)
                        {
                            IActor newObject = creator();
                            ThreaderInterceptor temp = new ThreaderInterceptor(newObject, newObject);

                            // Need to make sure that the ThreaderInterceptor is completely finished being constructed before
                            // assigning it to the field, so that unsynchronised access to it is safe.
                            Thread.MemoryBarrier();
                            m_ThreaderInterceptor = temp;
                            Monitor.PulseAll(m_Sync);
                        }
                    });
        }

        public void Intercept(IInvocation invocation)
        {
            if (m_ThreaderInterceptor == null)
            {
                // Have to wait for it to get made
                lock (m_Sync)
                {
                    while (m_ThreaderInterceptor == null)
                    {
                        // Use a timed-out wait to mitigate the missed update problem
                        Monitor.Wait(m_Sync, 50);
                    }
                }
            }

            // Now m_ThreaderInterceptor is definitely finished, forward to it
            m_ThreaderInterceptor.Intercept(invocation);
        }
    }
}

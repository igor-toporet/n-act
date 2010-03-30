using System;
using System.Reflection;
using System.Threading;

namespace NAct
{
    class CreatorInterfaceInvocationHandler : IInterfaceInvocationHandler
    {
        private IInterfaceInvocationHandler m_RealInvocationHandler;

        /// <summary>
        /// Something to lock on while co-ordinating construction
        /// </summary>
        private readonly object m_Sync = new object();

        /// <summary>
        /// Creates an interceptor for an object that doesn't exist yet.
        /// 
        /// This will trigger its asynchronous construction.
        /// </summary>
        public CreatorInterfaceInvocationHandler(ObjectCreator<IActor> creator, ProxyFactory proxyFactory)
        {
            ThreadPool.QueueUserWorkItem(
                delegate
                    {
                        lock (m_Sync)
                        {
                            IActor newObject = creator();
                            InterfaceInvocationHandler temp = new InterfaceInvocationHandler(newObject, newObject, proxyFactory);

                            // Need to make sure that the ThreaderInterceptor is completely finished being constructed before
                            // assigning it to the field, so that unsynchronised access to it is safe.
                            Thread.MemoryBarrier();
                            m_RealInvocationHandler = temp;
                            Monitor.PulseAll(m_Sync);
                        }
                    });
        }

        public IMethodInvocationHandler GetInvocationHandlerFor(MethodInfo method)
        {
            if (m_RealInvocationHandler == null)
            {
                // Have to wait for it to get made
                lock (m_Sync)
                {
                    while (m_RealInvocationHandler == null)
                    {
                        // Use a timed-out wait to mitigate the missed update problem
                        Monitor.Wait(m_Sync, 50);
                    }
                }
            }

            // Now m_RealInvocationHandler is definitely finished, forward to it
            return m_RealInvocationHandler.GetInvocationHandlerFor(method);
        }
    }
}

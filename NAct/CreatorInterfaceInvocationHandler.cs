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
        private readonly object m_Sync;

        /// <summary>
        /// Creates an interceptor for an object that doesn't exist yet.
        /// 
        /// This will trigger its asynchronous construction.
        /// </summary>
        public CreatorInterfaceInvocationHandler(ObjectCreator<IActor> creator, ProxyFactory proxyFactory)
        {
            m_Sync = new object();
            ThreadPool.QueueUserWorkItem(
                delegate
                {
                    try
                    {
                        lock (m_Sync)
                        {
                            IActor newObject = creator();
                            ActorInterfaceInvocationHandler temp = new ActorInterfaceInvocationHandler(newObject, newObject, proxyFactory);

                            // Need to make sure that the ThreaderInterceptor is completely finished being constructed before
                            // assigning it to the field, so that unsynchronised access to it is safe.
                            Thread.MemoryBarrier();
                            m_RealInvocationHandler = temp;
                            Monitor.PulseAll(m_Sync);
                        }
                    }
                    catch (Exception e)
                    {
                        ExceptionHandling.ExceptionHandler(e);
                    }
                });
        }

        /// <summary>
        /// Creates a proxy for an actor component object and places it in an existing actor
        /// </summary>
        internal CreatorInterfaceInvocationHandler(ObjectCreator<IActorComponent> creator, IActor rootObject, ProxyFactory proxyFactory)
        {
            // We need to lock on the root when using an existing actor
            m_Sync = rootObject;
            ThreadPool.QueueUserWorkItem(
                delegate
                {
                    try
                    {
                        lock (rootObject)
                        {
                            IActorComponent newObject = creator();
                            ActorInterfaceInvocationHandler temp = new ActorInterfaceInvocationHandler(newObject, rootObject, proxyFactory);

                            // Need to make sure that the ThreaderInterceptor is completely finished being constructed before
                            // assigning it to the field, so that unsynchronised access to it is safe.
                            Thread.MemoryBarrier();
                            m_RealInvocationHandler = temp;
                            Monitor.PulseAll(m_Sync);
                        }
                    }
                    catch (Exception e)
                    {
                        ExceptionHandling.ExceptionHandler(e);
                    }
                });
        }

        private void WaitForConstruction()
        {
            if (m_RealInvocationHandler == null)
            {
                // Have to wait for it to get made
                lock (m_Sync)
                {
                    while (m_RealInvocationHandler == null)
                    {
                        // TODO Use a timed-out wait to mitigate the missed update problem
                        Monitor.Wait(m_Sync);
                    }
                }
            }
        }

        public IMethodInvocationHandler GetInvocationHandlerFor(MethodCaller methodCaller)
        {
            WaitForConstruction();

            // Now m_RealInvocationHandler is definitely finished, forward to it
            return m_RealInvocationHandler.GetInvocationHandlerFor(methodCaller);
        }
    }
}

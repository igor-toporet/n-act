using System;
using System.Threading;

namespace NAct
{
    class CreatorInterfaceInvocationHandler : IInterfaceInvocationHandler
    {
        private IInterfaceInvocationHandler m_RealInvocationHandler;

        /// <summary>
        /// Something to wait on while constructing
        /// </summary>
        private readonly ManualResetEvent m_FinishedEvent = new ManualResetEvent(false);

        /// <summary>
        /// Creates an interceptor for an object that doesn't exist yet.
        /// 
        /// This will trigger its asynchronous construction.
        /// </summary>
        public CreatorInterfaceInvocationHandler(ObjectCreator<IActor> creator, ProxyFactory proxyFactory)
        {
            Hooking.BeforeQueueActorCall();

            ThreadPool.QueueUserWorkItem(
                delegate
                {
                    Hooking.ActorCallWrapper(
                        () =>
                        {
                            IActor newObject = creator();
                            ActorInterfaceInvocationHandler temp = new ActorInterfaceInvocationHandler(newObject, newObject, proxyFactory);

                            m_RealInvocationHandler = temp;
                            m_FinishedEvent.Set();
                        });
                });
        }

        /// <summary>
        /// Creates a proxy for an actor component object and places it in an existing actor
        /// </summary>
        internal CreatorInterfaceInvocationHandler(ObjectCreator<IActorComponent> creator, IActor rootObject, ProxyFactory proxyFactory)
        {
            Hooking.BeforeQueueActorCall();

            // We need to lock on the root when using an existing actor
            ThreadPool.QueueUserWorkItem(
                delegate
                {
                    Hooking.ActorCallWrapper(() =>
                    {
                        IActorComponent newObject = creator();
                        ActorInterfaceInvocationHandler temp = new ActorInterfaceInvocationHandler(newObject, rootObject, proxyFactory);

                        m_RealInvocationHandler = temp;
                        m_FinishedEvent.Set();
                    });
                });
        }

        public IMethodInvocationHandler GetInvocationHandlerFor(MethodCaller methodCaller)
        {
            if (!m_FinishedEvent.WaitOne(10000))
            {
                throw new TimeoutException("Object was not constructed fast enough");
            }

            // Now m_RealInvocationHandler is definitely finished, forward to it
            return m_RealInvocationHandler.GetInvocationHandlerFor(methodCaller);
        }
    }
}

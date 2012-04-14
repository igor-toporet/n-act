using System;
using System.Reflection;
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

        public IMethodInvocationHandler GetInvocationHandlerFor(MethodCaller methodCaller, Type returnType, MethodInfo targetMethod)
        {
            if (m_FinishedEvent.WaitOne(0))
            {
                // m_RealInvocationHandler is already finished, forward to it
                return m_RealInvocationHandler.GetInvocationHandlerFor(methodCaller, returnType, targetMethod);
            }
            else
            {
                // It's taking a while to construct, use something that will forward to it once it's finished
                return new CreatorMethodInvocationHandler(this, methodCaller, returnType, targetMethod);
            }
        }

        class CreatorMethodInvocationHandler : IMethodInvocationHandler
        {
            private readonly CreatorInterfaceInvocationHandler m_CreatorInterfaceInvocationHandler;
            private readonly MethodCaller m_MethodCaller;
            private readonly Type m_ReturnType;
            private readonly MethodInfo m_TargetMethod;

            public CreatorMethodInvocationHandler(CreatorInterfaceInvocationHandler creatorInterfaceInvocationHandler, MethodCaller methodCaller, Type returnType, MethodInfo targetMethod)
            {
                m_CreatorInterfaceInvocationHandler = creatorInterfaceInvocationHandler;
                m_MethodCaller = methodCaller;
                m_ReturnType = returnType;
                m_TargetMethod = targetMethod;
            }

            public void InvokeHappened(object[] parameterValues)
            {
                WaitForRealMethodInvocationHandler().InvokeHappened(parameterValues);
            }

            private IMethodInvocationHandler WaitForRealMethodInvocationHandler()
            {
                // Wait for the real invocation handler to finish being contructed
                m_CreatorInterfaceInvocationHandler.m_FinishedEvent.WaitOne();

                IMethodInvocationHandler realMethodInvocationHandler = m_CreatorInterfaceInvocationHandler.m_RealInvocationHandler.GetInvocationHandlerFor(m_MethodCaller, m_ReturnType, m_TargetMethod);
                return realMethodInvocationHandler;
            }

            public object ReturningInvokeHappened(object[] parameterValues)
            {
                return WaitForRealMethodInvocationHandler().ReturningInvokeHappened(parameterValues);
            }
        }
    }
}

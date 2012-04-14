using System;
using System.Reflection;

namespace NAct
{
    class ActorInterfaceInvocationHandler : IInterfaceInvocationHandler
    {
        private readonly object m_Original;
        private readonly IActor m_RootForObject;
        private readonly ProxyFactory m_ProxyFactory;

        public ActorInterfaceInvocationHandler(object original, IActor rootForObject, ProxyFactory proxyFactory)
        {
            m_Original = original;
            m_ProxyFactory = proxyFactory;
            m_RootForObject = rootForObject;
        }

        public IMethodInvocationHandler GetInvocationHandlerFor(MethodCaller methodCaller, Type returnType, MethodInfo targetMethod)
        {

            return new ActorMethodInvocationHandler(m_RootForObject, m_Original, methodCaller, m_ProxyFactory, returnType, targetMethod);
        }
    }
}

using System;
using System.Reflection;

namespace NAct
{
    class AudienceInterfaceInvocationHandler : IInterfaceInvocationHandler
    {        
        private readonly object m_Original;
        private readonly ProxyFactory m_ProxyFactory;

        public AudienceInterfaceInvocationHandler(object original, ProxyFactory proxyFactory)
        {
            m_Original = original;
            m_ProxyFactory = proxyFactory;
        }
        public IMethodInvocationHandler GetInvocationHandlerFor(MethodCaller methodCaller, Type returnType, MethodInfo targetMethod)
        {
            return new AudienceMethodInvocationHandler(m_Original, methodCaller, m_ProxyFactory);
        }
    }
}

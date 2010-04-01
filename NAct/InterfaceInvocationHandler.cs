using System.Reflection;

namespace NAct
{
    class InterfaceInvocationHandler : IInterfaceInvocationHandler
    {
        private readonly object m_Original;
        private readonly IActor m_RootForObject;
        private readonly ProxyFactory m_ProxyFactory;

        public InterfaceInvocationHandler(object original, IActor rootForObject, ProxyFactory proxyFactory)
        {
            m_Original = original;
            m_ProxyFactory = proxyFactory;
            m_RootForObject = rootForObject;
        }

        public IMethodInvocationHandler GetInvocationHandlerFor(MethodInfo method)
        {
            return new ActorMethodInvocationHandler(m_RootForObject, m_Original, method, m_ProxyFactory);
        }
    }
}

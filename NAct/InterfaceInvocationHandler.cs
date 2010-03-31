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
            // TODO Wasteful - create new MethodInvocationHandlers for each invocation
            return new MethodInvocationHandler(m_RootForObject, m_Original, method, m_ProxyFactory);
        }

        public ISubInterfaceMethodInvocationHandler GetSubInterfaceHandlerFor(MethodInfo method)
        {
            // TODO probably also wasteful
            return new SubinterfaceMethodInvocationHandler(m_RootForObject, m_Original, method, m_ProxyFactory);
        }
    }
}

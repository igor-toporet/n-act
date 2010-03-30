using System;
using System.Collections.Generic;
using System.Reflection;

namespace NAct
{
    class InterfaceInvocationHandler : IInterfaceInvocationHandler
    {
        private readonly object m_Original;
        private readonly IActor m_RootForObject;
        private readonly MethodProxyFactory m_MethodProxyFactory;

        public InterfaceInvocationHandler(object original, IActor rootForObject, MethodProxyFactory methodProxyFactory)
        {
            m_Original = original;
            m_MethodProxyFactory = methodProxyFactory;
            m_RootForObject = rootForObject;
        }

        public void InvokeHappened(MethodInfo method, object[] parameterValues)
        {
            // Wasteful - create new MethodInvocationHandlers for each invocation
            IMethodInvocationHandler handler = new MethodInvocationHandler(m_RootForObject, m_Original, method,
                                                                           m_MethodProxyFactory);
            handler.InvokeHappened(parameterValues);
        }
    }
}

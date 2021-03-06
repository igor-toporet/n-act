﻿using System.Reflection;

namespace NAct
{
    class AudienceMethodInvocationHandler : MethodInvocationHandler
    {
        public AudienceMethodInvocationHandler(object wrapped, MethodCaller methodCaller, ProxyFactory proxyFactory)
            : base(proxyFactory, methodCaller, wrapped)
        {
        }

        public override void InvokeHappened(object[] parameterValues)
        {
            // A method has been called on the proxy
            ConvertParameters(parameterValues);

            // No thread switching, just plough ahead and call the method. Hope it's thread safe
            base.InvokeHappened(parameterValues);
        }

        public override object ReturningInvokeHappened(object[] parameterValues)
        {
            ConvertParameters(parameterValues);

            return base.ReturningInvokeHappened(parameterValues);
        }
    }
}

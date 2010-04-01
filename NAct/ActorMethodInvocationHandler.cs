using System;
using System.Threading;

namespace NAct
{
    class ActorMethodInvocationHandler : MethodInvocationHandler
    {
        private readonly IActor m_Root;
        private readonly ProxyFactory m_ProxyFactory;

        public ActorMethodInvocationHandler(IActor root, object wrapped, MethodCaller methodCaller, ProxyFactory proxyFactory)
            : base (proxyFactory, methodCaller, wrapped)
        {
            m_Root = root;
            m_ProxyFactory = proxyFactory;
        }

        public override void InvokeHappened(object[] parameterValues)
        {
            // A method has been called on the proxy
            ConvertParameters(parameterValues);

            if (IsWinformsControl(m_Root))
            {
                // It's a winforms control, use reflection to call begininvoke on it
                m_Root.GetType().GetMethod("BeginInvoke", new[] { typeof(Delegate)}).Invoke(
                    m_Root,
                    new object[]
                        {
                            (Action) delegate
                                         {
                                             BaseInvokeHappened(parameterValues);
                                         }
                        });
            }
            else
            {
                // Just a standard actor - add the task to the work queue
                ThreadPool.QueueUserWorkItem(
                    delegate
                        {
                            lock (m_Root)
                            {
                                BaseInvokeHappened(parameterValues);
                            }
                        });
            }
        }

        /// <summary>
        ///  I do this in a helper method to keep the code verifiable.
        /// http://stackoverflow.com/questions/405379/what-is-unverifiable-code-and-why-is-it-bad
        /// </summary>
        private void BaseInvokeHappened(object[] parameterValues)
        {
            base.InvokeHappened(parameterValues);
        }

        private static bool IsWinformsControl(object obj)
        {
            // Climb up through base classes to find Control
            foreach (Type eachInterface in obj.GetType().GetInterfaces())
            {
                if (eachInterface.FullName == "System.ComponentModel.ISynchronizeInvoke")
                {
                    return true;
                }
            }

            return false;
        }

        public override object ReturningInvokeHappened(object[] parameterValues)
        {
            // This is only allowed if the returning method gets a IActorCompoment

            // TODO Use a CIIH or something to run the getter method asynchronously in the root actor's thread
            //CreatorInterfaceInvocationHandler creatorInvocationHandler = new CreatorInterfaceInvocationHandler(
            //    () => (IActorComponent)m_MethodBeingProxied.Invoke(m_Wrapped, parameterValues), m_Root, m_ProxyFactory);

            object subInterfaceObject = base.ReturningInvokeHappened(parameterValues);

            // Find the object's interface which implements IActor (it might have others, but this is the important one
            Type interfaceType = null;
            foreach (Type eachInterface in subInterfaceObject.GetType().GetInterfaces())
            {
                if (typeof(IActorComponent).IsAssignableFrom(eachInterface))
                {
                    interfaceType = eachInterface;
                    break;
                }
            }

            ActorInterfaceInvocationHandler invocationHandler = new ActorInterfaceInvocationHandler(subInterfaceObject, m_Root, m_ProxyFactory);

            return m_ProxyFactory.CreateInterfaceProxy(invocationHandler, interfaceType, true);
        }
    }
}

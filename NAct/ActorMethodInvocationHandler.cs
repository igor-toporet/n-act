using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using NAct.Utils;

namespace NAct
{
    class ActorMethodInvocationHandler : MethodInvocationHandler
    {
        private static readonly Dictionary<IActor, Queue<Action>> s_JobQueues = new Dictionary<IActor, Queue<Action>>(); 

        private readonly IActor m_Root;
        private readonly MethodCaller m_MethodCaller;
        private readonly ProxyFactory m_ProxyFactory;
        private readonly MethodInfo m_TargetMethod;

        // This will be accessed in a thread-unsafe way. I believe the worst that can happen is calculating it twice.
        private bool? m_RootIsControl;
        private bool? m_RootIsWPFControl;

        public ActorMethodInvocationHandler(IActor root, object wrapped, MethodCaller methodCaller, ProxyFactory proxyFactory, Type returnType, MethodInfo targetMethod)
            : base(proxyFactory, methodCaller, wrapped)
        {
            m_Root = root;
            m_MethodCaller = methodCaller;
            m_ProxyFactory = proxyFactory;
            m_TargetMethod = targetMethod;
        }

        public override void InvokeHappened(object[] parameterValues)
        {
            // A method has been called on the proxy
            Hooking.BeforeActorCallQueued(m_Root.GetType(), m_TargetMethod, parameterValues);

            ConvertParameters(parameterValues);

            if (!m_RootIsWPFControl.HasValue)
            {
                // Find whether this actor is a wpf control
                m_RootIsWPFControl = IsWPFControl(m_Root);
                if (m_RootIsWPFControl.Value) m_RootIsControl = true;
            }

            if (!m_RootIsControl.HasValue)
            {
                // Find whether this actor is a winforms control
                m_RootIsControl = IsWinformsControl(m_Root);
            }

            Queue<Action> queueForThisObject;
            lock(s_JobQueues)
            {
                if ( ! s_JobQueues.TryGetValue(m_Root, out queueForThisObject) )
                {
                    queueForThisObject = new Queue<Action>();
                    s_JobQueues[m_Root] = queueForThisObject;
                }
            }

            lock(queueForThisObject)
            {
                queueForThisObject.Enqueue(() => BaseInvokeHappened(parameterValues));
            }

            if (m_RootIsControl.Value)
            {
                // It's a control, use reflection to call begininvoke on it
                object dispatcher;
                if (m_RootIsWPFControl.Value)
                {
                    // It's a wpf control, use reflection to get its dispatcher to call begininvoke on that
                    dispatcher = m_Root.GetType().GetProperty("Dispatcher").GetGetMethod().Invoke(m_Root, new object[0]);
                }
                else
                {
                    // Winforms controls are their own dispatcher
                    dispatcher = m_Root;
                }

                dispatcher.GetType().GetMethod("BeginInvoke", new[] { typeof(Delegate), typeof(object[]) }).Invoke(
                    dispatcher,
                    new object[]
                        {
                            (Action) (() => Hooking.ActorCallWrapper(() => RunNextQueueItem(m_Root, queueForThisObject))),
                            new object[0]
                        });
            }
            else
            {
                // Just a standard actor - add the task to the work queue
                ThreadPool.QueueUserWorkItem(
                    delegate
                    {
                        Hooking.ActorCallWrapper(() =>
                                         {
                                             lock (m_Root)
                                             {
                                                 RunNextQueueItem(m_Root, queueForThisObject);
                                             }
                                         });
                    });
            }
        }

        private void RunNextQueueItem(IActor actor, Queue<Action> queueForThisObject)
        {
            Hooking.BeforeActorMethodRun(m_Root.GetType(), m_TargetMethod);

            Action action;
            lock(queueForThisObject)
            {
                if(queueForThisObject.Count > 0)
                {
                    action = queueForThisObject.Dequeue();
                }
                else
                {
                    action = null;
                }
                if(queueForThisObject.Count == 0)
                {
                    lock(s_JobQueues)
                    {
                        // Remove from the dictionary so we do not stop the actor being garbage collected
                        s_JobQueues.Remove(actor);
                    }
                }
            }
            if (action != null)
            {
                action();
            }
        }

        /// <summary>
        ///  I do this in a helper method to keep the code verifiable.
        /// http://stackoverflow.com/questions/405379/what-is-unverifiable-code-and-why-is-it-bad
        /// </summary
        /// >
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

        private static bool IsWPFControl(object obj)
        {
            // Climb up through base classes to find DispatcherObject
            Type eachBaseClass = obj.GetType();
            while (eachBaseClass != null)
            {
                if (eachBaseClass.FullName == "System.Windows.Threading.DispatcherObject")
                {
                    return true;
                }

                eachBaseClass = eachBaseClass.BaseType;
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

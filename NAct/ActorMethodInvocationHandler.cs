using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NAct
{
    class ActorMethodInvocationHandler : MethodInvocationHandler
    {
        private static readonly Dictionary<IActor, Queue<Action>> s_JobQueues = new Dictionary<IActor, Queue<Action>>();

        private readonly IActor m_Root;
        private readonly ProxyFactory m_ProxyFactory;
        private readonly Type m_ReturnType;

        // This will be accessed in a thread-unsafe way. I believe the worst that can happen is calculating it twice.
        private bool? m_RootIsControl;
        private bool? m_RootIsWPFControl;

        public ActorMethodInvocationHandler(IActor root, object wrapped, MethodCaller methodCaller, ProxyFactory proxyFactory, Type returnType)
            : base(proxyFactory, methodCaller, wrapped)
        {
            m_Root = root;
            m_ProxyFactory = proxyFactory;
            m_ReturnType = returnType;
        }

        public override void InvokeHappened(object[] parameterValues)
        {
            // A method has been called on the proxy
            ConvertParameters(parameterValues);

            DoInRightThread(() => CallTheVoidMethod(parameterValues));
        }

        private void DoInRightThread(Action action)
        {
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

            Hooking.BeforeQueueActorCall();

            Queue<Action> queueForThisObject;
            lock (s_JobQueues)
            {
                if (!s_JobQueues.TryGetValue(m_Root, out queueForThisObject))
                {
                    queueForThisObject = new Queue<Action>();
                    s_JobQueues[m_Root] = queueForThisObject;
                }
            }

            lock (queueForThisObject)
            {
                queueForThisObject.Enqueue(action);
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
            // Set the SyncronizationContext in case we end up awaiting a Task
            SynchronizationContext.SetSynchronizationContext(new ActorSynchronizationContext(DoInRightThread));

            Action action;
            lock (queueForThisObject)
            {
                if (queueForThisObject.Count > 0)
                {
                    action = queueForThisObject.Dequeue();
                }
                else
                {
                    action = null;
                }
                if (queueForThisObject.Count == 0)
                {
                    lock (s_JobQueues)
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
        private void CallTheVoidMethod(object[] parameterValues)
        {
            base.InvokeHappened(parameterValues);
        }

        // Similarly
        private object CallTheReturningMethod(object[] parameterValues)
        {
            return base.ReturningInvokeHappened(parameterValues);
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
            ConvertParameters(parameterValues);

            if (m_ReturnType != typeof(Task) && m_ReturnType != typeof(Task<>))
            {
                // Sub-actor case
                // This is only allowed if the method returns a IActorCompoment

                // TODO Use a CIIH or something to run the getter method asynchronously in the root actor's thread
                //CreatorInterfaceInvocationHandler creatorInvocationHandler = new CreatorInterfaceInvocationHandler(
                //    () => (IActorComponent)m_MethodBeingProxied.Invoke(m_Wrapped, parameterValues), m_Root, m_ProxyFactory);

                object subInterfaceObject = base.ReturningInvokeHappened(parameterValues);

                // Find the object's interface which implements IActor
                Type interfaceType = GetImplementedActorInterface(subInterfaceObject);

                ActorInterfaceInvocationHandler invocationHandler = new ActorInterfaceInvocationHandler(subInterfaceObject, m_Root, m_ProxyFactory);

                return m_ProxyFactory.CreateInterfaceProxy(invocationHandler, interfaceType, true);
            }
            else
            {
                // We are returning a Task
                return CreateMethodCallerTask(parameterValues);
            }
        }

        private async Task CreateMethodCallerTask(object[] parameterValues)
        {
            Future future = new Future();

            // Switch thread to do the method
            DoInRightThread(
                    async () =>
                    {
                        // Call the method (which might only half-do itself)
                        Task resultTask = (Task)CallTheReturningMethod(parameterValues);

                        // Don't want to switch to our SynchronizationContext on return, our caller will switch to their one in a sec anyway
                        resultTask.ConfigureAwait(false);

                        await resultTask;

                        // Now the method is completely finished, put its return value in the builder, causing the caller to get called back
                        future.Complete();
                    });

            // And wait for it all to finish, thereby causing this method to become the appropriate Task
            await future;
        }
    }
}

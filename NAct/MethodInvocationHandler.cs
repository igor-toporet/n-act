using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;

namespace NAct
{
    /// <summary>
    /// Handles the invocation of a single method
    /// </summary>
    class MethodInvocationHandler : IMethodInvocationHandler
    {
        private readonly IActor m_Root;
        private readonly object m_Wrapped;
        private readonly MethodInfo m_MethodBeingProxied;
        private readonly ProxyFactory m_ProxyFactory;

        public MethodInvocationHandler(IActor root, object wrapped, MethodInfo methodBeingProxied, ProxyFactory proxyFactory)
        {
            m_Root = root;
            m_ProxyFactory = proxyFactory;
            m_MethodBeingProxied = methodBeingProxied;
            m_Wrapped = wrapped;
        }

        /// <summary>
        /// Finds the root object of an actor that an object is part of, or null if there isn't one.
        /// </summary>
        /// <param name="anObject">The object that might be part of an actor</param>
        /// <returns>The actor to which it corresponds, or null if there is none</returns>
        private static IActor RootForObject(object anObject)
        {
            IActor objectAsActor = anObject as IActor;
            if (objectAsActor != null)
            {
                // The parameter itself is an actor, give that
                return objectAsActor;
            }

            Delegate objectAsDelegate = anObject as Delegate;
            if (objectAsDelegate != null && objectAsDelegate.Target != null)
            {
                // The parameter is a delegate, see if its target is in an actor
                IActor targetRoot = RootForObject(objectAsDelegate.Target);

                if (targetRoot != null)
                {
                    return targetRoot;
                }

                foreach (FieldInfo eachField in objectAsDelegate.Target.GetType().GetFields())
                {
                    if (typeof(IActor).IsAssignableFrom(eachField.FieldType))
                    {
                        // This field is an actor, give that
                        return (IActor)eachField.GetValue(objectAsDelegate.Target);
                    }
                }

                // None of the fields are actors, see whether we have any fields of our containing type which might in turn be part of an actor
                return RootInNestedTypeField(objectAsDelegate.Target);
            }

            // The object isn't an actor or a delegate, but maybe it has the parent actor in a field
            // (anonymous delegates do this often)
            // Disallow for now, as we can't guarantee there's a suitable interface
            //Type type = anObject.GetType();

            //foreach (FieldInfo eachField in type.GetFields())
            //{
            //    if (eachField.FieldType.IsSubclassOf(typeof(IActor)))
            //    {
            //        // This field is an actor, give that
            //        return (IActor)eachField.GetValue(anObject);
            //    }
            //}

            // No joy, is probably just a random (immutable I hope) variable
            return null;
        }

        /// <summary>
        /// Searches the fields of anObject for one that is of it's declaring type
        /// </summary>
        /// <param name="anObject"></param>
        /// <returns></returns>
        private static IActor RootInNestedTypeField(object anObject)
        {
            IActor objectAsActor = anObject as IActor;
            if (objectAsActor != null)
            {
                // The parameter itself is an actor, give that
                return objectAsActor;
            }

            Type type = anObject.GetType();
            foreach (FieldInfo eachField in type.GetFields())
            {
                if (eachField.FieldType == type.DeclaringType)
                {
                    // The field is the type of our declaring type, search the field's contents for actors
                    IActor possibleRoot = RootInNestedTypeField(eachField.GetValue(anObject));
                    if (possibleRoot != null)
                    {
                        return possibleRoot;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Checks an object for being a callback, and proxies it to move to the right thread if it is
        /// </summary>
        /// <param name="original">The original object that's might be a callback</param>
        /// <returns>The object that should now be the parameter, either just original, or a proxy for it if it was a callback</returns>
        private object ConvertParameter(object original)
        {
            // See if this parameter is, or is in, an actor
            IActor rootForObject = RootForObject(original);

            if (rootForObject != null)
            {
                Delegate originalAsDelegate = original as Delegate;
                if (originalAsDelegate != null)
                {
                    // Special case for delegates: make a new delegate that calls the existing one in the right thread
                    MethodInvocationHandler methodInvocationHandler = new MethodInvocationHandler(rootForObject, originalAsDelegate.Target, originalAsDelegate.Method, m_ProxyFactory);
                    return m_ProxyFactory.CreateMethodProxy(methodInvocationHandler, originalAsDelegate.Method, original.GetType());
                }
                else
                {
                    // Yep, this object needs to be wrapped to move back to its actor's logical thread when it's used
                    InterfaceInvocationHandler callbackInterceptor = new InterfaceInvocationHandler(original, rootForObject, m_ProxyFactory);

                    // Find the object's interface which implements IActor (it might have others, but this is the important one
                    Type interfaceType = null;
                    foreach (Type eachInterface in original.GetType().GetInterfaces())
                    {
                        if (typeof(IActor).IsAssignableFrom(eachInterface))
                        {
                            interfaceType = eachInterface;
                            break;
                        }
                    }

                    if (interfaceType == null)
                    {
                        throw new ApplicationException("NAct encountered an internal inconsistency and will eat your cake.");
                    }

                    return m_ProxyFactory.CreateInterfaceProxy(callbackInterceptor, interfaceType);
                }
            }

            return original;
        }

        public void InvokeHappened(object[] parameterValues)
        {
            // A method has been called on the proxy
            // Detect event signups, and callback arguments
            for (int i = 0; i < parameterValues.Length; i++)
            {
                object eachParameter = parameterValues[i];
                if (eachParameter != null)
                {
                    parameterValues[i] = ConvertParameter(eachParameter);
                }
            }

            if (IsWinformsControl(m_Root))
            {
                // It's a winforms control, use reflection to call begininvoke on it
                m_Root.GetType().GetMethod("BeginInvoke", new Type[] { typeof(Delegate)}).Invoke(
                    m_Root,
                    new object[]
                        {
                            (Action) delegate
                                         {
                                             m_MethodBeingProxied.Invoke(m_Wrapped, parameterValues);
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
                                m_MethodBeingProxied.Invoke(m_Wrapped, parameterValues);
                            }
                        });
            }
        }

        private bool IsWinformsControl(object obj)
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
    }
}

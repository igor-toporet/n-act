using System;
using System.Reflection;

namespace NAct
{
    /// <summary>
    /// Handles the invocation of a single method
    /// </summary>
    abstract class MethodInvocationHandler : IMethodInvocationHandler
    {
        private readonly ProxyFactory m_ProxyFactory;
        private readonly MethodCaller m_MethodCaller;
        private readonly object m_Wrapped;

        protected MethodInvocationHandler(ProxyFactory proxyFactory, MethodCaller methodCaller, object wrapped)
        {
            m_ProxyFactory = proxyFactory;
            m_MethodCaller = methodCaller;
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

            // No joy, is probably just a random (immutable I hope) variable
            return null;
        }

        /// <summary>
        /// Searches the fields of anObject for one that is of it's declaring type
        /// </summary>
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
        /// <param name="original">The original object that might be a callback</param>
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
                    if (originalAsDelegate.Method.ReturnType != typeof(void))
                    {
                        // The method has a return type, fail fast
                        throw new InvalidOperationException("The delegate " + originalAsDelegate.GetType() +
                                                            " has a non-void return type. Actors may only be given callbacks with void return types.");
                    }
                    MethodCaller delegateMethodCaller = m_ProxyFactory.CreateDelegateCaller(originalAsDelegate.GetType(), originalAsDelegate.Method);
                    ActorMethodInvocationHandler methodInvocationHandler = new ActorMethodInvocationHandler(rootForObject, originalAsDelegate, delegateMethodCaller, m_ProxyFactory);
                    return m_ProxyFactory.CreateDelegateProxy(methodInvocationHandler, originalAsDelegate.Method, original.GetType());
                }
                else
                {
                    // Yep, this object needs to be wrapped to move back to its actor's logical thread when it's used
                    ActorInterfaceInvocationHandler callbackInterceptor = new ActorInterfaceInvocationHandler(original, rootForObject, m_ProxyFactory);

                    // Find the object's interface which implements IActor (it might have others, but this is the important one)
                    Type interfaceType = null;
                    foreach (Type eachInterface in original.GetType().GetInterfaces())
                    {
                        if (typeof(IActor).IsAssignableFrom(eachInterface) && !eachInterface.Equals(typeof(IActor)))
                        {
                            interfaceType = eachInterface;
                            break;
                        }
                    }

                    if (interfaceType == null)
                    {
                        throw new ApplicationException("NAct encountered an internal inconsistency and will eat your cake.");
                    }

                    return m_ProxyFactory.CreateInterfaceProxy(callbackInterceptor, interfaceType, true);
                }
            }

            return original;
        }

        /// <summary>
        /// Checks each object for being a callback, and proxies it to move to the right thread if it is
        /// </summary>
        /// <param name="parameterValues">The original parameters that might be callbacks. Modified in place to give converted parameters on return.</param>
        protected void ConvertParameters(object [] parameterValues)
        {
            // Detect event signups, and callback arguments
            for (int i = 0; i < parameterValues.Length; i++)
            {
                object eachParameter = parameterValues[i];
                if (eachParameter != null)
                {
                    parameterValues[i] = ConvertParameter(eachParameter);
                }
            }
        }

        public virtual void InvokeHappened(object[] parameterValues)
        {
            m_MethodCaller.CallMethod(m_Wrapped, parameterValues);
        }

        public virtual object ReturningInvokeHappened(object[] parameterValues)
        {
            return m_MethodCaller.CallReturningMethod(m_Wrapped, parameterValues);
        }
    }
}

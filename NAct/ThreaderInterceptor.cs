using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using Castle.Core.Interceptor;
using Castle.DynamicProxy;

namespace NAct
{
    class ThreaderInterceptor : IInterceptor
    {
        /// <summary>
        /// The root object (the one that implements the same interface
        /// </summary>
        private readonly IActor m_Root;
        private readonly object m_Wrapped;

        /// <summary>
        /// Creates an interceptor for an object that already exists.
        /// </summary>
        public ThreaderInterceptor(object wrapped, IActor root)
        {
            m_Wrapped = wrapped;
            m_Root = root;
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
            if (objectAsDelegate != null)
            {
                // The parameter is a delegate, see if its target is in an actor
                IActor targetRoot = RootForObject(objectAsDelegate.Target);

                if (targetRoot != null)
                {
                    return targetRoot;
                }
            }

            // The object isn't an actor or a delegate, but maybe it has the parent actor in a field
            // (anonymous delegates do this often)
            Type type = anObject.GetType();

            foreach (FieldInfo eachField in type.GetFields())
            {
                if (eachField.FieldType.IsSubclassOf(typeof(IActor)))
                {
                    // This field is an actor, give that
                    return (IActor) eachField.GetValue(anObject);
                }
            }

            // No joy, is probably just a random (immutable I hope) variable
            return null;
        }

        /// <summary>
        /// Checks an object for being a callback, and proxies it to move to the right thread if it is
        /// </summary>
        /// <param name="original">The original object that's might be a callback</param>
        /// <param name="parameterType">The static type of the parameter in which that object sits</param>
        /// <returns>The object that should now be the parameter, either just original, or a proxy for it if it was a callback</returns>
        private static object ConvertParameter(object original, Type parameterType)
        {
            // See if this parameter is, or is in, an actor
            IActor rootForObject = RootForObject(original);

            if (rootForObject != null)
            {
                Delegate originalAsDelegate = original as Delegate;
                if (originalAsDelegate != null)
                {
                    // Special case for delegates: make a new delegate that calls the existing one in the right thread
                    
                }
                else
                {
                    // Yep, this object needs to be wrapped to move back to its actor's logical thread when it's used
                    ThreaderInterceptor callbackInterceptor = new ThreaderInterceptor(original, rootForObject);
                    return new ProxyGenerator().CreateInterfaceProxyWithoutTarget(parameterType, callbackInterceptor);
                }
            }

            return original;
        }

        public void Intercept(IInvocation invocation)
        {
            // A method has been called on the proxy
    		// Detect event signups, and callback arguments
            ParameterInfo[] parameterTypes = invocation.Method.GetParameters();
            for (int i = 0; i < parameterTypes.Length; i++)
            {
                ParameterInfo eachParameterType = parameterTypes[i];
                object eachParameter = invocation.Arguments[i];
                if (eachParameter != null)
                {
                    invocation.Arguments[i] = ConvertParameter(eachParameter, eachParameterType.ParameterType);
                }
    		}

    		// Get the information from the current thread about what's going on in the stack, to give to the new thread
    		//final SavedStackState stackState = SavedStackState.currentState();

    		// Add the task to the work queue
            ThreadPool.QueueUserWorkItem(
                delegate
                    {
                        lock (m_Root)
                        {
                            invocation.Method.Invoke(m_Wrapped, invocation.Arguments);
                        }
                    });
        }
    }
}

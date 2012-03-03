using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using NAct.Utils;

namespace NAct
{
    public class ProxyFactory
    {
        private const string c_FieldNameForInvocationHandler = "s_InvocationHandler";
        private const string c_DelegateMethodName = "ProxyMethod";

        private readonly MethodInfo m_InvokeHappenedMethod = typeof(IMethodInvocationHandler).GetMethod("InvokeHappened");
        private readonly MethodInfo m_ReturningInvokeHappenedMethod = typeof(IMethodInvocationHandler).GetMethod("ReturningInvokeHappened");
        private readonly AssemblyBuilder m_DynamicAssembly = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName("NActDynamicAssembly"), AssemblyBuilderAccess.RunAndSave);
        private readonly ModuleBuilder m_DynamicModule;

        private readonly object m_Sync = new object();

        private readonly IDictionary<Type, Type> m_InterfaceProxyCache = new Dictionary<Type, Type>();
        private readonly IDictionary<Type, Type> m_DelegateProxyCache = new Dictionary<Type, Type>();
        private readonly IDictionary<Type, MethodCaller> m_DelegateCallerCache = new Dictionary<Type, MethodCaller>();
        private readonly IDictionary<MethodInfo, MethodCaller> m_MethodCallerCache = new Dictionary<MethodInfo, MethodCaller>();

        private int m_TypeIndex = 0;

        public ProxyFactory()
        {
            m_DynamicModule = m_DynamicAssembly.DefineDynamicModule("dynamic.dll", "dynamic.dll");
        }

        private TypeBuilder GetFreshType()
        {
            lock (m_Sync)
            {
                m_TypeIndex++;
                return m_DynamicModule.DefineType("DynamicType" + m_TypeIndex);
            }
        }

        /// <summary>
        /// Creates something that implements the given interface, and forwards all calls to the invocationHandler
        /// </summary>
        public object CreateInterfaceProxy(IInterfaceInvocationHandler invocationHandler, Type interfaceType, bool throwOnNonActorMethod)
        {
            if (!interfaceType.IsInterface)
            {
                // Only allowing interfaces for the moment, fail fast
                throw new InvalidOperationException("The type " + interfaceType +
                                                    " is not an interface, so an NAct proxy cannot be created for it.");
            }

            if (interfaceType.IsNotPublic)
            {
                throw new InvalidOperationException("The interface " + interfaceType +
                                                    " is not public, so an NAct proxy cannot be created for it.");
            }

            Type proxyType;
            lock (m_Sync)
            {
                m_InterfaceProxyCache.TryGetValue(interfaceType, out proxyType);
            }

            if (proxyType == null)
            {
                TypeBuilder typeBuilder = GetFreshType();

                ForEveryMethodIncludingSuperInterfaces(
                    interfaceType,
                    delegate(MethodInfo eachMethod)
                    {
                        if (throwOnNonActorMethod && eachMethod.ReturnType != typeof(void) &&
                            !typeof(IActorComponent).IsAssignableFrom(eachMethod.ReturnType) &&
                            typeof(Task) != eachMethod.ReturnType &&
                            typeof(Task<>) != eachMethod.ReturnType)
                        {
                            // The method has a return type which isn't a sub-actor or Task, fail fast
                            throw new InvalidOperationException("The interface " + interfaceType +
                                                                " contains the method " +
                                                                eachMethod +
                                                                " which has a non-void return type. Actors may only have methods with void return types.");
                        }

                        Type[] parameterTypes = GetParameterTypes(eachMethod);
                        MethodBuilder methodBuilder = typeBuilder.DefineMethod(eachMethod.Name,
                                                                               eachMethod.Attributes &
                                                                               ~MethodAttributes.Abstract,
                                                                               eachMethod.ReturnType, parameterTypes);

                        if (eachMethod.ReturnType == typeof(void))
                        {
                            // This is an asynchronous call, use the appropriate IMethodInvocationHandler to move it to the right thread
                            // Create a field in which to put the IMethodInvocationHandler
                            FieldBuilder invocationHandlerField =
                                typeBuilder.DefineField(InvocationHandlerNameForMethod(eachMethod),
                                                        typeof(IMethodInvocationHandler),
                                                        FieldAttributes.Private);
                            BuildForwarderMethod(methodBuilder, parameterTypes, m_InvokeHappenedMethod,
                                                 invocationHandlerField);
                        }
                        else
                        {
                            // This is a request for a subinterface - create a method that will return a proxied version of it
                            FieldBuilder invocationHandlerField =
                                typeBuilder.DefineField(InvocationHandlerNameForMethod(eachMethod),
                                                        typeof(IMethodInvocationHandler),
                                                        FieldAttributes.Private);
                            BuildForwarderMethod(methodBuilder, parameterTypes, m_ReturningInvokeHappenedMethod,
                                                 invocationHandlerField);
                        }
                    });

                typeBuilder.AddInterfaceImplementation(interfaceType);

                // Finalise the type
                proxyType = typeBuilder.CreateType();

                // Save it in the cache (this may have raced with another thread, worst that can happen is an unused extra type is created)
                lock (m_Sync)
                {
                    m_InterfaceProxyCache[interfaceType] = proxyType;
                }
            }

            object proxyInstance = Activator.CreateInstance(proxyType);

            // Now we can write all the invocation handlers
            ForEveryMethodIncludingSuperInterfaces(
                interfaceType,
                delegate(MethodInfo eachMethod)
                {
                    FieldInfo writeableInvocationHandlerField =
                        proxyType.GetField(InvocationHandlerNameForMethod(eachMethod), BindingFlags.NonPublic | BindingFlags.Instance);

                    MethodCaller methodCaller = CreateMethodCaller(eachMethod);

                    writeableInvocationHandlerField.SetValue(proxyInstance,
                                                             invocationHandler.GetInvocationHandlerFor(
                                                                 methodCaller, eachMethod.ReturnType));
                });

            return proxyInstance;
        }

        private static void ForEveryMethodIncludingSuperInterfaces(Type interfaceType, Action<MethodInfo> todo)
        {
            foreach (Type eachSuperInterface in interfaceType.GetInterfaces())
            {
                foreach (MethodInfo eachMethod in eachSuperInterface.GetMethods())
                {
                    todo(eachMethod);
                }
            }

            foreach (MethodInfo eachMethod in interfaceType.GetMethods())
            {
                todo(eachMethod);
            }
        }

        private static string InvocationHandlerNameForMethod(MethodInfo method)
        {
            // TODO disambiguate methods with the same name
            return c_FieldNameForInvocationHandler + method.DeclaringType.Name + method.Name;
        }

        /// <summary>
        /// Creates a new method that has the same signature as an existing one, but just calls a methodInvocationHandler when it's called.
        /// </summary>
        /// <param name="methodInvocationHandler">The thing that handles the call</param>
        /// <param name="signature">A method whose signature we want to duplicate</param>
        /// <param name="delegateType">The specific type of the delegate you'd like to create (it's safe to cast the return value to this type)</param>
        public Delegate CreateDelegateProxy(IMethodInvocationHandler methodInvocationHandler, MethodInfo signature, Type delegateType)
        {
            Type proxyType;
            lock (m_Sync)
            {
                m_DelegateProxyCache.TryGetValue(delegateType, out proxyType);
            }

            if (proxyType == null)
            {
                TypeBuilder typeBuilder = GetFreshType();

                // Create a field in which to put the IMethodInvocationHandler
                FieldBuilder invocationHandlerField = typeBuilder.DefineField(c_FieldNameForInvocationHandler,
                                                                              typeof(IMethodInvocationHandler),
                                                                              FieldAttributes.Private);
                Type[] parameterTypes = GetParameterTypes(signature);
                MethodBuilder methodBuilder = typeBuilder.DefineMethod(c_DelegateMethodName, MethodAttributes.Public,
                                                                       signature.ReturnType, parameterTypes);
                BuildForwarderMethod(methodBuilder, parameterTypes, m_InvokeHappenedMethod, invocationHandlerField);

                // Finalise the type
                proxyType = typeBuilder.CreateType();

                // Cache it
                lock (m_Sync)
                {
                    m_DelegateProxyCache[delegateType] = proxyType;
                }
            }

            object proxyInstance = Activator.CreateInstance(proxyType);

            // Put the invocation handler in a field
            FieldInfo writeableInvocationHandlerField = proxyType.GetField(c_FieldNameForInvocationHandler, BindingFlags.Instance | BindingFlags.NonPublic);
            writeableInvocationHandlerField.SetValue(proxyInstance, methodInvocationHandler);

            return Delegate.CreateDelegate(delegateType, proxyInstance, proxyType.GetMethod(c_DelegateMethodName));
        }

        /// <summary>
        /// Creates something that, when given a delegate (compatible with the signature given here) and some parameters, will run it.
        /// </summary>
        public MethodCaller CreateDelegateCaller(Type delegateType, MethodInfo delegateSignature)
        {
            // Check the cache
            lock (m_Sync)
            {
                MethodCaller cachedCaller;
                if (m_DelegateCallerCache.TryGetValue(delegateType, out cachedCaller))
                {
                    return cachedCaller;
                }
            }

            Action<object, object[]> caller = (Action<object, object[]>)CreateDelegateCallerDelegate(delegateType, delegateSignature, typeof(Action<object, object[]>), typeof(void));
            Func<object, object[], object> returningCaller = (Func<object, object[], object>)CreateDelegateCallerDelegate(delegateType, delegateSignature, typeof(Func<object, object[], object>), typeof(object));

            MethodCaller methodCaller = new MethodCaller(caller, returningCaller);

            lock (m_Sync)
            {
                m_DelegateCallerCache[delegateType] = methodCaller;
            }

            return methodCaller;
        }

        private Delegate CreateDelegateCallerDelegate(Type targetDelegateType, MethodInfo targetDelegateSignature, Type shimDelegateType, Type returnType)
        {
            TypeBuilder typeBuilder = GetFreshType();
            MethodBuilder methodBuilder = typeBuilder.DefineMethod(c_DelegateMethodName, MethodAttributes.Public | MethodAttributes.Static, returnType, new[] { typeof(object), typeof(object[]) });
            ILGenerator ilGenerator = methodBuilder.GetILGenerator();

            // Load the target delegate
            ilGenerator.Emit(OpCodes.Ldarg, (short)0);
            ilGenerator.Emit(OpCodes.Castclass, targetDelegateType);

            // Suck each parameter out of the array
            ParameterInfo[] parameterInfos = targetDelegateSignature.GetParameters();
            for (int i = 0; i < parameterInfos.Length; i++)
            {
                // Load the array
                ilGenerator.Emit(OpCodes.Ldarg, (short)1);

                // Push the array index
                ilGenerator.Emit(OpCodes.Ldc_I4, i);

                // Get the parameter value from the array
                ilGenerator.Emit(OpCodes.Ldelem_Ref);

                Type parameterType = parameterInfos[i].ParameterType;
                if (parameterType.IsValueType)
                {
                    // The parameter is a value type - unbox it
                    ilGenerator.Emit(OpCodes.Unbox_Any, parameterType);
                }
                else
                {
                    // The parameter is a reference type, cast the object to it
                    ilGenerator.Emit(OpCodes.Castclass, parameterType);
                }
            }

            // Make the call
            MethodInfo delegateInvoke = targetDelegateType.GetMethod("Invoke");
            ilGenerator.Emit(OpCodes.Callvirt, delegateInvoke);

            // And the ret
            ilGenerator.Emit(OpCodes.Ret);

            // Finalise the type
            Type createdType = typeBuilder.CreateType();

            return Delegate.CreateDelegate(shimDelegateType, createdType.GetMethod(c_DelegateMethodName));
        }

        /// <summary>
        /// Creates something that takes a target and a parameters array, and will call the given method, having unpacked its arguments from the array.
        /// </summary>
        public MethodCaller CreateMethodCaller(MethodInfo methodToCall)
        {
            lock (m_Sync)
            {
                MethodCaller cachedCaller;
                if (m_MethodCallerCache.TryGetValue(methodToCall, out cachedCaller))
                {
                    return cachedCaller;
                }
            }

            Action<object, object[]> caller = (Action<object, object[]>)CreateCallerDelegate(methodToCall, typeof(Action<object, object[]>), typeof(void));
            Func<object, object[], object> returningCaller = (Func<object, object[], object>)CreateCallerDelegate(methodToCall, typeof(Func<object, object[], object>), typeof(object));

            MethodCaller methodCaller = new MethodCaller(caller, returningCaller);

            lock (m_Sync)
            {
                m_MethodCallerCache[methodToCall] = methodCaller;
            }

            return methodCaller;
        }

        private Delegate CreateCallerDelegate(MethodInfo methodToCall, Type delegateType, Type returnType)
        {
            TypeBuilder typeBuilder = GetFreshType();
            MethodBuilder methodBuilder = typeBuilder.DefineMethod(c_DelegateMethodName, MethodAttributes.Public | MethodAttributes.Static, returnType, new[] { typeof(object), typeof(object[]) });
            ILGenerator ilGenerator = methodBuilder.GetILGenerator();

            // Load the target object
            ilGenerator.Emit(OpCodes.Ldarg, (short)0);

            // Suck each parameter out of the array
            ParameterInfo[] parameterInfos = methodToCall.GetParameters();
            for (int i = 0; i < parameterInfos.Length; i++)
            {
                // Load the array
                ilGenerator.Emit(OpCodes.Ldarg, (short)1);

                // Push the array index
                ilGenerator.Emit(OpCodes.Ldc_I4, i);

                // Get the parameter value from the array
                ilGenerator.Emit(OpCodes.Ldelem_Ref);

                Type parameterType = parameterInfos[i].ParameterType;
                if (parameterType.IsValueType)
                {
                    // The parameter is a value type - unbox it
                    ilGenerator.Emit(OpCodes.Unbox_Any, parameterType);
                }
                else
                {
                    // The parameter is a reference type, cast the object to it
                    ilGenerator.Emit(OpCodes.Castclass, parameterType);
                }
            }

            // Make the call
            ilGenerator.Emit(OpCodes.Call, methodToCall);

            // And the ret
            ilGenerator.Emit(OpCodes.Ret);

            // Finalise the type
            Type createdType = typeBuilder.CreateType();

            return Delegate.CreateDelegate(delegateType, createdType.GetMethod(c_DelegateMethodName));
        }

        private static void BuildForwarderMethod(MethodBuilder methodBuilder, Type[] parameterTypes, MethodInfo toForwardToMethod, FieldInfo toForwardField)
        {
            ILGenerator ilGenerator = methodBuilder.GetILGenerator();

            // Push the IMethodInvocationHandler (needs to be lower on the stack than the array)
            ilGenerator.Emit(OpCodes.Ldarg_0);
            ilGenerator.Emit(OpCodes.Ldfld, toForwardField);

            // Create an array to put all the parameters in
            ilGenerator.Emit(OpCodes.Ldc_I4, parameterTypes.Length);
            ilGenerator.Emit(OpCodes.Newarr, typeof(object));

            // Put the parameters into the array
            for (int i = 0; i < parameterTypes.Length; i++)
            {
                // Duplicate the array reference so the next iteration can use it
                ilGenerator.Emit(OpCodes.Dup);

                // Push the array index
                ilGenerator.Emit(OpCodes.Ldc_I4, i);

                // Push the variable itself
                ilGenerator.Emit(OpCodes.Ldarg, (short)(methodBuilder.IsStatic ? i : i + 1));

                if (parameterTypes[i].IsValueType)
                {
                    // Need to box value types
                    ilGenerator.Emit(OpCodes.Box, parameterTypes[i]);
                }

                // And store!
                ilGenerator.Emit(OpCodes.Stelem_Ref);
            }

            // Make the call
            ilGenerator.Emit(OpCodes.Callvirt, toForwardToMethod);

            // And the ret
            ilGenerator.Emit(OpCodes.Ret);
        }

        private static Type[] GetParameterTypes(MethodInfo method)
        {
            ParameterInfo[] parameterInfos = method.GetParameters();
            Type[] parameterTypes = new Type[parameterInfos.Length];
            for (int i = 0; i < parameterTypes.Length; i++)
            {
                parameterTypes[i] = parameterInfos[i].ParameterType;
            }

            return parameterTypes;
        }
    }
}

using System;
using System.Reflection;
using System.Reflection.Emit;

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

            TypeBuilder typeBuilder = GetFreshType();

            ForEveryMethodIncludingSuperInterfaces(
                interfaceType,
                delegate(MethodInfo eachMethod)
                {
                    if (throwOnNonActorMethod && eachMethod.ReturnType != typeof(void) &&
                        !typeof(IActorComponent).IsAssignableFrom(eachMethod.ReturnType))
                    {
                        // The method has a return type, fail fast
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
                                                    FieldAttributes.Private | FieldAttributes.Static);
                        BuildForwarderMethod(methodBuilder, parameterTypes, m_InvokeHappenedMethod,
                                             invocationHandlerField);
                    }
                    else
                    {
                        // This is a request for a subinterface - create a method that will return a proxied version of it
                        FieldBuilder invocationHandlerField =
                            typeBuilder.DefineField(InvocationHandlerNameForMethod(eachMethod),
                                                    typeof(IMethodInvocationHandler),
                                                    FieldAttributes.Private | FieldAttributes.Static);
                        BuildForwarderMethod(methodBuilder, parameterTypes, m_ReturningInvokeHappenedMethod,
                                             invocationHandlerField);
                    }
                });

            typeBuilder.AddInterfaceImplementation(interfaceType);

            // Finalise the type
            Type createdType = typeBuilder.CreateType();

            // Now we can write all the invocation handlers
            ForEveryMethodIncludingSuperInterfaces(
                interfaceType,
                delegate(MethodInfo eachMethod)
                    {
                        FieldInfo writeableInvocationHandlerField =
                            createdType.GetField(InvocationHandlerNameForMethod(eachMethod),
                                                 BindingFlags.Static |
                                                 BindingFlags.NonPublic);

                        MethodCaller methodCaller = CreateMethodCaller(eachMethod);

                        if (eachMethod.ReturnType == typeof (void))
                        {
                            // Standard asynchronous call, put in a handler that will swap threads
                            writeableInvocationHandlerField.SetValue(null,
                                                                     invocationHandler.GetInvocationHandlerFor(
                                                                         methodCaller));
                        }
                        else
                        {
                            // Subinterface getter, put in a call that will get a wrapped subinterface
                            writeableInvocationHandlerField.SetValue(null,
                                                                     invocationHandler.GetInvocationHandlerFor(
                                                                         methodCaller));
                        }
                    });

            return Activator.CreateInstance(createdType);
        }

        internal MethodCaller CreateMethodCaller(MethodInfo eachMethod)
        {
            Action<object, object[]> voidMethodCaller = CreateCallerDelegate(eachMethod);
            Func<object, object[], object> returningMethodCaller = CreateReturningCallerDelegate(eachMethod);
            return new MethodCaller(voidMethodCaller, returningMethodCaller);
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
            TypeBuilder typeBuilder = GetFreshType();

            // Create a field in which to put the IMethodInvocationHandler
            FieldBuilder invocationHandlerField = typeBuilder.DefineField(c_FieldNameForInvocationHandler, typeof(IMethodInvocationHandler),
                                                                            FieldAttributes.Private | FieldAttributes.Static);
            Type[] parameterTypes = GetParameterTypes(signature);
            MethodBuilder methodBuilder = typeBuilder.DefineMethod(c_DelegateMethodName, MethodAttributes.Public | MethodAttributes.Static, signature.ReturnType, parameterTypes);
            BuildForwarderMethod(methodBuilder, parameterTypes, m_InvokeHappenedMethod, invocationHandlerField);

            // Finalise the type
            Type createdType = typeBuilder.CreateType();
            FieldInfo writeableInvocationHandlerField = createdType.GetField(c_FieldNameForInvocationHandler, BindingFlags.Static | BindingFlags.NonPublic);
            writeableInvocationHandlerField.SetValue(null, methodInvocationHandler);

            return Delegate.CreateDelegate(delegateType, createdType.GetMethod(c_DelegateMethodName));
        }

        /// <summary>
        /// Creates a function that takes a target and a parameters array, and will call the given method, having unpacked its arguments from the array.
        /// </summary>
        public Action<object, object[]> CreateCallerDelegate(MethodInfo methodToCall)
        {
            return (Action<object, object[]>) CreateCallerDelegate(methodToCall, typeof (Action<object, object[]>), typeof(void));
        }

        public Func<object, object[], object> CreateReturningCallerDelegate(MethodInfo methodToCall)
        {
            return (Func<object, object[], object>)CreateCallerDelegate(methodToCall, typeof(Func<object, object[], object>), typeof(object));
        }

        private Delegate CreateCallerDelegate(MethodInfo methodToCall, Type delegateType, Type returnType)
        {
            TypeBuilder typeBuilder = GetFreshType();
            MethodBuilder methodBuilder = typeBuilder.DefineMethod(c_DelegateMethodName, MethodAttributes.Public | MethodAttributes.Static, returnType, new Type[] {typeof(object), typeof(object[])});
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
            ilGenerator.Emit(OpCodes.Ldsfld, toForwardField);

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

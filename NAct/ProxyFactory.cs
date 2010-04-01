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
                        if (eachMethod.ReturnType == typeof (void))
                        {
                            // Standard asynchronous call, put in a handler that will swap threads
                            writeableInvocationHandlerField.SetValue(null,
                                                                     invocationHandler.GetInvocationHandlerFor(
                                                                         eachMethod));
                        }
                        else
                        {
                            // Subinterface getter, put in a call that will get a wrapped subinterface
                            writeableInvocationHandlerField.SetValue(null,
                                                                     invocationHandler.GetInvocationHandlerFor(
                                                                         eachMethod));
                        }
                    });

            return Activator.CreateInstance(createdType);
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
        public Delegate CreateMethodProxy(IMethodInvocationHandler methodInvocationHandler, MethodInfo signature, Type delegateType)
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
            ilGenerator.EmitCall(OpCodes.Callvirt, toForwardToMethod, null);

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

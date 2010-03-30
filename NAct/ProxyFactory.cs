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
        public object CreateInterfaceProxy(IInterfaceInvocationHandler invocationHandler, Type interfaceType)
        {
            TypeBuilder typeBuilder = GetFreshType();

            foreach (MethodInfo eachMethod in interfaceType.GetMethods())
            {
                // Create a field in which to put the IMethodInvocationHandler
                FieldBuilder invocationHandlerField = typeBuilder.DefineField(InvocationHandlerNameForMethod(eachMethod), typeof (IMethodInvocationHandler),
                                                                                FieldAttributes.Private | FieldAttributes.Static);
                Type[] parameterTypes = GetParameterTypes(eachMethod);
                MethodBuilder methodBuilder = typeBuilder.DefineMethod(eachMethod.Name, eachMethod.Attributes & ~MethodAttributes.Abstract, eachMethod.ReturnType, parameterTypes);
                BuildForwarderMethod(methodBuilder, parameterTypes, m_InvokeHappenedMethod, invocationHandlerField);
            }

            typeBuilder.AddInterfaceImplementation(interfaceType);

            // Finalise the type
            Type createdType = typeBuilder.CreateType();

            // Now we can write all the invocation handlers
            foreach (MethodInfo eachMethod in interfaceType.GetMethods())
            {
                FieldInfo writeableInvocationHandlerField = createdType.GetField(InvocationHandlerNameForMethod(eachMethod),
                                                                                 BindingFlags.Static |
                                                                                 BindingFlags.NonPublic);
                writeableInvocationHandlerField.SetValue(null, invocationHandler.GetInvocationHandlerFor(eachMethod));
            }

            m_DynamicAssembly.Save("dynamic.dll");

            return Activator.CreateInstance(createdType);
        }

        private static string InvocationHandlerNameForMethod(MethodInfo method)
        {
            // TODO disambiguate methods with the same name
            return c_FieldNameForInvocationHandler + method.Name;
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
                ilGenerator.Emit(OpCodes.Ldarg, (short) (methodBuilder.IsStatic ? i : i + 1));

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

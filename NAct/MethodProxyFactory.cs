using System;
using System.Reflection;
using System.Reflection.Emit;

namespace NAct
{
    public class MethodProxyFactory
    {
        private const string c_FieldNameForInvocationHandler = "s_InvocationHandler";
        private const string c_MethodName = "ProxyMethod";

        private readonly MethodInfo m_InvocationHappenedMethod = typeof(IMethodInvocationHandler).GetMethod("InvokeHappened");
        private readonly AssemblyBuilder m_DynamicAssembly = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName("NActDynamicAssembly"), AssemblyBuilderAccess.RunAndSave);
        private readonly ModuleBuilder m_DynamicModule;

        private int m_TypeIndex = 0;

        public MethodProxyFactory()
        {
            m_DynamicModule = m_DynamicAssembly.DefineDynamicModule("dynamic.dll", "dynamic.dll");
        }

        /// <summary>
        /// Creates a new method that has the same signature as an existing one, but just calls an methodInvocationHandler when it's called.
        /// </summary>
        /// <param name="methodInvocationHandler">The thing that handles the call</param>
        /// <param name="signature">A method whose signature we want to duplicate</param>
        /// <param name="delegateType">The specific type of the delegate you'd like to create (it's safe to cast the return value to this type)</param>
        public Delegate CreateMethodProxy(IMethodInvocationHandler methodInvocationHandler, MethodInfo signature, Type delegateType)
        {
            ParameterInfo[] delegateParameterInfos = signature.GetParameters();
            Type[] delegateParameterTypes = new Type[delegateParameterInfos.Length];
            for (int i = 0; i < delegateParameterTypes.Length; i++)
            {
                delegateParameterTypes[i] = delegateParameterInfos[i].ParameterType;
            }
            
            TypeBuilder typeBuilder = m_DynamicModule.DefineType("DynamicType" + m_TypeIndex);
            m_TypeIndex++;

            // Create a static field in which to put the IMethodInvocationHandler
            FieldBuilder invocationHandlerField = typeBuilder.DefineField(c_FieldNameForInvocationHandler, typeof (IMethodInvocationHandler),
                                                                            FieldAttributes.Private | FieldAttributes.Static);

            MethodBuilder methodBuilder = typeBuilder.DefineMethod(c_MethodName, MethodAttributes.Public | MethodAttributes.Static, signature.ReturnType, delegateParameterTypes);
            //DynamicMethod proxyMethod = new DynamicMethod("Proxy", signature.ReturnType, delegateParameterTypes, m_DynamicModule);
            ILGenerator ilGenerator = methodBuilder.GetILGenerator();

            // Push the IMethodInvocationHandler (needs to be lower on the stack than the array)
            ilGenerator.Emit(OpCodes.Ldsfld, invocationHandlerField);

            // Create an array to put all the parameters in
            ilGenerator.Emit(OpCodes.Ldc_I4, delegateParameterTypes.Length);
            ilGenerator.Emit(OpCodes.Newarr, typeof(object));

            // Put the parameters into the array
            for (int i = 0; i < delegateParameterTypes.Length; i++)
            {
                // Duplicate the array reference so the next iteration can use it
                ilGenerator.Emit(OpCodes.Dup);

                // Push the index in the array to be stored
                ilGenerator.Emit(OpCodes.Ldc_I4, i);

                // Push the variable itself
                ilGenerator.Emit(OpCodes.Ldarg, (short) i);

                if (delegateParameterTypes[i].IsValueType)
                {
                    ilGenerator.Emit(OpCodes.Box, delegateParameterTypes[i]);
                }

                // And store!
                ilGenerator.Emit(OpCodes.Stelem_Ref);
            }

            // Make the call
            ilGenerator.EmitCall(OpCodes.Callvirt, m_InvocationHappenedMethod, null);

            // And the ret
            ilGenerator.Emit(OpCodes.Ret);

            // Save the module for debugging
            Type createdType = typeBuilder.CreateType();
            FieldInfo writeableInvocationHandlerField = createdType.GetField(c_FieldNameForInvocationHandler, BindingFlags.Static | BindingFlags.NonPublic);
            writeableInvocationHandlerField.SetValue(null, methodInvocationHandler);

            return Delegate.CreateDelegate(delegateType, createdType.GetMethod(c_MethodName));
        }
    }
}

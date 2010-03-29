using System;
using System.Reflection;
using System.Reflection.Emit;

namespace NAct
{
    public static class MethodProxyFactory
    {
        private const string c_FieldNameForInvocationHandler = "s_InvocationHandler";
        private const string c_MethodName = "ProxyMethod";

        private static readonly MethodInfo s_InvocationHappenedMethod = typeof(IInvocationHandler).GetMethod("InvokeHappened");
        private static readonly AssemblyBuilder s_DynamicAssembly = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName("NActDynamicAssembly"), AssemblyBuilderAccess.RunAndSave);
        private static readonly ModuleBuilder s_DynamicModule = s_DynamicAssembly.DefineDynamicModule("dynamic.dll", "dynamic.dll");

        /// <summary>
        /// Creates a new method that has the same signature as an existing one, but just calls an invocationHandler when it's called.
        /// 
        /// Really sorry I can't do the cast to TDelegate for you, C# generics have a missing feature.
        /// </summary>
        /// <param name="invocationHandler">The thing that handles the call</param>
        /// <param name="signature">A method whose signature we want to duplicate</param>
        public static Delegate CreateMethodProxy<TDelegate>(IInvocationHandler invocationHandler, MethodInfo signature)
        {
            ParameterInfo[] delegateParameterInfos = signature.GetParameters();
            Type[] delegateParameterTypes = new Type[delegateParameterInfos.Length];
            for (int i = 0; i < delegateParameterTypes.Length; i++)
            {
                delegateParameterTypes[i] = delegateParameterInfos[i].ParameterType;
            }
            
            TypeBuilder typeBuilder = s_DynamicModule.DefineType("DynamicType");

            // Create a static field in which to put the IInvocationHandler
            FieldBuilder invocationHandlerField = typeBuilder.DefineField(c_FieldNameForInvocationHandler, typeof (IInvocationHandler),
                                                                            FieldAttributes.Private | FieldAttributes.Static);

            MethodBuilder methodBuilder = typeBuilder.DefineMethod(c_MethodName, MethodAttributes.Public | MethodAttributes.Static, signature.ReturnType, delegateParameterTypes);
            //DynamicMethod proxyMethod = new DynamicMethod("Proxy", signature.ReturnType, delegateParameterTypes, s_DynamicModule);
            ILGenerator ilGenerator = methodBuilder.GetILGenerator();

            // Push the IInvocationHandler (needs to be lower on the stack than the array)
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
            ilGenerator.EmitCall(OpCodes.Callvirt, s_InvocationHappenedMethod, null);

            // And the ret
            ilGenerator.Emit(OpCodes.Ret);

            // Save the module for debugging
            Type createdType = typeBuilder.CreateType();
            FieldInfo writeableInvocationHandlerField = createdType.GetField(c_FieldNameForInvocationHandler, BindingFlags.Static | BindingFlags.NonPublic);
            writeableInvocationHandlerField.SetValue(null, invocationHandler);

            return Delegate.CreateDelegate(typeof(TDelegate), createdType.GetMethod(c_MethodName));
        }
    }
}

using System.Reflection;

namespace NAct
{
    interface IInterfaceInvocationHandler
    {
        void InvokeHappened(MethodInfo method, object[] parameterValues);
    }
}

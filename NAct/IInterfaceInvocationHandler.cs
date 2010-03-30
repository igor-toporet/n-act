using System.Reflection;

namespace NAct
{
    public interface IInterfaceInvocationHandler
    {
        void InvokeHappened(MethodInfo method, object[] parameterValues);
    }
}

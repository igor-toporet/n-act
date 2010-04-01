using System.Reflection;

namespace NAct
{
    public interface IInterfaceInvocationHandler
    {
        /// <summary>
        /// Gets something that can handle the invocation of the given method. TODO This call may block while waiting for the actor to be instantiated
        /// </summary>
        IMethodInvocationHandler GetInvocationHandlerFor(MethodInfo method);
    }
}

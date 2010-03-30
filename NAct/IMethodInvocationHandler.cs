namespace NAct
{
    /// <summary>
    /// Something that can deal with a method being invoked
    /// </summary>
    public interface IMethodInvocationHandler
    {
        void InvokeHappened(object[] parameterValues);
    }
}

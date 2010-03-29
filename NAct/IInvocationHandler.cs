namespace NAct
{
    /// <summary>
    /// Something that can deal with a method being invoked
    /// </summary>
    public interface IInvocationHandler
    {
        void InvokeHappened(object[] parameterValues);
    }
}

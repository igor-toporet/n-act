namespace NAct
{
    /// <summary>
    /// Something that can deal with a method being invoked
    /// </summary>
    public interface IMethodInvocationHandler
    {
// ReSharper disable UnusedMemberInSuper.Global
        // These are called by reflection
        void InvokeHappened(object[] parameterValues);
        object ReturningInvokeHappened(object[] parameterValues);
// ReSharper restore UnusedMemberInSuper.Global
    }
}

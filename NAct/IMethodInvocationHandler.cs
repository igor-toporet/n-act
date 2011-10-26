using NAct.Utils;

namespace NAct
{
    /// <summary>
    /// Something that can deal with a method being invoked
    /// </summary>
    [DoNotObfuscateType]
    public interface IMethodInvocationHandler
    {
// ReSharper disable UnusedMemberInSuper.Global
        // These are called by reflection
        [DoNotPrune]
        void InvokeHappened(object[] parameterValues);
        [DoNotPrune]
        object ReturningInvokeHappened(object[] parameterValues);
// ReSharper restore UnusedMemberInSuper.Global
    }
}

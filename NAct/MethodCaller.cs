using System;
using NAct.Utils;

namespace NAct
{
    /// <summary>
    /// Stores two delegates (only one of which is ever valid to call) for calling later.
    /// </summary>
    public class MethodCaller
    {
        private readonly Action<object, object[]> m_MethodCaller;
        private readonly Func<object, object[], object> m_ReturningMethodCaller;

        public MethodCaller(Action<object, object[]> methodCaller, Func<object, object[], object> returningMethodCaller)
        {
            m_MethodCaller = methodCaller;
            m_ReturningMethodCaller = returningMethodCaller;
        }

        /// <summary>
        /// Takes a target object, and some parameters and runs the method represented by this MethodCaller on it.
        /// 
        /// The target object may be a real object, or it may be a delegate, depending on which kind of method caller this is.
        /// </summary>
        public void CallMethod(object target, object[] parameters)
        {
            m_MethodCaller(target, parameters);
        }

        /// <summary>
        /// Takes a target object, and some parameters and runs the method represented by this MethodCaller on it.
        /// 
        /// The target object may be a real object, or it may be a delegate, depending on which kind of method caller this is.
        /// </summary>
        public object CallReturningMethod(object target, object[] parameters)
        {
            return m_ReturningMethodCaller(target, parameters);
        }
    }
}

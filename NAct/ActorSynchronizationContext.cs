using System;
using System.Threading;

namespace NAct
{
    class ActorSynchronizationContext : SynchronizationContext
    {
        private readonly Action<Action> m_Executor;

        public ActorSynchronizationContext(Action<Action> executor)
        {
            m_Executor = executor;
        }

        public override void Send(SendOrPostCallback d, object state)
        {
            // Don't block while calling actors
            throw new NotSupportedException();
        }

        public override void Post(SendOrPostCallback d, object state)
        {
            // The important one, run the task in the actor's thread
            m_Executor(() => d(state));
        }

        public override SynchronizationContext CreateCopy()
        {
            // We are immutable, no need to copy
            return this;
        }
    }
}
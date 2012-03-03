using System;
using System.Runtime.CompilerServices;

namespace NAct
{
    class Future : IAwaitable, IAwaiter
    {
        private readonly object m_Sync = new object();

        private Action m_Action;
        private bool m_Completed;

        public bool IsCompleted
        {
            get
            {
                return m_Completed;
            }
        }

        public void OnCompleted(Action action)
        {
            lock (m_Sync)
            {
                if (m_Completed)
                {
                    // Sometimes, it finishes between IsCompleted being checked, and us being set.
                    action();
                }
                else
                {
                    m_Action = action;
                }
            }
        }

        public void GetResult()
        {
        }

        public void Complete()
        {
            lock (m_Sync)
            {
                m_Completed = true;

                Action action = m_Action;
                if (action != null)
                {
                    action();
                }
            }
        }

        public IAwaiter GetAwaiter()
        {
            return this;
        }
    }

    interface IAwaitable
    {
        IAwaiter GetAwaiter();
    }

    interface IAwaiter : INotifyCompletion
    {
        bool IsCompleted { get; }
        void OnCompleted(Action action);
        void GetResult();
    }
}
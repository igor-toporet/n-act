using System;
using System.Runtime.CompilerServices;

namespace NAct
{
    /// <summary>
    /// On the face of it, this is massively thread-unsafe. Not really sure how it ever works
    /// </summary>
    class Future : IAwaitable, IAwaiter
    {
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

        public void GetResult()
        {
        }

        public void Complete()
        {
            m_Completed = true;

            Action action = m_Action;
            if (action != null)
            {
                action();
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
        void GetResult();
    }
}
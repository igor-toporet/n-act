using System;
using System.Runtime.CompilerServices;

namespace NAct
{
    class Future<T> : IAwaitable<T>, IAwaiter<T>
    {
        private readonly object m_Sync = new object();

        private Action m_Action;
        private bool m_Completed;
        private T m_Result;

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

        public T GetResult()
        {
            return m_Result;
        }

        public void Complete(T result)
        {
            lock (m_Sync)
            {
                m_Result = result;
                m_Completed = true;

                Action action = m_Action;
                if (action != null)
                {
                    action();
                }
            }
        }

        public IAwaiter<T> GetAwaiter()
        {
            return this;
        }
    }

    interface IAwaitable<T>
    {
        IAwaiter<T> GetAwaiter();
    }

    interface IAwaiter<out T> : INotifyCompletion
    {
        bool IsCompleted { get; }
        void OnCompleted(Action action);
        T GetResult();
    }
}
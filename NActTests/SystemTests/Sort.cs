using System;
using System.Collections.Generic;
using System.Text;
using NAct;

namespace NActTests.SystemTests
{
    class Sort
    {
        /// <summary>
        /// Something that can returns things sorted (that you probably passed it in its contructor
        /// </summary>
        interface ISortor<T> : IActor where T : IComparable<T>
        {
            /// <summary>
            /// Sorts the given collection of Ts, and returns them via the callback resultReturner in order
            /// </summary>
            event Action<T> ResultFound;
        }

        class MergerSortor<T> : ISortor<T> where T : IComparable<T>
        {
            /// <summary>
            /// The threashold below which we consider the list to be sorted small enough to do single threaded
            /// </summary>
            private const long c_BiteSize = 100;
            
            /// <summary>
            /// The Ts which we have been given that we're not sure if they are the next value or not
            /// </summary>
            private readonly Queue<T> m_WaitingLeft = new Queue<T>();
            private readonly Queue<T> m_WaitingRight= new Queue<T>();

            private ISortor<T> m_Left;
            private ISortor<T> m_Right;

            public MergerSortor(T[] input)
            {
                if (input.Length < c_BiteSize)
                {
                    // Just sort single-threaded
                    List<T> inplaceSort = new List<T>(input);
                    inplaceSort.Sort();
                    foreach (T eachT in inplaceSort)
                    {
                        InvokeResultFound(eachT);
                    }
                }
                else
                {
                    T[] leftList = new T[input.Length / 2];
                    T[] rightList = new T[input.Length - leftList.Length];

                    for (int i = 0; i < leftList.Length; i++)
                    {
                        leftList[i] = input[i];
                    }

                    for (int i = 0; i < rightList.Length; i++)
                    {
                        rightList[i] = input[i + leftList.Length];
                    }

                    m_Left = ThreaderWrapper.CreateActor(() => new MergerSortor<T>(leftList));
                    m_Right = ThreaderWrapper.CreateActor(() => new MergerSortor<T>(rightList));
                    
                    m_Left.ResultFound += 
                }
            }

            public event Action<T> ResultFound;

            private void InvokeResultFound(T result)
            {
                Action<T> handler = ResultFound;
                if (handler != null) handler(result);
            }
        }
    }
}

using System;
using System.Collections.Generic;

namespace NAct.Utils
{
    public static class IterationHelper
    {
        /// <summary>
        /// Implements a pattern where you set off a lot of tasks (which would have been in a foreach), wait for them all to finish, then do something else.
        /// </summary>
        /// <typeparam name="TIterator">The enumerated type</typeparam>
        /// <param name="toLoopOver">The IEnumerable to loop over</param>
        /// <param name="toDoForEach">What to do for each iteration of the loop. Takes the loop variable and a callback you MUST call once your asynchronous task is done.</param>
        /// <param name="toDoWhenFinished">When to do when everything is finished</param>
        public static void ForEachThen<TIterator>(IEnumerable<TIterator> toLoopOver, Action<TIterator, Action> toDoForEach, Action toDoWhenFinished)
        {
            int leftToProcess = 0;

            IEnumerator<TIterator> enumerator = toLoopOver.GetEnumerator();

            bool hasNext = enumerator.MoveNext();

            while (hasNext)
            {
                TIterator eachTIterator = enumerator.Current;
                leftToProcess++;

                // We must update hasNext BEFORE the call, so that if no thread switch happens, the finished protocol runs
                hasNext = enumerator.MoveNext();

                toDoForEach(eachTIterator, delegate
                                               {
                                                   leftToProcess--;

                                                   if (!hasNext && leftToProcess == 0)
                                                   {
                                                       toDoWhenFinished();
                                                   }
                                               });
            }
        }
    }
}

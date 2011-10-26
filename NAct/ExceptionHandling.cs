using System;

namespace NAct
{
    public class ExceptionHandling
    {
        internal static Action<Exception> ExceptionHandler { get; set; }

        static ExceptionHandling()
        {
            Register(DefaultExceptionHandler);
        }

        private static void DefaultExceptionHandler(Exception e)
        {
            // By default, do nothing
        }

        public static void Register(Action<Exception> handler)
        {
            ExceptionHandler = handler;
        }
    }
}

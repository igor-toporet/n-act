namespace NAct.Utils
{
    public delegate void Action();

    public delegate void Action<TA, TB>(TA a, TB b);

    public delegate void Action<TA, TB, TC>(TA a, TB b, TC c);

    public delegate void Action<TA, TB, TC, TD>(TA a, TB b, TC c, TD d);

    public delegate void Action<TA, TB, TC, TD, TE>(TA a, TB b, TC c, TD d, TE e);

    public delegate TReturn Func<TA, TReturn>(TA a);

    public delegate TReturn Func<TA, TB, TReturn>(TA a, TB b);

    public delegate TReturn Func<TA, TB, TC, TReturn>(TA a, TB b, TC c);

    public delegate TReturn Func<TA, TB, TC, TD, TReturn>(TA a, TB b, TC c, TD d);
}

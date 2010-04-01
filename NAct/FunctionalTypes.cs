namespace NAct
{
    public delegate void Action();

    public delegate void Action<TA, TB>(TA a, TB b);

    public delegate TReturn Func<TA, TReturn>(TA a);

    public delegate TReturn Func<TA, TB, TReturn>(TA a, TB b);
}

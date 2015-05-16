# NAct Tutorial: Immutable messages #

Some actors systems (eg Axum) have a way to make sure that two actors never end up accessing memory in an un-thread-safe way. NAct doesn't - you have to be careful.

But there's a simple rule for keeping things safe. Actors can share objects, and pass objects between each other, as long as the objects they are sharing never change.


## What does an immutable class look like? ##

An immutable class is one where it's member variables never change (the readonly keyword is a great way to remember to keep it that way) **and** all of it's member variables are either value-types or immutable classes.

That's really important point, that all the class's member variables have to be immutable too. If an object contains an Dictionary, and you can add things to the Dictionary, two actors could try to add things at the same time, and the Dictionary would get very unhappy.

```
public class PlaneInfo
{
    private readonly string m_Name;
    private readonly int m_Capacity;
    private readonly bool m_IsBiplane;

    public PlaneInfo(string name, int capacity, bool isBiplane)
    {
        m_Name = name;
        m_IsBiplane = isBiplane;
        m_Capacity = capacity;
    }

    public bool IsBiplane
    {
        get { return m_IsBiplane; }
    }

    public int Capacity
    {
        get { return m_Capacity; }
    }

    public string Name
    {
        get { return m_Name; }
    }
}
```

Luckily, .NET strings are immutable already, so they're safe to use.

## Can I use structs instead of immutable objects? ##
Yes, it's always safe to pass value-types between actors. But large structs could get slow to copy, so I'd stick to using primitive types and immutable classes.
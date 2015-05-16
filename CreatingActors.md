## Step 1: Make a sub-interface of IActor ##

If you want your actor to be accessible by other actors (which you usually do) you need to define an interface for it.

```
public interface IPonger : IActor
{
    Task Pong();
}
```


## Step 2: Make your actor class ##

This is the easy bit, make a class that implements your new interface. This code is what will run when a method is called on your actor.

```
class Ponger : IPonger
{
    public async Task Pong()
    {
        Console.WriteLine("Pong!!");
    }
}
```

## Step 3: Wrap an actor ##

And finally, you need to tell NAct to turn your object into an actor, as you create it.

```
IPonger ponger = ActorWrapper.WrapActor<IPonger>(() => new Ponger());
```

The object you get back is an NAct proxy. Whenever you call a method on it, NAct will call the same method on your object at some point in the right thread.
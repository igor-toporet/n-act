So what does code using NAct look like?

The "Hello world" program using actors is two actors that send messages called Ping and Pong to each other. Here is it implemented using NAct:

```
        class Pinger : IPinger {
            private readonly IPonger m_Ponger;

            public Pinger(IPonger ponger) {
                m_Ponger = ponger;
                m_Ponger.Ponged += Ping;
            }

            public void Ping() {
                m_Ponger.Pong();
            }
        }

        class Ponger : IPonger {
            public void Pong() {
                s_Count++;
                Ponged();
            }

            public event Action Ponged;
        }
```

Looks _remarkably_ like two standard objects calling methods called Ping and Pong on each other, doesn't it? (Of course, to prevent a cyclic dependency, I have to make one an event instead).

If you run this **valid C#** it would do exactly the same as if you ran it without NAct (ok, so it would overflow the stack pretty quickly, but you get the idea). That's the point.

Here is the bit that starts the Pinging (and where NAct is actually used):
```
IPonger ponger = ActorWrapper.WrapActor<IPonger>(() => new Ponger());
IPinger pinger = ActorWrapper.WrapActor<IPinger>(() => new Pinger(ponger));

pinger.Ping();
Thread.Sleep(1000);

Console.WriteLine(s_Count);
```

You just need to show ActorWrapper how to create an object (using that lambda), and it will create a proxy obejct, that looks exactly the same, but is an Actor, and will run in its own thread.

In case you're interested in performance (and scared of the reflection I'm obviously doing) it currently manages 700,000 Pings per second on my laptop (which can do 150,000,000 method calls per second). Still some low-hanging fruit to go too, I hope to be down to under 50 times the overhead of a method call.
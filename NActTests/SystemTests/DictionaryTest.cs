using System;
using System.Collections.Generic;
using System.Text;
using NAct;
using NAct.Utils;
using NUnit.Framework;

namespace NActTests.SystemTests
{
    [TestFixture]
    class DictionaryTest
    {
        [Test]
        public void Stuff()
        {
            TesterMagig t = new TesterMagig();

            t.DoIt();
        }
    }

    class TesterMagig : IActor
    {
        public void DoIt()
        {
            IDictionaryActor<string, int> d =
                ActorWrapper.WrapActor<IDictionaryActor<string, int>>(() => new DictionaryActor<string, int>());

            int r = 3;

            d["hello"] = 3;

            d.Atomically(
                dictionary =>
                    {
                        dictionary["hello"] = dictionary["hello"] + r;
                        return true;
                    });
        }
    }
}

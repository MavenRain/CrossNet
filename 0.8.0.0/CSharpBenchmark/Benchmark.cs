/*
    CrossNet - C# Benchmark
*/

using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpBenchmark._Benchmark
{
    public static class Benchmark
    {
        public delegate bool TestDelegate(int N);

        public static void Test()
        {
            const int SCALE = 1;

            DoTest("Binary Tree", BinaryTreesTest.Test, SCALE * 1000);
            DoTest("Cast", CastTest.Test, SCALE * 66);
            DoTest("Dictionary", DictionaryTest.Test, SCALE * 150);
            DoTest("Event", EventsTest.Test, SCALE * 1700);
            DoTest("Fannkuch", FannkuchTest.Test, SCALE * 16000);
            DoTest("Foreach on array", ForeachOnArrayTest.Test, SCALE * 1250000);
            DoTest("GC", GcTest.Test, SCALE * 400);
            DoTest("Hashtable and box / unbox", HashtableTest.Test, SCALE * 1200);
            DoTest("Heapsort", HeapSortTest.Test, SCALE * 250);
            DoTest("Interface call", InterfaceTest.Test, SCALE * 100);
            DoTest("List", ListTest.Test, SCALE * 18000);
            DoTest("Matrix multiply", MatrixTest.Test, SCALE * 50000);
            DoTest("Nested loops", NestedLoopsTest.Test, SCALE * 500);
            DoTest("Sieves", NSieveTest.Test, SCALE * 700);
            DoTest("Partial sums", PartialSumsTest.Test, SCALE * 16000);
            DoTest("Recursive call", RecursiveTest.Test, SCALE * 1);
            DoTest("String concatenation", StringConcatenationTest.Test, SCALE * 160);
            DoTest("Unsafe code", UnsafeTest.Test, SCALE * 750000);
            DoTest("Virtual call", VirtualTest.Test, SCALE * 100);

            Console.WriteLine("Press a key...");
            Console.ReadKey(true);
        }

        public static void DoTest(string text, TestDelegate del, int n)
        {
            Console.WriteLine("Test " + text + ":");
            Console.WriteLine("\tInitializing...");

            // First collect everything
            System.GC.Collect();

            // Then do one run so the methods are JITTed correctly
            bool result = del(1);
            if (result == false)
            {
                Console.WriteLine("\tInitialization failed!");
                Console.WriteLine();
                return;
            }

            // Recollect everything again
            System.GC.Collect();

            Console.WriteLine("\tRunning...");
            DateTime started = DateTime.Now;

            result = del(n);
            if (result == false)
            {
                Console.WriteLine("\tTest failed!");
                Console.WriteLine();
                return;
            }

            DateTime ended = DateTime.Now;
            TimeSpan span = ended.Subtract(started);

            Console.WriteLine("\tFinished in " + span.TotalSeconds.ToString()  + " seconds...");
            Console.WriteLine();
        }
    }
}

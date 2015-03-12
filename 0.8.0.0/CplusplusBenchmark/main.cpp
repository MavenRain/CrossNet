/*
    CrossNet - C++ Benchmark
*/

#include "stdafx.h"
#include <time.h>
#include <stdio.h>
#include <conio.h>

bool DictionaryTest(int N);
bool FannkuchTest(int N);
bool ForeachOnArrayTest(int N);
bool GcTest(int N);
bool HeapsortTest(int N);
bool MatrixTest(int N);
bool NestedLoopsTest(int N);
bool NSieveTest(int N);
bool PartialSumsTest(int N);
bool RecursiveTest(int N);
bool UnsafeTest(int N);
bool VirtualTest(int N);

typedef bool (*callback)(int);

void DoTest(const char *, callback, int);
void ForceCompilation(int n);

void __cdecl main()
{
    const int SCALE = 1;

    //DoTest("Binary Tree", BinaryTreesTest.Test, SCALE * 1000);
    //DoTest("Cast", CastTest.Test, SCALE * 66);
    DoTest("Dictionary", DictionaryTest, SCALE * 150);
    //DoTest("Event", EventsTest.Test, SCALE * 1700);
    DoTest("Fannkuch", FannkuchTest, SCALE * 16000);
    DoTest("Foreach on array", ForeachOnArrayTest, SCALE * 1250000);
    DoTest("GC", GcTest, SCALE * 400);
    //DoTest("Hashtable and box / unbox", HashtableTest.Test, SCALE * 1200);
    DoTest("Heapsort", HeapsortTest, SCALE * 250);
    //DoTest("Interface call", InterfaceTest.Test, SCALE * 100);
    //DoTest("List", ListTest, SCALE * 18000);
    DoTest("Matrix multiply", MatrixTest, SCALE * 50000);
    DoTest("Nested loops", NestedLoopsTest, SCALE * 500);
    DoTest("Sieves", NSieveTest, SCALE * 700);
    DoTest("Partial sums", PartialSumsTest, SCALE * 16000);
    DoTest("Recursive call", RecursiveTest, SCALE * 1);
    //DoTest("String concatenation", StringConcatenationTest.Test, SCALE * 160);
    DoTest("Unsafe code", UnsafeTest, SCALE * 750000);
    DoTest("Virtual call", VirtualTest, SCALE * 100);

    ForceCompilation((int)clock());

    _getch();
}

void DoTest(const char * text, callback function, int N)
{
    printf("Test %s:\n", text);
    printf("\tInitializing...\n");

    bool result = function(1);
    if (result == false)
    {
        printf("\tInitialization failed!\n");
        printf("\n");
        return;
    }

    printf("\tRunning...\n");
    clock_t started = clock();

    result = function(N);
    if (result == false)
    {
        printf("\tTest failed!\n");
        printf("\n");
        return;
    }

    clock_t ended = clock();
    double diff = (double)(ended - started) / (double)CLOCKS_PER_SEC;

    printf("\tFinished in %lf seconds...\n", diff);
    printf("\n");
}

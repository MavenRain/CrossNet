/*
    CrossNetBenchmark
*/

#include "stdafx.h"
#include "dlmalloc.h"
#include <conio.h>

// Warnings related to using switch with flag based enums
#pragma warning (disable: 4063)     //  warning C4063: case '3' is not a valid value for switch of enum 'CrossNetUnitTestData::Flow::TestSwitch::Foo'
// Warnings related to unused variables
#pragma warning (disable: 4100)     //  warning C4100: 'b' : unreferenced formal parameter
#pragma warning (disable: 4101)     //  warning C4101: 'exception1' : unreferenced local variable
#pragma warning (disable: 4189)     //  warning C4189: 'obj2' : local variable is initialized but not referenced
// Warnings related to unreferenced method / code
#pragma warning (disable: 4505)     //  warning C4505: 'CrossNetUnitTestData::Generic::Predicate2__G<U,V>::__Cast__' : unreferenced local function has been removed
#pragma warning (disable: 4702)     //  warning C4702: unreachable code

#include "CrossNetSystem/CrossNetSystem.h"

namespace System
{
    class Type : public System::Object  // Bare minimum needed for the benchmark
    {
    public:
        CN_DYNAMIC_ID()

        static Type * Create()
        {
            Type * type = new Type();
            type->m__InterfaceMap__ = __GetInterfaceMap__();
            return (type);
        }
    };
    void * * Type::s__InterfaceMap__ = NULL;
}

#include "csharpbenchmark_method_definition.cpp"

void    RegisterSystemType()
{
    ::System::Type::s__InterfaceMap__ = CrossNetRuntime::InterfaceMapper::RegisterObject(sizeof(::System::Type));

    //  The System::Type of System::Type has been created before its m__InterfaceMap__ was set correctly
    //  Fixup the pointer here
    ::System::Type * type = CrossNetRuntime::InterfaceMapper::GetType(::System::Type::s__InterfaceMap__);
    type->m__InterfaceMap__ = ::System::Type::s__InterfaceMap__;
}

void MainTrace(unsigned char currentMark)
{
    csharpbenchmark__AssemblyTrace(currentMark);
}

void *  UnmanagedAlloc(int size)        // Currently just used for the interface wrappers
{
    return (dlmalloc(size));    
}

void    UnmanagedFree(void * pointer)
{
    dlfree(pointer);
}

void *  AllocateAfterGC(int size)
{
    __asm int 3;          // Detect if the buffer has been filled even after a GC
    return (NULL);
}

int __cdecl _tmain(int argc, _TCHAR* argv[])
{
    CrossNetRuntime::InitOptions initOptions;
    initOptions.mInterfaceMapSize = 1 * 1024 * 1024;
    initOptions.mInterfaceMapBuffer = new char[initOptions.mInterfaceMapSize];
    initOptions.mMainBufferSize = 40 * 1024 * 1024;
    void * buffer = new char[initOptions.mMainBufferSize + 15];
    initOptions.mMainBuffer = (void *)((int)(buffer) & 0xfffffff0); // Make sure it is aligned properly

    initOptions.mUnmanagedAllocateCallback = UnmanagedAlloc;
    initOptions.mUnmanagedFreeCallback = UnmanagedFree;
    initOptions.mRegisterSystemTypeCallback = RegisterSystemType;
    initOptions.mDestructGCObjectCallback = ::CrossNetRuntime::GCManager::CheckCollecting;
    initOptions.mAllocateAfterGCCallback = AllocateAfterGC;
    initOptions.mMainTrace = MainTrace;
    CrossNetRuntime::Setup(initOptions);

    CrossNetRuntime::GCManager::SetTopOfStack();

    CrossNetSystem__Setup();                        // Initialize BCL
    csharpbenchmark__Setup();                       // Populate the interface map and call the static constructors
    CSharpBenchmark::_Benchmark::Benchmark::Test(); // Run the benchmark
    csharpbenchmark__Teardown();
    CrossNetSystem__Teardown();

    CrossNetRuntime::Teardown();

    _getch();
	return 0;
}

System::Type * CrossNetRuntime::InterfaceMapper::CreateSystemType()
{
    return (System::Type::Create());
}

void    CrossNetRuntime::InterfaceMapper::TraceSystemType(System::Type * type, unsigned char currentMark)
{
    GCManager::Trace(type, currentMark);    // Truth is, with the current implementation, this should be a no-op
}

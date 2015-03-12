/*
    CrossNetBenchmark
*/

#include <new>
#include "dlmalloc.h"

void * __CRTDECL operator new(size_t size)
{
    return (dlmalloc(size));
}

void __CRTDECL operator delete(void * buffer)
{
    dlfree(buffer);
}

void * __CRTDECL operator new[](std::size_t size)
{
    return (dlmalloc(size));
}

void __CRTDECL operator delete[](void * buffer)
{
    dlfree(buffer);
}
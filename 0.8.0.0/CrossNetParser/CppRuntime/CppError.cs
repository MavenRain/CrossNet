/*
    CrossNet - Copyright (c) 2007 Olivier Nallet

    Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"),
    to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
    and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
    DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE
    OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Text;

using Reflector.CodeModel;
using CrossNet.Interfaces;

namespace CrossNet.CppRuntime
{
    public static class CppError
    {
        public static void DisplayAbstractGenericMethod(ITypeInfo typeInfo, IMethodDeclaration method)
        {
            string errorText = "ERROR CN0100: " + typeInfo.DotNetFullName + "." + method.Name + " is generic and abstract.\n";
            errorText += "This is incompatible with C++ and we cannot recover.";
            Console.Error.WriteLine(errorText);
        }

        public static void DisplayVirtualGenericMethod(ITypeInfo typeInfo, IMethodDeclaration method)
        {
            string warningText = "WARNING CN0101: " + typeInfo.DotNetFullName + "." + method.Name + " is generic and virtual.\n";
            warningText += "This is incompatible with C++, but we can recover by not marking it virtual. The application's behavior will not be the same as .NET.";
            Console.Out.WriteLine(warningText);
        }
    }
}

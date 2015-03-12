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
using System.Collections;
using System.Collections.Generic;
using System.Text;

using Reflector;
using Reflector.CodeModel;
using Reflector.CodeModel.Memory;

using CrossNet.Common;

namespace CrossNet.Interfaces
{
    public enum NestedType
    {
        NOT_NESTED,
        NESTED_STANDARD,
        NESTED_GENERIC,
    }

    public enum ObjectType
    {
        CLASS,
        STRUCT,
        ENUM,
        INTERFACE,
        DELEGATE,
    }

    public interface ITypeGenerator
    {
        GeneratedData GenerateCode(IAssembly assembly, AssemblyData data);
        void GenerateCode(IModule module, AssemblyData data);
        void GenerateCode(ITypeInfo typeInfo, NestedType nested, AssemblyData data);
        string GenerateCodeGenericArguments(ITypeCollection genericArguments, NestedType nested);
        string GenerateCodeGenericConstraints(ITypeCollection genericArguments, NestedType nested);
        void GenerateProperty(bool isInterface, ITypeInfo typeInfo, IPropertyDeclaration propertyDeclaration, ObjectType objectType, IDictionary removeMethods, bool wrapper);
        string GetMethodModifiers(ITypeInfo typeInfo, IMethodDeclaration methodDeclaration, ObjectType objectType, out bool abstractMethod);
        void GenerateCodeEnum(string passedText, ITypeInfo typeInfo);

        // Specific functions for anonymous methods
        // Anonymous methods are created while we are parsing type and methods
        // So we cannot complete them, we have to generate them afterward
        // But at the same time, anonymous method uses info found during parsing, so we still have to generate parts of the classes...

        string CreateAnonymousClass(ITypeInfo declaringType);
        string AddAnonymousMethod(string className, IMethodReturnType returnType, IParameterDeclarationCollection parameters, StringData methodBody, ParsingInfo info);
        void AddFieldForAnonymousMethod(string className, string fieldName, string fieldDeclaration);
        void GenerateAnonymousClasses();
    }
}

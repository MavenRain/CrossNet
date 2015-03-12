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

using Reflector.CodeModel;

using CrossNet.Common;

namespace CrossNet.Interfaces
{
    public interface ITypeInfo
    {
        string Name
        {
            get;
        }
        string FullName
        {
            get;
        }
        string FullNameWithoutGeneric
        {
            get;
        }
        string DotNetFullName
        {
            get;
        }

        string NonScopedFullName
        {
            get;
        }
        string GetWrapperName(string declaringType);

        string Namespace
        {
            get;
        }

        ObjectType Type
        {
            get;
        }

        ITypeInfo BaseType
        {
            get;
        }

        bool IsPrimitiveType
        {
            get;
        }

        IList<ITypeInfo> UnionOfInterfaces
        {
            get;
        }

        IList<ITypeInfo> ExclusiveInterfaces
        {
            get;
        }

/*
        IList<IMethodDeclaration> Operators
        {
            get;
        }

        IList<IMethodDeclaration> Methods
        {
            get;
        }
*/

        ITypeReference TypeReference
        {
            get;
        }

        ITypeDeclaration TypeDeclaration
        {
            get;
        }

        bool IsValueType
        {
            get;
        }

        string PointerToMember
        {
            get;
        }

        LocalType LocalType
        {
            get;
        }

        bool IsGeneric
        {
            get;
        }

        int NumGenericArguments
        {
            get;
        }

        bool IsFirstLevelGeneric
        {
            get;
        }

        int RemainingDependencies
        {
            get;
#if !STANDARD_DEP
            set;
#endif
        }

        bool DependencyCleared
        {
            get;
            set;
        }

        string GetFullName(ParsingInfo info);
        string GetInstanceText(ParsingInfo info);
        string GetInstancePostFix();
        string GetPrefixedFullName(string prefix);
        bool IsBaseType(ITypeInfo baseType);
        void FillInfo(ITypeReference typeReference);
        void FillInfo(ITypeDeclaration typeDeclaration);
        void DetectDependencies();
        bool RemoveSolvedDependencies();
    }
}

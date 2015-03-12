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

using Reflector;
using Reflector.CodeModel;
using Reflector.CodeModel.Memory;

using CrossNet.Common;

namespace CrossNet.Interfaces
{
    public interface IReferenceGenerator
    {
        StringData GenerateCodeMethodReference(IMethodReference methodReference, ParsingInfo info);
        StringData GenerateCodeTypeReference(ITypeReference typeReference, ParsingInfo info);
        String     GenerateCodeTypeReferenceAsString(ITypeReference typeReference, ParsingInfo info);
        StringData GenerateCodeType(IType type, ParsingInfo info);
        String     GenerateCodeTypeAsString(IType type, ParsingInfo info);
        StringData GenerateCodeTypeWithPostfix(IType type, ParsingInfo info);
        StringData GenerateCodeVariableDeclaration(IVariableDeclaration variableDeclaration, ParsingInfo info);
        StringData GenerateCodeVariableReference(IVariableReference variableReference);
        StringData GenerateCodeFieldReference(IFieldReference fieldReference, ParsingInfo info);
        StringData GenerateCodeFieldDeclaration(IFieldDeclaration fieldDeclaration, ParsingInfo info);
        StringData GenerateCodeEnumDeclaration(IFieldDeclaration fieldDeclaration, ParsingInfo info);
        StringData GenerateCodePropertyDeclaration(IPropertyDeclaration propertyDeclaration, ParsingInfo info, out string returnValue);
        StringData GenerateCodePropertyDeclarationName(IPropertyDeclaration propertyDeclaration, ParsingInfo info);
        StringData GenerateCodePropertyDeclarationParameters(IPropertyDeclaration propertyDeclaration, ParsingInfo info);
        string GenerateCodeParameterDeclaration(IParameterDeclaration parameterDeclarations, ParsingInfo info);
        StringData GenerateCodeParameterDeclarationCollection(IParameterDeclarationCollection parameterDeclarations, ParsingInfo info);
        StringData GenerateCodeParameterCollection(IParameterDeclarationCollection parameterDeclarations, ParsingInfo info);
        StringData GenerateCodeParameterReference(IParameterReference parameterReference);
        StringData GenerateCodePropertyReference(IPropertyReference propertyReference);
        StringData GenerateCodeEventReference(IEventReference eventReference);
        StringData GenerateCodeEventDeclaration(IEventDeclaration eventDeclaration, ParsingInfo info);
        StringData GenerateCodeMemberReference(IMemberReference memberReference, ParsingInfo info);
//        string GetInstancePostfix();
    }
}

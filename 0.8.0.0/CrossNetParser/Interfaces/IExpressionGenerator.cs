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

using CrossNet.Common;

namespace CrossNet.Interfaces
{
    public delegate StringData GenerateExpressionDelegate(IExpression expression, ParsingInfo info);
    public delegate void ParseExpressionDelegate(IExpression expression, ParsingInfo info);

    public interface IExpressionGenerator
    {
        StringData GenerateCode(IExpression expression, ParsingInfo info);

        StringData GenerateCodeAddressDereference(IExpression passedExpression, ParsingInfo info);
        StringData GenerateCodeAddressOf(IExpression passedExpression, ParsingInfo info);
        StringData GenerateCodeAddressOut(IExpression passedExpression, ParsingInfo info);
        StringData GenerateCodeAddressReference(IExpression passedExpression, ParsingInfo info);
        StringData GenerateCodeAnonymousMethod(IExpression passedExpression, ParsingInfo info);
        StringData GenerateCodeArgumentList(IExpression passedExpression, ParsingInfo info);
        StringData GenerateCodeArgumentReference(IExpression passedExpression, ParsingInfo info);
        StringData GenerateCodeArrayCreate(IExpression passedExpression, ParsingInfo info);
        StringData GenerateCodeArrayIndexer(IExpression passedExpression, ParsingInfo info);
        StringData GenerateCodeAssign(IExpression passedExpression, ParsingInfo info);
        StringData GenerateCodeBaseReference(IExpression passedExpression, ParsingInfo info);
        StringData GenerateCodeBinary(IExpression passedExpression, ParsingInfo info);
        StringData GenerateCodeCanCast(IExpression passedExpression, ParsingInfo info);
        StringData GenerateCodeCast(IExpression passedExpression, ParsingInfo info);
        StringData GenerateCodeCondition(IExpression passedExpression, ParsingInfo info);
        StringData GenerateCodeDelegateCreate(IExpression passedExpression, ParsingInfo info);
        StringData GenerateCodeDelegateInvoke(IExpression passedExpression, ParsingInfo info);
        StringData GenerateCodeEventReference(IExpression passedExpression, ParsingInfo info);
        StringData GenerateCodeFieldOf(IExpression passedExpression, ParsingInfo info);
        StringData GenerateCodeFieldReference(IExpression passedExpression, ParsingInfo info);
        StringData GenerateCodeGenericDefault(IExpression passedExpression, ParsingInfo info);
        StringData GenerateCodeLiteral(IExpression passedExpression, ParsingInfo info);
        StringData GenerateCodeMemberInitializer(IExpression passedExpression, ParsingInfo info);
        StringData GenerateCodeMethodInvoke(IExpression passedExpression, ParsingInfo info);
        StringData GenerateCodeMethodInvokeArguments(IExpressionCollection arguments, ParsingInfo info);
        StringData GenerateCodeMethodOf(IExpression passedExpression, ParsingInfo info);
        StringData GenerateCodeMethodReference(IExpression passedExpression, ParsingInfo info);
        StringData GenerateCodeNullCoalescing(IExpression passedExpression, ParsingInfo info);
        StringData GenerateCodeObjectCreate(IExpression passedExpression, ParsingInfo info);
        StringData GenerateCodePropertyIndexer(IExpression passedExpression, ParsingInfo info);
        StringData GenerateCodePropertyReference(IExpression passedExpression, ParsingInfo info);
        StringData GenerateCodeSizeOf(IExpression passedExpression, ParsingInfo info);
        StringData GenerateCodeSnippet(IExpression passedExpression, ParsingInfo info);
        StringData GenerateCodeStackAllocate(IExpression passedExpression, ParsingInfo info);
        StringData GenerateCodeThisReference(IExpression passedExpression, ParsingInfo info);
        StringData GenerateCodeTryCast(IExpression passedExpression, ParsingInfo info);
        StringData GenerateCodeTypedReferenceCreate(IExpression passedExpression, ParsingInfo info);
        StringData GenerateCodeTypeOf(IExpression passedExpression, ParsingInfo info);
        StringData GenerateCodeTypeOfTypedReference(IExpression passedExpression, ParsingInfo info);
        StringData GenerateCodeTypeReference(IExpression passedExpression, ParsingInfo info);
        StringData GenerateCodeUnary(IExpression passedExpression, ParsingInfo info);
        StringData GenerateCodeValueOfTypedReference(IExpression passedExpression, ParsingInfo info);
        StringData GenerateCodeVariableDeclaration(IExpression passedExpression, ParsingInfo info);
        StringData GenerateCodeVariableReference(IExpression passedExpression, ParsingInfo info);

        void ParseExpression(IExpression passedExpression, ParsingInfo info);
        void ParseAddressDereference(IExpression passedExpression, ParsingInfo info);
        void ParseAddressOf(IExpression passedExpression, ParsingInfo info);
        void ParseAddressOut(IExpression passedExpression, ParsingInfo info);
        void ParseAddressReference(IExpression passedExpression, ParsingInfo info);
        void ParseAnonymousMethod(IExpression passedExpression, ParsingInfo info);
        void ParseArgumentList(IExpression passedExpression, ParsingInfo info);
        void ParseArgumentReference(IExpression passedExpression, ParsingInfo info);
        void ParseArrayCreate(IExpression passedExpression, ParsingInfo info);
        void ParseArrayIndexer(IExpression passedExpression, ParsingInfo info);
        void ParseAssign(IExpression passedExpression, ParsingInfo info);
        void ParseBaseReference(IExpression passedExpression, ParsingInfo info);
        void ParseBinary(IExpression passedExpression, ParsingInfo info);
        void ParseBlock(IExpression passedExpression, ParsingInfo info);
        void ParseCanCast(IExpression passedExpression, ParsingInfo info);
        void ParseCast(IExpression passedExpression, ParsingInfo info);
        void ParseCondition(IExpression passedExpression, ParsingInfo info);
        void ParseDelegateCreate(IExpression passedExpression, ParsingInfo info);
        void ParseDelegateInvoke(IExpression passedExpression, ParsingInfo info);
        void ParseEventReference(IExpression passedExpression, ParsingInfo info);
        void ParseFieldOf(IExpression passedExpression, ParsingInfo info);
        void ParseFieldReference(IExpression passedExpression, ParsingInfo info);
        void ParseGenericDefault(IExpression passedExpression, ParsingInfo info);
        void ParseLiteral(IExpression passedExpression, ParsingInfo info);
        void ParseMemberInitializer(IExpression passedExpression, ParsingInfo info);
        void ParseMethodInvoke(IExpression passedExpression, ParsingInfo info);
        void ParseMethodOf(IExpression passedExpression, ParsingInfo info);
        void ParseMethodReference(IExpression passedExpression, ParsingInfo info);
        void ParseNullCoalescing(IExpression passedExpression, ParsingInfo info);
        void ParseObjectCreate(IExpression passedExpression, ParsingInfo info);
        void ParsePropertyIndexer(IExpression passedExpression, ParsingInfo info);
        void ParsePropertyReference(IExpression passedExpression, ParsingInfo info);
        void ParseSizeOf(IExpression passedExpression, ParsingInfo info);
        void ParseSnippet(IExpression passedExpression, ParsingInfo info);
        void ParseStackAllocate(IExpression passedExpression, ParsingInfo info);
        void ParseThisReference(IExpression passedExpression, ParsingInfo info);
        void ParseTryCast(IExpression passedExpression, ParsingInfo info);
        void ParseTypedReferenceCreate(IExpression passedExpression, ParsingInfo info);
        void ParseTypeOf(IExpression passedExpression, ParsingInfo info);
        void ParseTypeOfTypedReference(IExpression passedExpression, ParsingInfo info);
        void ParseTypeReference(IExpression passedExpression, ParsingInfo info);
        void ParseUnary(IExpression passedExpression, ParsingInfo info);
        void ParseValueOfTypedReference(IExpression passedExpression, ParsingInfo info);
        void ParseVariableDeclaration(IExpression passedExpression, ParsingInfo info);
        void ParseVariableReference(IExpression passedExpression, ParsingInfo info);
    }
}

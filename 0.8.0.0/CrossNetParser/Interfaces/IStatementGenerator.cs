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
    public delegate StringData  GenerateStatementDelegate(IStatement expression, ParsingInfo info);
    public delegate void        ParseStatementDelegate(IStatement expression, ParsingInfo info);

    public interface IStatementGenerator
    {
        StringData GenerateCodeForMethod(IStatement statement, ParsingInfo info);

        StringData GenerateCode(IStatement statement, ParsingInfo info);
        StringData GenerateCodeBlock(IStatement statement, ParsingInfo info);
        StringData GenerateCodeAttachEvent(IStatement statement, ParsingInfo info);
        StringData GenerateCodeBreak(IStatement statement, ParsingInfo info);
        StringData GenerateCodeComment(IStatement statement, ParsingInfo info);
        StringData GenerateCodeCondition(IStatement statement, ParsingInfo info);
        StringData GenerateCodeContinue(IStatement statement, ParsingInfo info);
        StringData GenerateCodeDo(IStatement statement, ParsingInfo info);
        StringData GenerateCodeExpression(IStatement statement, ParsingInfo info);
        StringData GenerateCodeFixed(IStatement statement, ParsingInfo info);
        StringData GenerateCodeForEach(IStatement statement, ParsingInfo info);
        StringData GenerateCodeFor(IStatement statement, ParsingInfo info);
        StringData GenerateCodeGoto(IStatement statement, ParsingInfo info);
        StringData GenerateCodeLabeled(IStatement statement, ParsingInfo info);
        StringData GenerateCodeLock(IStatement statement, ParsingInfo info);
        StringData GenerateCodeMemoryCopy(IStatement statement, ParsingInfo info);
        StringData GenerateCodeMemoryInitialize(IStatement statement, ParsingInfo info);
        StringData GenerateCodeMethodReturn(IStatement statement, ParsingInfo info);
        StringData GenerateCodeRemoveEvent(IStatement statement, ParsingInfo info);
        StringData GenerateCodeSwitch(IStatement statement, ParsingInfo info);
        StringData GenerateCodeThrowException(IStatement statement, ParsingInfo info);
        StringData GenerateCodeTryCatchFinally(IStatement statement, ParsingInfo info);
        StringData GenerateCodeUsing(IStatement statement, ParsingInfo info);
        StringData GenerateCodeWhile(IStatement statement, ParsingInfo info);

        // Parse the statements before any generation...
        // This is used to detect bogus info from Reflector so we can patch them...
        // An example being some goto label with the corresponding label missing.
        // The only known occurence so far corresponds actually to a "continue" in a foreach statement in some particular conditions...
        // Going forward, we might have other issues like this...

        void ParseStatement(IStatement statement, ParsingInfo info);
        void ParseBlock(IStatement statement, ParsingInfo info);
        void ParseAttachEvent(IStatement statement, ParsingInfo info);
        void ParseBreak(IStatement statement, ParsingInfo info);
        void ParseComment(IStatement statement, ParsingInfo info);
        void ParseCondition(IStatement statement, ParsingInfo info);
        void ParseContinue(IStatement statement, ParsingInfo info);
        void ParseDo(IStatement statement, ParsingInfo info);
        void ParseExpression(IStatement statement, ParsingInfo info);
        void ParseFixed(IStatement statement, ParsingInfo info);
        void ParseForEach(IStatement statement, ParsingInfo info);
        void ParseFor(IStatement statement, ParsingInfo info);
        void ParseGoto(IStatement statement, ParsingInfo info);
        void ParseLabeled(IStatement statement, ParsingInfo info);
        void ParseLock(IStatement statement, ParsingInfo info);
        void ParseMemoryCopy(IStatement statement, ParsingInfo info);
        void ParseMemoryInitialize(IStatement statement, ParsingInfo info);
        void ParseMethodReturn(IStatement statement, ParsingInfo info);
        void ParseRemoveEvent(IStatement statement, ParsingInfo info);
        void ParseSwitch(IStatement statement, ParsingInfo info);
        void ParseThrowException(IStatement statement, ParsingInfo info);
        void ParseTryCatchFinally(IStatement statement, ParsingInfo info);
        void ParseUsing(IStatement statement, ParsingInfo info);
        void ParseWhile(IStatement statement, ParsingInfo info);
    }
}

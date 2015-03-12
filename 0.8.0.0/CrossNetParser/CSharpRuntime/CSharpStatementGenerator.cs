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
using System.Diagnostics;
using System.Text;

using Reflector;
using Reflector.CodeModel;

using CrossNet.Common;
using CrossNet.Interfaces;
using CrossNet.Net;

namespace CrossNet.CSharpRuntime
{
    public class CSharpStatementGenerator : IStatementGenerator
    {
        public StringData GenerateCodeForMethod(IStatement statement, ParsingInfo info)
        {
            if (info.Labels != null)
            {
                // Delete previous labels from a previous method
                info.Labels.Clear();
            }
            // First do a pre-parsing...
            ParseStatement(statement, info);
            // Then do the code generation...
            return (GenerateCode(statement, info));
        }

        public StringData GenerateCode(IStatement statement, ParsingInfo info)
        {
            info.ResetBetweenStatements();

            if (statement == null)
            {
                return (new StringData());
            }

            // Try to use the cache first
            // Get the type of the statement (i.e. the implementation)
            Type statementType = statement.GetType();
            // See if there is a corresponding delegate for this type...
            GenerateStatementDelegate del;
            bool foundIt = sDelegates.TryGetValue(statementType, out del);
            if (foundIt)
            {
                // Yes, invoke it
                return (del(statement, info));
            }

            // There was no corresponding delegate, do the slow comparison
            // At the same time, it will populate the hashtable for the next time...

            del = null;

            if (statement is IAttachEventStatement)
            {
                del = GenerateCodeAttachEvent;
            }
            else if (statement is IBlockStatement)
            {
                del = GenerateCodeBlock;
            }
            else if (statement is IBreakStatement)
            {
                del = GenerateCodeBreak;
            }
            else if (statement is ICommentStatement)
            {
                del = GenerateCodeComment;
            }
            else if (statement is IConditionStatement)
            {
                del = GenerateCodeCondition;
            }
            else if (statement is IContinueStatement)
            {
                del = GenerateCodeContinue;
            }
            else if (statement is IDoStatement)
            {
                del = GenerateCodeDo;
            }
            else if (statement is IExpressionStatement)
            {
                del = GenerateCodeExpression;
            }
            else if (statement is IFixedStatement)
            {
                del = GenerateCodeFixed;
            }
            else if (statement is IForEachStatement)
            {
                del = GenerateCodeForEach;
            }
            else if (statement is IForStatement)
            {
                del = GenerateCodeFor;
            }
            else if (statement is IGotoStatement)
            {
                del = GenerateCodeGoto;
            }
            else if (statement is ILabeledStatement)
            {
                del = GenerateCodeLabeled;
            }
            else if (statement is ILockStatement)
            {
                del = GenerateCodeLock;
            }
            else if (statement is IMemoryCopyStatement)
            {
                del = GenerateCodeMemoryCopy;
            }
            else if (statement is IMemoryInitializeStatement)
            {
                del = GenerateCodeMemoryInitialize;
            }
            else if (statement is IMethodReturnStatement)
            {
                del = GenerateCodeMethodReturn;
            }
            else if (statement is IRemoveEventStatement)
            {
                del = GenerateCodeRemoveEvent;
            }
            else if (statement is ISwitchStatement)
            {
                del = GenerateCodeSwitch;
            }
            else if (statement is IThrowExceptionStatement)
            {
                del = GenerateCodeThrowException;
            }
            else if (statement is ITryCatchFinallyStatement)
            {
                del = GenerateCodeTryCatchFinally;
            }
            else if (statement is IUsingStatement)
            {
                del = GenerateCodeUsing;
            }
            else if (statement is IWhileStatement)
            {
                del = GenerateCodeWhile;
            }

            Debug.Assert(del != null, "The delegate should be set!");
            // Add the delegate on the hashtable to optimize the next time
            sDelegates[statementType] = del;
            return (del(statement, info));
        }


        public StringData GenerateCodeBlock(IStatement statement, ParsingInfo info)
        {
            IBlockStatement blockStatement = (IBlockStatement)statement;
            StringData data = new StringData();
            data.Append("{\n");
            ++data.Indentation;
            foreach (IStatement childStatement in blockStatement.Statements)
            {
                data.Append(GenerateCode(childStatement, info));
            }
            --data.Indentation;
            data.Append("}\n");
            return (data);
        }

        public StringData GenerateCodeAttachEvent(IStatement statement, ParsingInfo info)
        {
            IAttachEventStatement attachEventStatement = (IAttachEventStatement)statement;
            StringData data = LanguageManager.ExpressionGenerator.GenerateCode(attachEventStatement.Event, info);
            data.AppendSameLine(" += ");
            data.AppendSameLine(LanguageManager.ExpressionGenerator.GenerateCode(attachEventStatement.Listener, info));
            data.AppendSameLine(";\n");
            return (data);
        }

        public StringData GenerateCodeBreak(IStatement statement, ParsingInfo info)
        {
            IBreakStatement breakStatement = (IBreakStatement)statement;
            StringData data = new StringData("break;\n");
            return (data);
        }

        public StringData GenerateCodeComment(IStatement statement, ParsingInfo info)
        {
            ICommentStatement commentStatement = (ICommentStatement)statement;
            StringData data = new StringData("// ");
            data.AppendSameLine(commentStatement.Comment.Text);
            data.AppendSameLine("\n");
            return (data);
        }

        public StringData GenerateCodeCondition(IStatement statement, ParsingInfo info)
        {
            IConditionStatement conditionStatement = (IConditionStatement)statement;
            StringData data = new StringData("if (");
            data.Append(LanguageManager.ExpressionGenerator.GenerateCode(conditionStatement.Condition, info));
            data.Append(")\n");
            data.Append(GenerateCode(conditionStatement.Then, info));

            if (IsBlockStatementEmpty(conditionStatement.Else) == false)
            {
                data.Append("else\n");
                data.Append(GenerateCode(conditionStatement.Else, info));
            }
            return (data);
        }

        public StringData GenerateCodeContinue(IStatement statement, ParsingInfo info)
        {
            IContinueStatement continueStatement = (IContinueStatement)statement;
            StringData data = new StringData("continue;\n");
            return (data);
        }

        public StringData GenerateCodeDo(IStatement statement, ParsingInfo info)
        {
            IDoStatement doStatement = (IDoStatement)statement;
            StringData data = new StringData("do\n");
            data.Append(GenerateCode(doStatement.Body, info));
            data.Append("while (");
            data.Append(LanguageManager.ExpressionGenerator.GenerateCode(doStatement.Condition, info));
            data.Append(");\n");
            return (data);
        }

        public StringData GenerateCodeExpression(IStatement statement, ParsingInfo info)
        {
            IExpressionStatement expressionStatement = (IExpressionStatement)statement;
            StringData data = LanguageManager.ExpressionGenerator.GenerateCode(expressionStatement.Expression, info);
            data.Append(";\n");
            return (data);
        }

        public StringData GenerateCodeFixed(IStatement statement, ParsingInfo info)
        {
            IFixedStatement fixedStatement = (IFixedStatement)statement;
            StringData data = new StringData("fixed (");
            StringData variableDeclaration = LanguageManager.ReferenceGenerator.GenerateCodeVariableDeclaration(fixedStatement.Variable, info);
            data.AppendSameLine(variableDeclaration);
            data.AppendSameLine(" = ");

            StringData subExpression = null;
            // Look for the specific case with implicit conversion from string to char *
            LocalType varType = variableDeclaration.LocalType;
            if (LanguageManager.LocalTypeManager.TypeCharPointer.Same(varType))
            {
                ICastExpression castExpression = fixedStatement.Expression as ICastExpression;
                if (castExpression != null)
                {
                    subExpression = LanguageManager.ExpressionGenerator.GenerateCode(castExpression.Expression, info);
                    LocalType exprType = subExpression.LocalType;
                    if (LanguageManager.LocalTypeManager.TypeString.Same(exprType))
                    {
                        // Do nothing...
                        // subExpression is already set correctly...
                    }
                    else
                    {
                        // We are not implicitly casting String to Char * (skipping the cast).
                        // null the subExpression to use the default behavior
                        subExpression = null;
                    }
                }
            }

            if (subExpression == null)
            {
                subExpression = LanguageManager.ExpressionGenerator.GenerateCode(fixedStatement.Expression, info);
            }

            data.AppendSameLine(subExpression);
            data.AppendSameLine(")\n");
            data.Append(GenerateCode(fixedStatement.Body, info));
            info.UnsafeMethod = true; // Fixed implies unsafe code...
            return (data);
        }

        public StringData GenerateCodeForEach(IStatement statement, ParsingInfo info)
        {
            IForEachStatement forEachStatement = (IForEachStatement)statement;
            StringData data = new StringData("foreach (");
            data.AppendSameLine(LanguageManager.ReferenceGenerator.GenerateCodeVariableDeclaration(forEachStatement.Variable, info));
            data.AppendSameLine(" in ");
            StringData inData = LanguageManager.ExpressionGenerator.GenerateCode(forEachStatement.Expression, info);
            if (inData.Text == "base")
            {
                // Special case here... If "base" it has actually be incorrectly translated fomr "this" by reflector
                data.AppendSameLine("this");
            }
            else
            {
                data.AppendSameLine(inData);
            }
            data.AppendSameLine(")\n");
            data.Append(GenerateCode(forEachStatement.Body, info));
            return (data);
        }

        public StringData GenerateCodeFor(IStatement statement, ParsingInfo info)
        {
            IForStatement forStatement = (IForStatement)statement;
            StringData data = new StringData("for (");
            string text;

            if (forStatement.Initializer != null)
            {
                text = GenerateCode(forStatement.Initializer, info).Text;
                text = text.TrimEnd(';', '\n');
                text += " ; ";
            }
            else
            {
                text = " ";
            }
            data.AppendSameLine(text);
            data.AppendSameLine(LanguageManager.ExpressionGenerator.GenerateCode(forStatement.Condition, info));
            data.AppendSameLine(" ; ");

            if (forStatement.Increment != null)
            {
                text = GenerateCode(forStatement.Increment, info).Text;
                text = text.TrimEnd(';', '\n');
            }
            else
            {
                text = " ";
            }
            data.AppendSameLine(text);
            data.AppendSameLine(")\n");

            data.Append(GenerateCode(forStatement.Body, info));
            return (data);
        }

        public StringData GenerateCodeGoto(IStatement statement, ParsingInfo info)
        {
            IGotoStatement gotoStatement = (IGotoStatement)statement;
            StringData data;
            if ((info.Labels == null) || (info.Labels.ContainsKey(gotoStatement.Name) == false))
            {
                // This is a reflector bug here...
                // It gives a goto statement to a a label that doesn't exist...
                // For the moment, the only known case is a "continue" in some foreach construction
                // Assume this the occurence that we are solving...
                data = new StringData("continue;\n");
            }
            else
            {
                data = new StringData("goto ");
                data.Append(gotoStatement.Name);
                data.Append(";\n");
            }
            return (data);
        }

        public StringData GenerateCodeLabeled(IStatement statement, ParsingInfo info)
        {
            ILabeledStatement labeledStatement = (ILabeledStatement)statement;
            StringData data = new StringData(labeledStatement.Name);
            if (labeledStatement.Statement != null)
            {
                data.Append(":\n");
                data.Append(GenerateCode(labeledStatement.Statement, info));
            }
            else
            {
                // No statement, make sure we have a fake statement otherwise it is considered as incorrect syntax
                // Comment in C#, we should not be able to have this case but because of the Reflector decompilation
                // This case can happen...
                data.Append(":;\n");
            }
            return (data);
        }

        public StringData GenerateCodeLock(IStatement statement, ParsingInfo info)
        {
            ILockStatement lockStatement = (ILockStatement)statement;
            StringData data = new StringData("lock (");
            data.AppendSameLine(LanguageManager.ExpressionGenerator.GenerateCode(lockStatement.Expression, info));
            data.AppendSameLine(")\n");
            data.Append(GenerateCode(lockStatement.Body, info));
            return (data);
        }

        public StringData GenerateCodeMemoryCopy(IStatement statement, ParsingInfo info)
        {
            IMemoryCopyStatement memoryCopyStatement = (IMemoryCopyStatement)statement;
            Debug.Fail("Not implemented!");
            return (null);
        }

        public StringData GenerateCodeMemoryInitialize(IStatement statement, ParsingInfo info)
        {
            IMemoryInitializeStatement memoryInitializeStatement = (IMemoryInitializeStatement)statement;
            Debug.Fail("Not implemented!");
            return (null);
        }

        public StringData GenerateCodeMethodReturn(IStatement statement, ParsingInfo info)
        {
            IMethodReturnStatement methodReturn = (IMethodReturnStatement)statement;
            StringData data;
            if (methodReturn.Expression != null)
            {
                data = new StringData("return (");
                StringData returnedValue = LanguageManager.ExpressionGenerator.GenerateCode(methodReturn.Expression, info);
                returnedValue = LanguageManager.LocalTypeManager.DoesNeedCast(info.ReturnedType, returnedValue.LocalType, returnedValue);
                data.AppendSameLine(returnedValue);
                data.AppendSameLine(");\n");
            }
            else
            {
                data = new StringData("return;\n");
            }
            return (data);
        }

        public StringData GenerateCodeRemoveEvent(IStatement statement, ParsingInfo info)
        {
            IRemoveEventStatement removeEventStatement = (IRemoveEventStatement)statement;
            StringData data = LanguageManager.ExpressionGenerator.GenerateCode(removeEventStatement.Event, info);
            data.AppendSameLine(" -= ");
            data.AppendSameLine(LanguageManager.ExpressionGenerator.GenerateCode(removeEventStatement.Listener, info));
            data.AppendSameLine(";\n");
            return (data);
        }

        public StringData GenerateCodeSwitch(IStatement statement, ParsingInfo info)
        {
            ISwitchStatement switchStatement = (ISwitchStatement)statement;
            StringData data = new StringData("switch (");
            StringData switchValue = LanguageManager.ExpressionGenerator.GenerateCode(switchStatement.Expression, info);

            if (info.CurrentSwitch == null)
            {
                info.CurrentSwitch = new Stack<LocalType>();
            }

            // Push the value type on the stack
            info.CurrentSwitch.Push(switchValue.LocalType);

            data.Append(switchValue);
            data.Append(")\n");
            data.Append("{\n");
            ++data.Indentation;
            foreach (ISwitchCase switchCase in switchStatement.Cases)
            {
                if (switchCase is IDefaultCase)
                {
                    data.Append("default:\n");
                }
                else
                {
                    IConditionCase conditionCase = (IConditionCase)switchCase;
                    GenerateCodeCase(conditionCase.Condition, data, info);
                }

                data.Append(GenerateCode(switchCase.Body, info));
            }
            --data.Indentation;
            data.Append("}\n");

            // Pop the value stack from the stack
            info.CurrentSwitch.Pop();

            return (data);
        }

        private void GenerateCodeCase(IExpression expression, StringData data, ParsingInfo info)
        {
            IBinaryExpression caseExpression = expression as IBinaryExpression;
            if ((caseExpression != null) && (caseExpression.Operator == BinaryOperator.BooleanOr))
            {
                // Several contiguous cases are actually a "||" of the cases
                // And add the case statements recursively
                GenerateCodeCase(caseExpression.Left, data, info);
                GenerateCodeCase(caseExpression.Right, data, info);
                return;
            }
            // We have a lead in the case, add them correctly...
            data.Append("case ");
            StringData caseData = LanguageManager.ExpressionGenerator.GenerateCode(expression, info);
            if (caseData.Text == "System.String.Empty")
            {
                data.AppendSameLine("\"\":\n");
            }
            else
            {
                if (LanguageManager.LocalTypeManager.TypeInt32.Same(caseData.LocalType)
                    && LanguageManager.LocalTypeManager.TypeChar.Same(info.CurrentSwitch.Peek()))
                {
                    // The constant is an integer, but the variable is of type char
                    // This is following the reflector bug with literal char bigger than 0x7fff
                    // We have to cast it explicitly...

                    data.AppendSameLine("(System.Char)(");
                    data.AppendSameLine(caseData);
                    data.AppendSameLine("):\n");
                }
                else
                {
                    data.AppendSameLine(caseData);
                    data.AppendSameLine(":\n");
                }
            }
        }

        public StringData GenerateCodeThrowException(IStatement statement, ParsingInfo info)
        {
            IThrowExceptionStatement throwStatement = (IThrowExceptionStatement)statement;
            StringData data = new StringData("throw ");
            data.AppendSameLine(LanguageManager.ExpressionGenerator.GenerateCode(throwStatement.Expression, info));
            data.AppendSameLine(";\n");
            return (data);
        }

        public StringData GenerateCodeTryCatchFinally(IStatement statement, ParsingInfo info)
        {
            ITryCatchFinallyStatement tryStatement = (ITryCatchFinallyStatement)statement;
            StringData data = new StringData("try\n");
            data.Append(GenerateCode(tryStatement.Try, info));
            foreach (ICatchClause catchClause in tryStatement.CatchClauses)
            {
                data.Append("catch");
                if (catchClause.Variable.VariableType.ToString().ToLower() == "object")
                {
                    // If object, it means catch anything...

                    // Actually it means that we catch RuntimeWrappedException...
//                    data.AppendSameLine(" (System.Runtime.CompilerServices.RuntimeWrappedException)");
                }
                else
                {
                    data.AppendSameLine(" (");
                    data.AppendSameLine(LanguageManager.ReferenceGenerator.GenerateCodeVariableDeclaration(catchClause.Variable, info));
                    data.AppendSameLine(")");
                }
                data.AppendSameLine("\n");

                if (catchClause.Condition != null)
                {
                    Debug.Fail("Don't know what this is!");
                    // Don't know what the condition is, is it code to detect what the exception is?
                    data.Append(LanguageManager.ExpressionGenerator.GenerateCode(catchClause.Condition, info));
                }
                data.Append(GenerateCode(catchClause.Body, info));
            }

            if (IsBlockStatementEmpty(tryStatement.Finally) == false)
            {
                data.Append("finally\n");
                data.Append(GenerateCode(tryStatement.Finally, info));
            }

/*
 * Deactivated as I don't know what it is used for!
            if (tryStatement.Fault != null)
            {
                data.Append("Fault!");
                data.Append(1, GenerateCode(tryStatement.Fault));
            }
 */
            return (data);
        }

        public StringData GenerateCodeUsing(IStatement statement, ParsingInfo info)
        {
            IUsingStatement usingStatement = (IUsingStatement)statement;
            StringData data = new StringData("using (");
            data.AppendSameLine(LanguageManager.ExpressionGenerator.GenerateCode(usingStatement.Variable, info));
            data.AppendSameLine(" = ");
            data.AppendSameLine(LanguageManager.ExpressionGenerator.GenerateCode(usingStatement.Expression, info));
            data.AppendSameLine(")\n");
            data.Append(GenerateCode(usingStatement.Body, info));
            return (data);
        }

        public StringData GenerateCodeWhile(IStatement statement, ParsingInfo info)
        {
            IWhileStatement whileStatement = (IWhileStatement)statement;
            StringData data = new StringData("while (");
            data.Append(LanguageManager.ExpressionGenerator.GenerateCode(whileStatement.Condition, info));
            data.Append(")\n");
            data.Append(GenerateCode(whileStatement.Body, info));
            return (data);
        }

        public bool IsBlockStatementEmpty(IBlockStatement statement)
        {
            if (statement == null)
            {
                return (true);
            }
            if (statement.Statements == null)
            {
                return (true);
            }
            if (statement.Statements.Count == 0)
            {
                return (true);
            }
            return (false);
        }

        public void ParseStatement(IStatement statement, ParsingInfo info)
        {
            if (statement == null)
            {
                return;
            }

            // Try to use the cache first
            // Get the type of the statement (i.e. the implementation)
            Type statementType = statement.GetType();
            // See if there is a corresponding delegate for this type...
            ParseStatementDelegate del;
            bool foundIt = sParseDelegates.TryGetValue(statementType, out del);
            if (foundIt)
            {
                // Yes, there is one delegate
                if (del != null)
                {
                    // And it is not null, so it means that the statement has to be parsed...
                    del(statement, info);
                }
                return;
            }

            // There was no corresponding delegate, do the slow comparison
            // At the same time, it will populate the hashtable for the next time...

            del = null;

            if (statement is IBlockStatement)
            {
                del = ParseBlock;
            }
            else if (statement is IAttachEventStatement)
            {
                del = ParseAttachEvent;
            }
            else if (statement is IBreakStatement)
            {
                del = ParseBreak;
            }
            else if (statement is ICommentStatement)
            {
                del = ParseComment;
            }
            else if (statement is IConditionStatement)
            {
                del = ParseCondition;
            }
            else if (statement is IContinueStatement)
            {
                del = ParseContinue;
            }
            else if (statement is IDoStatement)
            {
                del = ParseDo;
            }
            else if (statement is IExpressionStatement)
            {
                del = ParseExpression;
            }
            else if (statement is IFixedStatement)
            {
                del = ParseFixed;
            }
            else if (statement is IForEachStatement)
            {
                del = ParseForEach;
            }
            else if (statement is IForStatement)
            {
                del = ParseFor;
            }
            else if (statement is IGotoStatement)
            {
                del = ParseGoto;
            }
            else if (statement is ILabeledStatement)
            {
                // That's the real one we care about...
                del = ParseLabeled;
            }
            else if (statement is ILockStatement)
            {
                del = ParseLock;
            }
            else if (statement is IMemoryCopyStatement)
            {
                del = ParseMemoryCopy;
            }
            else if (statement is IMemoryInitializeStatement)
            {
                del = ParseMemoryInitialize;
            }
            else if (statement is IMethodReturnStatement)
            {
                del = ParseMethodReturn;
            }
            else if (statement is IRemoveEventStatement)
            {
                del = ParseRemoveEvent;
            }
            else if (statement is ISwitchStatement)
            {
                del = ParseSwitch;
            }
            else if (statement is IThrowExceptionStatement)
            {
                del = ParseThrowException;
            }
            else if (statement is ITryCatchFinallyStatement)
            {
                del = ParseTryCatchFinally;
            }
            else if (statement is IUsingStatement)
            {
                del = ParseUsing;
            }
            else if (statement is IWhileStatement)
            {
                del = ParseWhile;
            }
            else
            {
                // Set the delegate to null for statements that are not doing anything!
                del = null;
            }

            // Add the delegate on the hashtable to optimize the next time
            sParseDelegates[statementType] = del;
            if (del != null)
            {
                // The statement can be parsed, invoke it... 
                del(statement, info);
            }
        }

        public void ParseBlock(IStatement statement, ParsingInfo info)
        {
            IBlockStatement blockStatement = (IBlockStatement)statement;
            foreach (IStatement subStatement in blockStatement.Statements)
            {
                ParseStatement(subStatement, info);
            }
        }

        public void ParseAttachEvent(IStatement statement, ParsingInfo info)
        {
            IAttachEventStatement attachEventStatement = (IAttachEventStatement)statement;
            LanguageManager.ExpressionGenerator.ParseEventReference(attachEventStatement.Event, info);
            LanguageManager.ExpressionGenerator.ParseExpression(attachEventStatement.Listener, info);
        }

        public void ParseBreak(IStatement statement, ParsingInfo info)
        {
            IBreakStatement breakStatement = (IBreakStatement)statement;
        }

        public void ParseComment(IStatement statement, ParsingInfo info)
        {
            ICommentStatement commentStatement = (ICommentStatement)statement;
        }

        public void ParseCondition(IStatement statement, ParsingInfo info)
        {
            IConditionStatement conditionStatement = (IConditionStatement)statement;
            LanguageManager.ExpressionGenerator.ParseExpression(conditionStatement.Condition, info);
            ParseStatement(conditionStatement.Then, info);
            ParseStatement(conditionStatement.Else, info);
        }

        public void ParseContinue(IStatement statement, ParsingInfo info)
        {
            IContinueStatement continueStatement = (IContinueStatement)statement;
        }

        public void ParseDo(IStatement statement, ParsingInfo info)
        {
            IDoStatement doStatement = (IDoStatement)statement;
            LanguageManager.ExpressionGenerator.ParseExpression(doStatement.Condition, info);
            ParseStatement(doStatement.Body, info);
        }

        public void ParseExpression(IStatement statement, ParsingInfo info)
        {
            IExpressionStatement expressionStatement = (IExpressionStatement)statement;
            LanguageManager.ExpressionGenerator.ParseExpression(expressionStatement.Expression, info);
        }

        public void ParseFixed(IStatement statement, ParsingInfo info)
        {
            IFixedStatement fixedStatement = (IFixedStatement)statement;
            LanguageManager.ExpressionGenerator.ParseExpression(fixedStatement.Expression, info);
            ParseStatement(fixedStatement.Body, info);
        }

        public void ParseForEach(IStatement statement, ParsingInfo info)
        {
            IForEachStatement forEachStatement = (IForEachStatement)statement;
            LanguageManager.ExpressionGenerator.ParseExpression(forEachStatement.Expression, info);
            ParseStatement(forEachStatement.Body, info);
        }

        public void ParseFor(IStatement statement, ParsingInfo info)
        {
            IForStatement forStatement = (IForStatement)statement;
            LanguageManager.ExpressionGenerator.ParseExpression(forStatement.Condition, info);
            ParseStatement(forStatement.Initializer, info);
            ParseStatement(forStatement.Increment, info);
            ParseStatement(forStatement.Body, info);
        }

        public void ParseGoto(IStatement statement, ParsingInfo info)
        {
            IGotoStatement gotoStatement = (IGotoStatement)statement;
            /*
             * Although we are looking at patching the goto statement, in this case we just don't care about the goto statement
             */
        }

        public void ParseLabeled(IStatement statement, ParsingInfo info)
        {
            // This is currently the only reason why we are parsing the function
            // Mainly to know the defined label, so we can detect if a goto is invalid
            ILabeledStatement labeledStatement = (ILabeledStatement)statement;
            if (info.Labels == null)
            {
                info.Labels = new Dictionary<string, string>();
            }
            info.Labels.Add(labeledStatement.Name, labeledStatement.Name);
            ParseStatement(labeledStatement.Statement, info);
        }

        public void ParseLock(IStatement statement, ParsingInfo info)
        {
            ILockStatement lockStatement = (ILockStatement)statement;
            LanguageManager.ExpressionGenerator.ParseExpression(lockStatement.Expression, info);
            ParseStatement(lockStatement.Body, info);
        }

        public void ParseMemoryCopy(IStatement statement, ParsingInfo info)
        {
            IMemoryCopyStatement memoryCopyStatement = (IMemoryCopyStatement)statement;
            LanguageManager.ExpressionGenerator.ParseExpression(memoryCopyStatement.Source, info);
            LanguageManager.ExpressionGenerator.ParseExpression(memoryCopyStatement.Destination, info);
            LanguageManager.ExpressionGenerator.ParseExpression(memoryCopyStatement.Length, info);
        }

        public void ParseMemoryInitialize(IStatement statement, ParsingInfo info)
        {
            IMemoryInitializeStatement memoryInitializeStatement = (IMemoryInitializeStatement)statement;
            LanguageManager.ExpressionGenerator.ParseExpression(memoryInitializeStatement.Offset, info);
            LanguageManager.ExpressionGenerator.ParseExpression(memoryInitializeStatement.Length, info);
            LanguageManager.ExpressionGenerator.ParseExpression(memoryInitializeStatement.Value, info);
        }

        public void ParseMethodReturn(IStatement statement, ParsingInfo info)
        {
            IMethodReturnStatement methodReturnStatement = (IMethodReturnStatement)statement;
            LanguageManager.ExpressionGenerator.ParseExpression(methodReturnStatement.Expression, info);
        }

        public void ParseRemoveEvent(IStatement statement, ParsingInfo info)
        {
            IRemoveEventStatement removeEventStatement = (IRemoveEventStatement)statement;
            LanguageManager.ExpressionGenerator.ParseEventReference(removeEventStatement.Event, info);
            LanguageManager.ExpressionGenerator.ParseExpression(removeEventStatement.Listener, info);
        }

        public void ParseSwitch(IStatement statement, ParsingInfo info)
        {
            ISwitchStatement switchStatement = (ISwitchStatement)statement;
            LanguageManager.ExpressionGenerator.ParseExpression(switchStatement.Expression, info);
            foreach (ISwitchCase switchCase in switchStatement.Cases)
            {
                ParseStatement(switchCase.Body, info);
            }
        }

        public void ParseThrowException(IStatement statement, ParsingInfo info)
        {
            IThrowExceptionStatement throwExceptionStatement = (IThrowExceptionStatement)statement;
            LanguageManager.ExpressionGenerator.ParseExpression(throwExceptionStatement.Expression, info);
        }

        public void ParseTryCatchFinally(IStatement statement, ParsingInfo info)
        {
            ITryCatchFinallyStatement tryCatchFinallyStatement = (ITryCatchFinallyStatement)statement;
            ParseStatement(tryCatchFinallyStatement.Try, info);
            foreach (ICatchClause catchClause in tryCatchFinallyStatement.CatchClauses)
            {
                ParseStatement(catchClause.Body, info);
            }
            ParseStatement(tryCatchFinallyStatement.Finally, info);
            ParseStatement(tryCatchFinallyStatement.Fault, info);
        }

        public void ParseUsing(IStatement statement, ParsingInfo info)
        {
            IUsingStatement usingStatement = (IUsingStatement)statement;
            LanguageManager.ExpressionGenerator.ParseExpression(usingStatement.Expression, info);
            LanguageManager.ExpressionGenerator.ParseExpression(usingStatement.Variable, info);
            ParseStatement(usingStatement.Body, info);
        }

        public void ParseWhile(IStatement statement, ParsingInfo info)
        {
            IWhileStatement whileStatement = (IWhileStatement)statement;
            LanguageManager.ExpressionGenerator.ParseExpression(whileStatement.Condition, info);
            ParseStatement(whileStatement.Body, info);
        }

        private IDictionary<Type, GenerateStatementDelegate> sDelegates = new Dictionary<Type, GenerateStatementDelegate>();
        private IDictionary<Type, ParseStatementDelegate> sParseDelegates = new Dictionary<Type, ParseStatementDelegate>();
    }
}

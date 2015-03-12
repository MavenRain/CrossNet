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
using System.Collections.Specialized;
using System.Diagnostics;
using System.Text;

using Reflector;
using Reflector.CodeModel;

using CrossNet.Common;
using CrossNet.Interfaces;
using CrossNet.Net;

namespace CrossNet.CppRuntime
{
    public class CppStatementGenerator : IStatementGenerator
    {
        public StringData GenerateCodeForMethod(IStatement statement, ParsingInfo info)
        {
            if (info.Labels != null)
            {
                // Delete previous labels from a previous method
                info.Labels.Clear();
            }
            // First do a pre-parsing...
            // The pre-parsing is used to detect incorrect label and generate correctly anonymous methods
            // It parses statements and expressions recursively
            ParseStatement(statement, info);

            // All the variables that have been used by anonymous method must be transformed
            StringData anonymousVariables = FixupVariableName(info);

            // Then do the code generation...
            StringData method = GenerateCode(statement, info);

            // We don't want these variables to pollute another method
            info.ClearAnonymousVariables();
            info.AtLeastOneAnonymousMethod = false;

            if (anonymousVariables != null)
            {
                // Patch the method to add what the anonymous variables...
                anonymousVariables.Append(method);
                anonymousVariables.Indentation--;
                anonymousVariables.Append("}\n");
                method = anonymousVariables;
            }

            return (method);
        }

        public StringData FixupVariableName(ParsingInfo info)
        {
            if (info.AtLeastOneAnonymousMethod == false)
            {
                return (null);
            }

            // There is at least one anonymous method, we have to generate the corresponding type
            info.AnonymousMethodClass = LanguageManager.TypeGenerator.CreateAnonymousClass(TypeInfoManager.GetTypeInfo(info.DeclaringType));

            if (info.Variables == null)
            {
                // Nothing to do as there is no variable used by anonymous methods
                return (null);
            }
            if (info.Variables.Count == 0)
            {
                return (null);
            }

            StringData initVariables = new StringData();
            initVariables.Append("{\n");
            initVariables.Indentation++;

            StringCollection variablesToDeclare = new StringCollection();

            // name of the pointer that contains access to 'this', 'base', 'parameter' and 'local variable'
            string pointerVariables = CppUtil.GetNextTempVariable();
            info.AnonymousMethodObject = pointerVariables;

            initVariables.Append(info.AnonymousMethodClass + " * " + pointerVariables + " = " + info.AnonymousMethodClass + "::__Create__();\n");

            foreach (AnonymousVariable var in info.Variables.Values)
            {
                string fieldName = null;
                string fieldDeclaration = null;
                ITypeInfo declaringType;
                switch (var.Kind)
                {
                    case Kind.This:
                        fieldName = "__this__";
                        var.NewName = pointerVariables + "->" + fieldName;
                        variablesToDeclare.Add(fieldName);

                        // For parameter there is a little twist...
                        // We have to copy the parameter first on the structure...
                        initVariables.Append(var.NewName + " = this;\n");

                        declaringType = TypeInfoManager.GetTypeInfo(info.DeclaringType);
                        fieldDeclaration = declaringType.FullName + declaringType.GetInstancePostFix() + " __this__";
                        break;

                    case Kind.Base:
                        fieldName = "__base__";
                        var.NewName = pointerVariables + "->" + fieldName;
                        variablesToDeclare.Add(fieldName);

                        // For parameter there is a little twist...
                        // We have to copy the parameter first on the structure...
                        initVariables.Append(var.NewName + " = this;\n");

                        declaringType = TypeInfoManager.GetTypeInfo(info.DeclaringType);
                        if (declaringType.BaseType != null)
                        {
                            declaringType = declaringType.BaseType;
                        }
                        fieldDeclaration = declaringType.FullName + declaringType.GetInstancePostFix() + " __base__";
                        break;

                    case Kind.LocalVariable:
                        fieldName = var.OldName;
                        var.NewName = pointerVariables + "->" + fieldName;
                        variablesToDeclare.Add(fieldName);

                        // For local variable, the init will be done at declaration time...

                        fieldDeclaration = LanguageManager.ReferenceGenerator.GenerateCodeVariableDeclaration(var.VariableDeclaration, info).Text;
                        break;

                    case Kind.Parameter:
                        if (var.Declared == Declared.Inside)
                        {
                            // If the parameter was declared inside (like the parameter for the anonymous method)
                            // There is nothing to patch
                            break;
                        }
                        fieldName = var.OldName;
                        var.NewName = pointerVariables + "->" + fieldName;
                        variablesToDeclare.Add(fieldName);

                        // For parameter there is a little twist...
                        // We have to copy the parameter first on the structure...
                        initVariables.Append(var.NewName + " = " + var.OldName + ";\n");

                        fieldDeclaration = LanguageManager.ReferenceGenerator.GenerateCodeParameterDeclaration(var.ParameterDeclaration, info);
                        break;

                    default:
                        Debug.Fail("Should not be here!");
                        continue;
                }

                if (fieldDeclaration != null)
                {
                    LanguageManager.TypeGenerator.AddFieldForAnonymousMethod(info.AnonymousMethodClass, fieldName, fieldDeclaration);
                }
            }

            // We now need to populate the skeleton for the class
            // And all the associated methods...
            return (initVariables);
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
            bool foundIt = sGenerateDelegates.TryGetValue(statementType, out del);
            StringData thisStatement;
            if (foundIt)
            {
                // Yes, invoke it
                thisStatement = del(statement, info);
                return (Util.CombineStatements(thisStatement, info));
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
            sGenerateDelegates[statementType] = del;
            thisStatement = del(statement, info);
            return (Util.CombineStatements(thisStatement, info));
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
#if false
            IAttachEventStatement attachEventStatement = (IAttachEventStatement)statement;
            StringData eventToModify = LanguageManager.ExpressionGenerator.GenerateCode(attachEventStatement.Event, info);
            StringData delegateToAdd = LanguageManager.ExpressionGenerator.GenerateCode(attachEventStatement.Listener, info);
            StringData data = new StringData(eventToModify.ToString());
            data.AppendSameLine(" = ");

            StringData value = new StringData("System::Delegate::Combine(");
            value.AppendSameLine(eventToModify);
            value.AppendSameLine(", ");
            value.AppendSameLine(delegateToAdd);
            value.AppendSameLine(")");

            value = LanguageManager.LocalTypeManager.DoesNeedCast(eventToModify.LocalType, LanguageManager.LocalTypeManager.TypeDelegate, value);
            data.AppendSameLine(value);
            data.AppendSameLine(";\n");
            return (data);
#else
            IAttachEventStatement attachEventStatement = (IAttachEventStatement)statement;
            StringData target = LanguageManager.ExpressionGenerator.GenerateCode(attachEventStatement.Event.Target, info);
            StringData delegateToAdd = LanguageManager.ExpressionGenerator.GenerateCode(attachEventStatement.Listener, info);

//            LocalType expectedDelegateType = LanguageManager.LocalTypeManager.GetLocalType(attachEventStatement.Event.Event.EventType);

            ITypeInfo targetTypeInfo = target.LocalType.GetTypeInfo();
            StringData data;
            if (targetTypeInfo.IsValueType)
            {
                data = target;
                data.AppendSameLine(".");
            }
            else if (targetTypeInfo.Type == ObjectType.INTERFACE)
            {
                string tempVariable = CppUtil.GetNextTempVariable();
                data = new StringData();
                data.Append(targetTypeInfo.FullName + targetTypeInfo.GetInstancePostFix() + tempVariable + " = " + target + ";\n");
                string call = CppUtil.InterfaceCall(tempVariable, targetTypeInfo.FullName, "add_" + attachEventStatement.Event.Event.Name, delegateToAdd.Text);
                data.Append(call + ";\n");
                return (data);
            }
            else
            {
                data = target;
                data.AppendSameLine("->");
            }

            data.AppendSameLine("add_" + attachEventStatement.Event.Event.Name + "(");
            data.AppendSameLine(delegateToAdd);
            data.AppendSameLine(")");
/*
            data = LanguageManager.LocalTypeManager.DoesNeedCast(expectedDelegateType, LanguageManager.LocalTypeManager.TypeDelegate, data);
            data.AppendSameLine(data);
 */
            data.AppendSameLine(";\n");
            return (data);
#endif
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
            StringData condition = LanguageManager.ExpressionGenerator.GenerateCode(conditionStatement.Condition, info);
            // The condition might generate some pre / post statement codes, backup them before handling the sub-statements
            StatementState backupState = info.RetrieveStatementState();
            StringData data = new StringData("if (");
            data.AppendSameLine(condition);
            data.AppendSameLine(")\n");
            data.Append(GenerateCode(conditionStatement.Then, info));

            if (IsBlockStatementEmpty(conditionStatement.Else) == false)
            {
                data.Append("else\n");
                data.Append(GenerateCode(conditionStatement.Else, info));
            }
            // Restore the pre / post statement codes for the condition
            info.AddStatementState(backupState);
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
            StringData condition = LanguageManager.ExpressionGenerator.GenerateCode(doStatement.Condition, info);
            StatementState backupState = info.RetrieveStatementState();
            StringData body = GenerateCode(doStatement.Body, info);
            StringData data;
            if (condition.Text != "true")
            {
                data = new StringData("do\n");
                data.Append(body);
                data.Append("while (");
                data.AppendSameLine(condition);
                data.AppendSameLine(");\n");
            }
            else
            {
                data = new StringData("for ( ; ; )\n");
                data.Append(body);
            }
            info.AddStatementState(backupState);
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
            StringData data = new StringData("{\n");
            data.Indentation++;
            StringData variableDeclaration = LanguageManager.ReferenceGenerator.GenerateCodeVariableDeclaration(fixedStatement.Variable, info);
            data.Append(variableDeclaration);
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
                        // From string to char, help the conversion a bit
                        subExpression.AppendSameLine("->__ToCString__()");
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

            if (subExpression.LocalType.FullName.StartsWith("::System::Array__G"))
            {
                // If the source type is an array, that means we are converting to a pointer of the type (or maybe void *)
                // So convert the array in pointer of the type
                subExpression.AppendSameLine("->__ToPointer__()");
            }

            data.AppendSameLine(subExpression);
            data.AppendSameLine(";\n");

            // We should actually, make sure there is no GC in between...
            // TODO: Improve this by having the expression calucated first, then fixed
            // Currently this is not an issue as the GC would not interrupt the thread
            data.Append("::CrossNetRuntime::SetFixed(" + fixedStatement.Variable.Name + ", true);\n");
            data.Append(GenerateCode(fixedStatement.Body, info));
            data.Append("::CrossNetRuntime::SetFixed(" + fixedStatement.Variable.Name + ", false);\n");

            data.Indentation--;
            data.Append("}\n");
            info.UnsafeMethod = true; // Fixed implies unsafe code...
            return (data);
        }

        public StringData GenerateCodeForEach(IStatement statement, ParsingInfo info)
        {
            IForEachStatement forEachStatement = (IForEachStatement)statement;
/*
 * Need to specialize the code here...
 * 
            StringData data = new StringData("foreach (");
            data.AppendSameLine(LanguageManager.ReferenceGenerator.GenerateCodeVariableDeclaration(forEachStatement.Variable, info));
            data.AppendSameLine(" in ");
            StringData inData = LanguageManager.ExpressionGenerator.GenerateCode(forEachStatement.Expression, info);
            if (inData.Text == "base")
            {
                // Special case here... If "base" it has actually be incorrectly translated from "this" by reflector
                data.AppendSameLine("this");
            }
            else
            {
                data.AppendSameLine(inData);
            }
            data.AppendSameLine(")\n");
            data.Append(GenerateCode(forEachStatement.Body, info));
*/
            StringData data = new StringData("{\n");
            data.Indentation++;

            StringData inData = LanguageManager.ExpressionGenerator.GenerateCode(forEachStatement.Expression, info);
            ITypeInfo inInfo = inData.LocalType.GetTypeInfo();

            StringData variableDeclaration = LanguageManager.ReferenceGenerator.GenerateCodeVariableDeclaration(forEachStatement.Variable, info);

            // In case the expression had some pre-statements / post-statements...
            // We want them added before the first scoping "{"
            // This is the case if the target of the expression is a call to an interface
            // Don't need the post statements as they will be done by the standard statement loop
            StatementState backupState = info.RetrieveStatementState();

            bool enumerableInterface = false;
            bool withArray = false;
            ITypeInfo enumeratorInfo = null;

            // If we use an interface to call GetEnumerator() we will have to get a temp as well...
            // But first we have to look if the class implements the method publicly
            // If it does that mean that it takes priority over everything else...
            IMethodDeclaration getEnumeratorMethod = null;
            if (inInfo != null)
            {
                getEnumeratorMethod = Util.FindMethod(inInfo, "GetEnumerator", null, null, false);
                Debug.Assert(getEnumeratorMethod != null);

                if (getEnumeratorMethod.Visibility == MethodVisibility.Public)
                {
                    // Update inInfo with the corresponding type
                    // It will be usually the same type, but in some cases (like IList, etc...)
                    // the interface is actually going to be IEnumerable instead of IList
                    inInfo = TypeInfoManager.GetTypeInfo(getEnumeratorMethod.DeclaringType);

                    // We found the method, get the enumerator type of the return value and determine if that's an interface
                    enumeratorInfo = TypeInfoManager.GetTypeInfo(getEnumeratorMethod.ReturnType.Type as ITypeReference);

                    enumerableInterface = (inInfo.Type == ObjectType.INTERFACE);
                }
                else
                {
                    // The method is _not_ public, so we cannot use it for foreach
                    getEnumeratorMethod = null;
                }
            }
            else if (inData.LocalType.EmbeddedType is IArrayType)
            {
                // Specific case for array (as they don't have ITypeInfo...)

                IArrayType arrayType = (IArrayType)(inData.LocalType.EmbeddedType);
                // unfortunately Reflector doesn't give us the full name for the type, so we have to use our internal functions...
                ITypeInfo elementType = TypeInfoManager.GetTypeInfo(arrayType.ElementType);
                if (elementType != null)
                {
                    // It's an array and we have the element type
                    // Create a specific code for it, very optimized without any enumerator

                    string tempVariableArray = CppUtil.GetNextTempVariable();
                    data.Append(inData.LocalType + " " + tempVariableArray + " = ");
                    data.AppendSameLine(inData);
                    data.AppendSameLine(";\n");

                    string tempVariableLength = CppUtil.GetNextTempVariable();
                    data.Append("::System::Int32 " + tempVariableLength + " = ");
                    data.AppendSameLine(tempVariableArray + "->get_Length();\n");

                    string tempVariableIndex = CppUtil.GetNextTempVariable();
                    data.Append("for (");
                    data.AppendSameLine("::System::Int32 " + tempVariableIndex + " = 0 ; ");
                    data.AppendSameLine(tempVariableIndex + " < " + tempVariableLength + " ; ");
                    data.AppendSameLine("++" + tempVariableIndex + ")\n");

                    data.Append("{\n");
                    data.Indentation++;

                    data.Append(variableDeclaration);
                    data.AppendSameLine(" = " + tempVariableArray + "->Item(" + tempVariableIndex + ");\n");

                    data.Append(GenerateCode(forEachStatement.Body, info));

                    data.Indentation--;
                    data.Append("}\n");

                    data.Indentation--;
                    data.Append("}\n");

                    info.AddStatementState(backupState);
                    return (data);
                }
            }
            if ((getEnumeratorMethod == null) && (withArray == false))
            {
                // No method found, it means that's either an IEnumerable<> or an IEnumerable
                // The priority is with the IEnumerable<>
                // Note that we should not have the case where the class implements IEnumerable<int> and IEnumerable<double> at the same time
                // In C#, this won't compile.
                // But the user can cast to the proper interface in the foreach expression
                // In that case, if Reflector doesn't return the proper type, we may have incorrect behavior
                // 2 solutions if that happens:
                //      1. Put a warning during parsing, and ask the user to cast before the expression
                //      2. report the issue to Lutz to have the cast passed down correctly

                enumerableInterface = true;
                bool foundIt = false;

                if (inInfo != null)
                {
                    IList<ITypeInfo> allInterfaces = inInfo.UnionOfInterfaces;
                    foreach (ITypeInfo oneInterface in allInterfaces)
                    {
                        if (oneInterface.FullName.StartsWith("::System::Collections::Generic::IEnumerable__G1<"))
                        {
                            foundIt = true;
                            inInfo = oneInterface;

                            getEnumeratorMethod = Util.FindMethod(oneInterface, "GetEnumerator", null, null, false);
                            Debug.Assert(getEnumeratorMethod != null);

                            // We found the method, get the enumerator type of the return value and determine if that's an interface
                            enumeratorInfo = TypeInfoManager.GetTypeInfo(getEnumeratorMethod.ReturnType.Type as ITypeReference);
                            break;
                        }
                    }
                }

                if (foundIt == false)
                {
                    // Cannot have the type, assume that we have an IEnumerable then
                    inInfo = LanguageManager.LocalTypeManager.TypeIEnumerable.GetTypeInfo();
                    enumeratorInfo = LanguageManager.LocalTypeManager.TypeIEnumerator.GetTypeInfo();
                }
            }

            string returnType = enumeratorInfo.GetInstanceText(info);

            // Handle case where the foreach expression is "base"
            bool isBase = (forEachStatement.Expression is IBaseReferenceExpression);

            string tempVariable;    // Don't initialize it yet, so the order stays consistent

            if (enumerableInterface)
            {
                // If base, then it can't be an interface as we explictly ask the method...
                // As we are looking for the method first, we should not use the interface version
                Debug.Assert(isBase == false);
                string enumerableInstance;
                string enumerableType;
                LocalType localType;

                enumerableType = inInfo.GetFullName(info);
                enumerableInstance = enumerableType + inInfo.GetInstancePostFix();
                localType = inInfo.LocalType;

                string tempEnumerable = CppUtil.GetNextTempVariable();
                data.Append(enumerableInstance);
                data.AppendSameLine(" " + tempEnumerable + " = ");
                // We might need a cast between the target type and the temporary interface type
                inData = LanguageManager.LocalTypeManager.DoesNeedCast(localType, inData.LocalType, inData);
                data.AppendSameLine(inData);
                data.AppendSameLine(";\n");

                tempVariable = CppUtil.GetNextTempVariable();
                data.Append(returnType + " " + tempVariable + " = ");
                string call = CppUtil.InterfaceCall(tempEnumerable, enumerableType, "GetEnumerator");
                data.AppendSameLine(call);
                data.AppendSameLine(";\n");
            }
            else
            {
                tempVariable = CppUtil.GetNextTempVariable();
                data.Append(returnType + " " + tempVariable + " = ");
                data.AppendSameLine(inData);
                if (isBase)
                {
                    // For base, in C++ code, base is actually using explicitly the base type
                    data.AppendSameLine("::");
                }
                else
                {
                    if (inData.LocalType.GetTypeInfo().IsValueType)
                    {
                        data.AppendSameLine(".");
                    }
                    else
                    {
                        data.AppendSameLine("->");
                    }
                }
                data.AppendSameLine("GetEnumerator();\n");
            }

            string tempVariableMoveNext;
            string tempVariableGetCurrent;
            ITypeInfo enumeratorInfoMoveNext = null;
            ITypeInfo enumeratorInfoGetCurrent = null;

            enumeratorInfoMoveNext = Util.FindInterfaceDefiningMethod(enumeratorInfo, "MoveNext", null, LanguageManager.LocalTypeManager.TypeBool.EmbeddedType);
            if ((enumeratorInfoMoveNext != null) && (enumeratorInfo != enumeratorInfoMoveNext))
            {
                tempVariableMoveNext = CppUtil.GetNextTempVariable();

                data.Append(enumeratorInfoMoveNext.GetInstanceText(info) + " " + tempVariableMoveNext + " = ");
                // In reality we know that enumeratorInfoMoveNext is a parent interface of enumeratorInfo,
                // so we could directly do a fastinterfacecast instead of calling the generic function. But doing this is more maintainable...
                string tempCast = LanguageManager.LocalTypeManager.DoesNeedCast(enumeratorInfoMoveNext.LocalType, enumeratorInfo.LocalType, tempVariable);
                data.AppendSameLine(tempCast);
                data.AppendSameLine(";\n");
            }
            else
            {
                tempVariableMoveNext = tempVariable;
                enumeratorInfoMoveNext = enumeratorInfo;
            }
            enumeratorInfoGetCurrent = Util.FindInterfaceDefiningProperty(enumeratorInfo, "Current", null);
            if ((enumeratorInfoGetCurrent != null) && (enumeratorInfo != enumeratorInfoGetCurrent))
            {
                tempVariableGetCurrent = CppUtil.GetNextTempVariable();

                data.Append(enumeratorInfoGetCurrent.GetInstanceText(info) + " " + tempVariableGetCurrent + " = ");
                // In reality we know that enumeratorInfoGetCurrent is a parent interface of enumeratorInfo,
                // so we could directly do a fastinterfacecast instead of calling the generic function. But doing this is more maintainable...
                string tempCast = LanguageManager.LocalTypeManager.DoesNeedCast(enumeratorInfoGetCurrent.LocalType, enumeratorInfo.LocalType, tempVariable);
                data.AppendSameLine(tempCast);
                data.AppendSameLine(";\n");
            }
            else
            {
                tempVariableGetCurrent = tempVariable;
                enumeratorInfoGetCurrent = enumeratorInfo;
            }

            // TODO: To speed up the foreach a little bit more
            //  It seems that if enumeratorInfoMoveNext or enumeratorInfoGetCurrent are interfaces
            //  We could actually cache the interface map pointer, the interface wrapper or even better possibly the corresponding function pointer.
            //  That way, we would just do a static call instead of a slower interface call!

            // MoveNext() already using a temp variable, no need to combine statements...
            if (enumeratorInfoMoveNext.Type == ObjectType.INTERFACE)
            {
                string moveNextCall = CppUtil.InterfaceCall(tempVariableMoveNext, enumeratorInfoMoveNext.FullName, "MoveNext");
                data.Append("while (" + moveNextCall + ")\n");
            }
            else
            {
                data.Append("while (" + tempVariableMoveNext + enumeratorInfoMoveNext.PointerToMember + "MoveNext())\n");
            }
            data.Append("{\n");
            data.Indentation++;
            data.Append(variableDeclaration);

            IPropertyDeclaration property = Util.FindProperty(enumeratorInfo, "Current", null);
            Debug.Assert(property != null);

            LocalType getCurrentReturnValue = LanguageManager.LocalTypeManager.GetLocalType(property.PropertyType);
            string needCast = LanguageManager.LocalTypeManager.DoesNeedCast(variableDeclaration.LocalType, getCurrentReturnValue);

            // get_Current() already using a temp variable, no need to combine statements...
            string getCurrentCall;
            if (enumeratorInfoGetCurrent.Type == ObjectType.INTERFACE)
            {
                getCurrentCall = CppUtil.InterfaceCall(tempVariableGetCurrent, enumeratorInfoGetCurrent.FullName, "get_Current");
            }
            else
            {
                getCurrentCall = tempVariableGetCurrent + enumeratorInfoGetCurrent.PointerToMember + "get_Current()";
            }

            if (needCast != "")
            {
                data.AppendSameLine(" = " + needCast + "(" + getCurrentCall + ");\n");
            }
            else
            {
                data.AppendSameLine(" = " + getCurrentCall + ";\n");
            }

            data.Append(GenerateCode(forEachStatement.Body, info));
            data.Indentation--;
            data.Append("}\n");

            {
                // Dispose() already using a temp variable, no need to combine statements...
                ITypeInfo disposeInfo = LanguageManager.LocalTypeManager.TypeIDisposable.GetTypeInfo();
                if (disposeInfo != null)
                {
                    if ((inInfo != null) && Util.CanImplicitCast(inInfo, disposeInfo))
                    {
                        // TODO: This might not be correct if one of the base class is actually a disposable
                        // The sub class won't have the method. And BTW this should be an interface call...

                        //      data.Append(CppUtil.InterfaceCall(tempVariable, "System::IDisposable", "Dispose"));
                        // might be more correct...

                        data.Append(tempVariable + enumeratorInfo.PointerToMember + "Dispose();\n");
                    }
                }
                else
                {
                    // Here it means that System.IDisposable has not been parsed yet
                    // In reality this is not an issue as if the type was deriving from System::IDisposable
                    // the interface would have been parsed as well... So it also means it's not implementing the interface
                }
            }

            data.Indentation--;
            data.Append("}\n");

            info.AddStatementState(backupState);

            return (data);
        }

        public StringData GenerateCodeFor(IStatement statement, ParsingInfo info)
        {
            IForStatement forStatement = (IForStatement)statement;
            StringData data = new StringData("for (");
            string text;

            // Disable the combine before the loop conditionals
            info.CombineStatementEnabled = false;

            if (forStatement.Initializer != null)
            {
                // Backup pre and pro as they might have been added during initialization
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

            // Re-enable the combine after the loop conditionals
            // So pre-post statements are added outside the statement...
            info.CombineStatementEnabled = true;
            data = Util.CombineStatements(data, info);

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
            String variable = LanguageManager.ExpressionGenerator.GenerateCode(lockStatement.Expression, info).Text;
            StringData data = new StringData("{\n");
            data.Indentation++;
            data.Append("::System::Object::__Lock__(" + variable + ");\n");
            data.Append(GenerateCode(lockStatement.Body, info));
            data.Append("::System::Object::__Unlock__(" + variable + ");\n");
            data.Indentation--;
            data.Append("}\n");
            return (data);
        }

        public StringData GenerateCodeMemoryCopy(IStatement statement, ParsingInfo info)
        {
            IMemoryCopyStatement memoryCopyStatement = (IMemoryCopyStatement)statement;
            Debug.Fail("Not implemented!");

            StringData data = new StringData("[==20==]");
            return (data);
        }

        public StringData GenerateCodeMemoryInitialize(IStatement statement, ParsingInfo info)
        {
            IMemoryInitializeStatement memoryInitializeStatement = (IMemoryInitializeStatement)statement;
            Debug.Fail("Not implemented!");

            StringData data = new StringData("[==21==]");
            return (data);
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
                if (string.IsNullOrEmpty(info.ForcedReturnValue))
                {
                    data = new StringData("return;\n");
                }
                else
                {
                    data = new StringData("return (" + info.ForcedReturnValue + ");\n");
                }
            }
            return (data);
        }

        public StringData GenerateCodeRemoveEvent(IStatement statement, ParsingInfo info)
        {
#if false
            IRemoveEventStatement removeEventStatement = (IRemoveEventStatement)statement;
            StringData eventToModify = LanguageManager.ExpressionGenerator.GenerateCode(removeEventStatement.Event, info);
            StringData delegateToRemove = LanguageManager.ExpressionGenerator.GenerateCode(removeEventStatement.Listener, info);
            StringData data = new StringData(eventToModify.ToString());
            data.AppendSameLine(" = ");

            StringData value = new StringData("System::Delegate::Remove(");
            value.AppendSameLine(eventToModify);
            value.AppendSameLine(", ");
            value.AppendSameLine(delegateToRemove);
            value.AppendSameLine(")");

            value = LanguageManager.LocalTypeManager.DoesNeedCast(eventToModify.LocalType, LanguageManager.LocalTypeManager.TypeDelegate, value);
            data.AppendSameLine(value);
            data.AppendSameLine(";\n");
            return (data);
#else
            IRemoveEventStatement removeEventStatement = (IRemoveEventStatement)statement;
            StringData target = LanguageManager.ExpressionGenerator.GenerateCode(removeEventStatement.Event.Target, info);
            StringData delegateToRemove = LanguageManager.ExpressionGenerator.GenerateCode(removeEventStatement.Listener, info);

//            LocalType expectedDelegateType = LanguageManager.LocalTypeManager.GetLocalType(removeEventStatement.Event.Event.EventType);

            ITypeInfo targetTypeInfo = target.LocalType.GetTypeInfo();
            StringData data;
            if (targetTypeInfo.IsValueType)
            {
                data = target;
                data.AppendSameLine(".");
            }
            else if (targetTypeInfo.Type == ObjectType.INTERFACE)
            {
                string tempVariable = CppUtil.GetNextTempVariable();
                data = new StringData();
                data.Append(targetTypeInfo.FullName + targetTypeInfo.GetInstancePostFix() + tempVariable + " = " + target + ";\n");
                string call = CppUtil.InterfaceCall(tempVariable, targetTypeInfo.FullName, "remove_" + removeEventStatement.Event.Event.Name, delegateToRemove.Text);
                data.Append(call + ";\n");
                return (data);
            }
            else
            {
                data = target;
                data.AppendSameLine("->");
            }

            data.AppendSameLine("remove_" + removeEventStatement.Event.Event.Name + "(");
            data.AppendSameLine(delegateToRemove);
            data.AppendSameLine(")");
/*
            data = LanguageManager.LocalTypeManager.DoesNeedCast(expectedDelegateType, LanguageManager.LocalTypeManager.TypeDelegate, data);
            data.AppendSameLine(data);
 */
            data.AppendSameLine(";\n");
            return (data);
#endif
        }

        public StringData GenerateCodeSwitch(IStatement statement, ParsingInfo info)
        {
            ISwitchStatement switchStatement = (ISwitchStatement)statement;
            StringData data = new StringData("switch (");
            StringData switchValue = LanguageManager.ExpressionGenerator.GenerateCode(switchStatement.Expression, info);
            StatementState backupState = info.RetrieveStatementState();

            if (info.CurrentSwitch == null)
            {
                info.CurrentSwitch = new Stack<LocalType>();
            }

            // Push the value type on the stack
            info.CurrentSwitch.Push(switchValue.LocalType);

            data.AppendSameLine(switchValue);
            if (LanguageManager.LocalTypeManager.TypeString.Same(switchValue.LocalType))
            {
                // This is a special case here... Switch case on strings...
                // We are actually comparing hashcode ;)
                // There is potentially a risk of collision but we will improve that later...

                // Also we fully qualify the type (because we know it is a string)
                // The reason is two folds: We do a static call instead of a virtual call, so it is faster
                // But mainly by _not_ having a virtual call, the test is still valid even with null string.
                data.AppendSameLine("->::System::String::GetHashCode()");
            }
            data.AppendSameLine(")\n");
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
            info.AddStatementState(backupState);

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
            info.InCase = true;
            StringData caseData = LanguageManager.ExpressionGenerator.GenerateCode(expression, info);
            info.InCase = false;
            if (LanguageManager.LocalTypeManager.TypeString.Same(caseData.LocalType))
            {
                // In case of switch case with strings, we are currently using hashcode...
#if DISABLED    // String pool changed this behavior
                const string NEW_STRING_START = "::System::String::__Create__(L";
                const string NEW_STRING_END = ")";
                if (caseData.Text.StartsWith(NEW_STRING_START) && (caseData.Text.EndsWith(NEW_STRING_END)))
                {
                    string text = caseData.Text;
                    text = text.Substring(NEW_STRING_START.Length, text.Length - (NEW_STRING_START.Length + NEW_STRING_END.Length));
                    caseData.Replace(text);
                }
#else
                if (expression is ILiteralExpression)
                {
                    // For the case on strings, we still need the original string (esp. without esc. sequence...)
                    ILiteralExpression literal = (ILiteralExpression)expression;
                    caseData.Replace(literal.Value as String);
                }
#endif

                if (caseData.Text == "::System::String::Empty")
                {
                    caseData.Replace("\"\"");
                }
                Int32 hashCode = CppUtil.GetStringHashCode(caseData.Text);
                data.AppendSameLine(hashCode.ToString());
                data.AppendSameLine(":");
                // For debugging reason, put the string in comment... 
                data.AppendSameLine("\t\t// " + caseData.Text);
                data.AppendSameLine("\n");
            }
            else if (LanguageManager.LocalTypeManager.TypeNull.Same(caseData.LocalType))
            {
                // For null pointer - it should be used only for strings - maybe for nullable objects as well
                // We use the hash-code 0
                // By definition nothing should use the hashcode 0 (at least for strings, might have to adapt for nullable)
                // So if the string pointer is null, it "kind of" make sense to use 0
                // At least we differentiate it from the other strings.
                // The reason why 0 can't be used is because the hashcode is cached in the string code
                // (as they are immutable and should be pooled as well...)
                // And 0 indicates that the string hash code has not been calculated yet.
                // Enabling the usage of 0 would mean in those very rare strings that the cache would not work
                data.AppendSameLine("0:");
                data.AppendSameLine("\t\t// null");
                data.AppendSameLine("\n");
            }
            else
            {
                if (LanguageManager.LocalTypeManager.TypeInt32.Same(caseData.LocalType)
                    && LanguageManager.LocalTypeManager.TypeChar.Same(info.CurrentSwitch.Peek()))
                {
                    // The constant is an integer, but the variable is of type char
                    // This is following the reflector bug with literal char bigger than 0x7fff
                    // We have to cast it explicitly...

                    data.AppendSameLine("(System::Char)(");
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

            StringData finallyStatement = null;
            string finallyExceptionVarName = null;
            if (IsBlockStatementEmpty(tryStatement.Finally) == false)
            {
                finallyStatement = GenerateCode(tryStatement.Finally, info);
                finallyExceptionVarName = CppUtil.GetNextTempVariable();
            }

            foreach (ICatchClause catchClause in tryStatement.CatchClauses)
            {
                data.Append("catch");
                if (catchClause.Variable.VariableType.ToString().ToLower() == "object")
                {
                    // If object, it means catch anything...

                    // Actually it means that we catch RuntimeWrappedException...
//                    data.AppendSameLine(" (System.Runtime.CompilerServices.RuntimeWrappedException)");
                    data.AppendSameLine(" (...)");
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

                StringData catchClauseBody = GenerateCode(catchClause.Body, info);

                if (finallyStatement == null)
                {
                    data.Append(catchClauseBody);
                }
                else
                {
                    // There is finally statement, so we actually have to change the catch body
                    // We change Body to:

                    //  try
                    //  {
                    //      body
                    //  }
                    //  catch (Exception temp)
                    //  {
                    //      finally body
                    //      throw temp;
                    //  }

                    data.Append("{\n");
                    data.Indentation++;
                    data.Append("try\n");
                    data.Append(catchClauseBody);
                    data.Append("catch (void * " + finallyExceptionVarName + ")\n");
                    data.Append("{\n");
                    data.Indentation++;
                    data.Append(finallyStatement);
                    data.Append("throw " + finallyExceptionVarName + ";\n");
                    data.Indentation--;
                    data.Append("}\n");
                    data.Indentation--;
                    data.Append("}\n");
                }
            }
            if (tryStatement.CatchClauses.Count == 0)
            {
                // No catch clause! It's impossible in C++
                data.Append("catch (...)\n");
                data.Append("{\n    //Empty catch\n}\n");
            }

            if (finallyStatement != null)
            {
                data.Append("// Emulation of finally\n");
                data.Append(finallyStatement);
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

            // In an using statement, if an exception occurs, we will still call dispose!
            // So we have to catch the exception, call the dispose, then throw the exception again...
            StringData data = new StringData("{\n");
            ++data.Indentation;
            StringData varDeclaration = LanguageManager.ExpressionGenerator.GenerateCode(usingStatement.Variable, info);
            string varName = varDeclaration.Text;
            ITypeInfo varInfo = TypeInfoManager.GetTypeInfo(varDeclaration.LocalType);
            data.Append(varName);
            data.AppendSameLine(" = ");
            data.AppendSameLine(LanguageManager.ExpressionGenerator.GenerateCode(usingStatement.Expression, info));
            data.AppendSameLine(";\n");

            StatementState backupState = info.RetrieveStatementState();

            data.Append("try\n");
            data.Append(GenerateCode(usingStatement.Body, info));

            string disposeText;
            int indexVar = varName.LastIndexOf(' ');
            if (indexVar > 0)
            {
                varName = varName.Substring(indexVar + 1);
            }

            if (varInfo == null)
            {
                // We might not want to call Dispose() in this case...
                disposeText = varName + "->Dispose();\n";
            }
            else
            {
                LocalType disposeType = LanguageManager.LocalTypeManager.TypeIDisposable;
                bool interfaceCall = disposeType.Same(varDeclaration.LocalType);
                foreach (ITypeInfo oneInterface in varInfo.UnionOfInterfaces)
                {
                    if (disposeType.Same(oneInterface.LocalType))
                    {
                        interfaceCall = true;
                    }
                }

                if (interfaceCall)
                {
                    // Interface call
                    disposeText = CppUtil.InterfaceCall(varName, disposeType.FullName, "Dispose");
                }
                else
                {
                    disposeText = varName + varInfo.PointerToMember + "Dispose()";
                }
                disposeText += ";\n";
            }

            // For the dispose text, add the test to make sure that the pointer was not NULL...
            disposeText = "if (" + varName + " != NULL)\n{\n\t" + disposeText + "}\n";

            // If there was an exception, call dispose then throw the exception again...
            data.Append("catch (::System::Exception * __temp_e__)\n{\n");
            data.Append(1, disposeText);
            data.Append(1, "throw __temp_e__;\n");
            data.Append("}\n");

            // If there was no exception, call the dispose in standard manner.
            data.Append(disposeText);

            info.AddStatementState(backupState);

            --data.Indentation;
            data.Append("}\n");

            return (data);
        }

        public StringData GenerateCodeWhile(IStatement statement, ParsingInfo info)
        {
            IWhileStatement whileStatement = (IWhileStatement)statement;
            StringData condition = LanguageManager.ExpressionGenerator.GenerateCode(whileStatement.Condition, info);
            StatementState backupState = info.RetrieveStatementState();
            StringData body = GenerateCode(whileStatement.Body, info);
            StringData data;
            if (condition.Text != "true")
            {
                data = new StringData("while (");
                data.AppendSameLine(condition);
                data.AppendSameLine(")\n");
                data.Append(body);
            }
            else
            {
                data = new StringData("for ( ; ; )\n");
                data.Append(body);
            }
            info.AddStatementState(backupState);
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

        private IDictionary<Type, GenerateStatementDelegate> sGenerateDelegates = new Dictionary<Type, GenerateStatementDelegate>();
        private IDictionary<Type, ParseStatementDelegate> sParseDelegates = new Dictionary<Type, ParseStatementDelegate>();
    }
}

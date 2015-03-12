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
    public class CSharpExpressionGenerator : IExpressionGenerator
    {
        public StringData GenerateCode(IExpression expression, ParsingInfo info)
        {
            if (expression == null)
            {
                return (new StringData());
            }

            // Try to use the cache first
            // Get the type of the expression (i.e. the implementation)
            Type expressionType = expression.GetType();
            // See if there is a corresponding delegate for this type...
            GenerateExpressionDelegate del;
            bool foundIt = sGenerateDelegates.TryGetValue(expressionType, out del);
            if (foundIt)
            {
                // Yes, invoke it
                return (del(expression, info));
            }

            // There was no corresponding delegate, do the slow comparison
            // At the same time, it will populate the hashtable for the next time...

            del = null;

            if (expression is IAddressDereferenceExpression)
            {
                del = GenerateCodeAddressDereference;
            }
            else if (expression is IAddressOfExpression)
            {
                del = GenerateCodeAddressOf;
            }
            else if (expression is IAddressOutExpression)
            {
                del = GenerateCodeAddressOut;
            }
            else if (expression is IAddressReferenceExpression)
            {
                del = GenerateCodeAddressReference;
            }
            else if (expression is IAnonymousMethodExpression)
            {
                del = GenerateCodeAnonymousMethod;
            }
            else if (expression is IArgumentListExpression)
            {
                del = GenerateCodeArgumentList;
            }
            else if (expression is IArgumentReferenceExpression)
            {
                del = GenerateCodeArgumentReference;
            }
            else if (expression is IArrayCreateExpression)
            {
                del = GenerateCodeArrayCreate;
            }
            else if (expression is IArrayIndexerExpression)
            {
                del = GenerateCodeArrayIndexer;
            }
            else if (expression is IAssignExpression)
            {
                del = GenerateCodeAssign;
            }
            else if (expression is IBaseReferenceExpression)
            {
                del = GenerateCodeBaseReference;
            }
            else if (expression is IBinaryExpression)
            {
                del = GenerateCodeBinary;
            }
            else if (expression is ICanCastExpression)
            {
                del = GenerateCodeCanCast;
            }
            else if (expression is ICastExpression)
            {
                del = GenerateCodeCast;
            }
            else if (expression is IConditionExpression)
            {
                del = GenerateCodeCondition;
            }
            else if (expression is IDelegateCreateExpression)
            {
                del = GenerateCodeDelegateCreate;
            }
            else if (expression is IDelegateInvokeExpression)
            {
                del = GenerateCodeDelegateInvoke;
            }
            else if (expression is IEventReferenceExpression)
            {
                del = GenerateCodeEventReference;
            }
            else if (expression is IFieldOfExpression)
            {
                del = GenerateCodeFieldOf;
            }
            else if (expression is IFieldReferenceExpression)
            {
                del = GenerateCodeFieldReference;
            }
            else if (expression is IGenericDefaultExpression)
            {
                del = GenerateCodeGenericDefault;
            }
            else if (expression is ILiteralExpression)
            {
                del = GenerateCodeLiteral;
            }
            else if (expression is IMemberInitializerExpression)
            {
                del = GenerateCodeMemberInitializer;
            }
            else if (expression is IMethodInvokeExpression)
            {
                del = GenerateCodeMethodInvoke;
            }
            else if (expression is IMethodOfExpression)
            {
                del = GenerateCodeMethodOf;
            }
            else if (expression is IMethodReferenceExpression)
            {
                del = GenerateCodeMethodReference;
            }
            else if (expression is INullCoalescingExpression)
            {
                del = GenerateCodeNullCoalescing;
            }
            else if (expression is IObjectCreateExpression)
            {
                del = GenerateCodeObjectCreate;
            }
            else if (expression is IPropertyIndexerExpression)
            {
                del = GenerateCodePropertyIndexer;
            }
            else if (expression is IPropertyReferenceExpression)
            {
                del = GenerateCodePropertyReference;
            }
            else if (expression is ISizeOfExpression)
            {
                del = GenerateCodeSizeOf;
            }
            else if (expression is ISnippetExpression)
            {
                del = GenerateCodeSnippet;
            }
            else if (expression is IStackAllocateExpression)
            {
                del = GenerateCodeStackAllocate;
            }
            else if (expression is IThisReferenceExpression)
            {
                del = GenerateCodeThisReference;
            }
            else if (expression is ITryCastExpression)
            {
                del = GenerateCodeTryCast;
            }
            else if (expression is ITypedReferenceCreateExpression)
            {
                del = GenerateCodeTypedReferenceCreate;
            }
            else if (expression is ITypeOfExpression)
            {
                del = GenerateCodeTypeOf;
            }
            else if (expression is ITypeOfTypedReferenceExpression)
            {
                del = GenerateCodeTypeOfTypedReference;
            }
            else if (expression is ITypeReferenceExpression)
            {
                del = GenerateCodeTypeReference;
            }
            else if (expression is IUnaryExpression)
            {
                del = GenerateCodeUnary;
            }
            else if (expression is IValueOfTypedReferenceExpression)
            {
                del = GenerateCodeValueOfTypedReference;
            }
            else if (expression is IVariableDeclarationExpression)
            {
                del = GenerateCodeVariableDeclaration;
            }
            else if (expression is IVariableReferenceExpression)
            {
                del = GenerateCodeVariableReference;
            }

            Debug.Assert(del != null, "The delegate should be set!");
            // Add the delegate on the hashtable to optimize the next time
            sGenerateDelegates[expressionType] = del;
            return (del(expression, info));
        }

        public StringData GenerateCodeAddressDereference(IExpression passedExpression, ParsingInfo info)
        {
            IAddressDereferenceExpression expression = (IAddressDereferenceExpression)passedExpression;
            StringData data = new StringData("*(");
            StringData expr = GenerateCode(expression.Expression, info);
            data.AppendSameLine(expr);
            data.AppendSameLine(")");
            info.UnsafeMethod = true; // Address dereference implies unsafe code
            data.LocalType = expr.LocalType;      // TODO: Put the exact type...
            return (data);
        }

        public StringData GenerateCodeAddressOf(IExpression passedExpression, ParsingInfo info)
        {
            IAddressOfExpression expression = (IAddressOfExpression)passedExpression;
            StringData data = new StringData("&");
            StringData expr = GenerateCode(expression.Expression, info);
            data.AppendSameLine(expr);
            info.UnsafeMethod = true; // AddressOf implies unsafe code
            data.LocalType = LanguageManager.LocalTypeManager.TypePointer;            // TODO: Put the exact type...
            return (data);
        }

        public StringData GenerateCodeAddressOut(IExpression passedExpression, ParsingInfo info)
        {
            IAddressOutExpression expression = (IAddressOutExpression)passedExpression;
            StringData data = new StringData("out ");
            info.InRefOrOut = true;
            StringData value = GenerateCode(expression.Expression, info);
            data.AppendSameLine(value);
            data.LocalType = value.LocalType;
            info.InRefOrOut = false;        // Reset the state when we are done...
            return (data);
        }

        public StringData GenerateCodeAddressReference(IExpression passedExpression, ParsingInfo info)
        {
            IAddressReferenceExpression expression = (IAddressReferenceExpression)passedExpression;
            StringData data = new StringData("ref ");
            info.InRefOrOut = true;
            StringData value = GenerateCode(expression.Expression, info);
            data.AppendSameLine(value);
            data.LocalType = value.LocalType;
            info.InRefOrOut = false;        // Reset the state when we are done...
            return (data);
        }

        public StringData GenerateCodeAnonymousMethod(IExpression passedExpression, ParsingInfo info)
        {
            IAnonymousMethodExpression expression = (IAnonymousMethodExpression)passedExpression;
            StringData data = new StringData();
            //#error TO IMPLEMENT!
            Debug.Fail("Not implemented!");
            data.LocalType = LanguageManager.LocalTypeManager.TypeUnknown;
            return (data);
        }

        public StringData GenerateCodeArgumentList(IExpression passedExpression, ParsingInfo info)
        {
            Debug.Fail("Not implemented!");
            return (null);
        }

        public StringData GenerateCodeArgumentReference(IExpression passedExpression, ParsingInfo info)
        {
            IArgumentReferenceExpression expression = (IArgumentReferenceExpression)passedExpression;
            StringData data = LanguageManager.ReferenceGenerator.GenerateCodeParameterReference(expression.Parameter);
            return (data);
        }

        public StringData GenerateCodeArrayCreate(IExpression passedExpression, ParsingInfo info)
        {
            IArrayCreateExpression expression = (IArrayCreateExpression)passedExpression;
            StringData data = new StringData("new ");

            // We have to handle a specific case here
            // It seems somewhat a hack though...

            IArrayType arrayType = expression.Type as IArrayType;
            if (arrayType != null)
            {
                data.AppendSameLine(LanguageManager.ReferenceGenerator.GenerateCodeType(arrayType.ElementType, info));

                foreach (IExpression oneDimension in expression.Dimensions)
                {
                    data.AppendSameLine("[");
                    data.AppendSameLine(GenerateCode(oneDimension, info));
                    data.AppendSameLine("]");
                }

                if (arrayType.Dimensions.Count != 0)
                {
                    foreach (IArrayDimension dimension in arrayType.Dimensions)
                    {
                        data.AppendSameLine("[" + dimension.UpperBound + "]");
                    }
                }
                else
                {
                    data.AppendSameLine("[]");
                }
            }
            else
            {
                data.AppendSameLine(LanguageManager.ReferenceGenerator.GenerateCodeType(expression.Type, info));

                data.AppendSameLine("[");
                bool first = true;
                foreach (IExpression oneDimension in expression.Dimensions)
                {
                    if (first == false)
                    {
                        data.AppendSameLine(", ");
                    }
                    first = false;
                    data.AppendSameLine(GenerateCode(oneDimension, info));
                }
                data.AppendSameLine("]");
            }

            if (expression.Initializer != null)
            {
                data.AppendSameLine(" ");
                data.AppendSameLine(GenerateCodeArrayInitializer(expression.Initializer, info));
            }
            data.LocalType = LanguageManager.LocalTypeManager.TypeArray;
            return (data);
        }

        public StringData GenerateCodeArrayIndexer(IExpression passedExpression, ParsingInfo info)
        {
            IArrayIndexerExpression expression = (IArrayIndexerExpression)passedExpression;
            StringData data = GenerateCode(expression.Target, info);
            foreach (IExpression eachIndice in expression.Indices)
            {
                data.AppendSameLine("[");
                data.AppendSameLine(GenerateCode(eachIndice, info));
                data.AppendSameLine("]");
            }

            // The return type is different, as we have to skip as many necessary [] (or *) in the type...
            // First skip the LocalType
            IType returnedType = data.EmbeddedType;     // On purpose use the embedded type
            for (int numToSkip = expression.Indices.Count ; numToSkip > 0 ; --numToSkip)
            {
                // There is more chance that we are manipulating array than pointer, so do array first...
                IArrayType arraytype = returnedType as IArrayType;
                if (arraytype != null)
                {
                    returnedType = arraytype.ElementType;
                    continue;
                }

                IPointerType pointerType = returnedType as IPointerType;
                if (pointerType != null)
                {
                    returnedType = pointerType.ElementType;
                    continue;
                }
            }

            data.EmbeddedType = returnedType;       // Store the embedded type, will extract the LocalType
            return (data);
        }

        public StringData GenerateCodeArrayInitializer(IBlockExpression expression, ParsingInfo info)
        {
            StringData data = new StringData("{");
            ++data.Indentation;
            foreach (IExpression oneExpression in expression.Expressions)
            {
                data.Append(" ");
                if (oneExpression is IBlockExpression)
                {
                    data.AppendSameLine(GenerateCodeArrayInitializer((IBlockExpression)oneExpression, info));
                }
                else
                {
                    data.AppendSameLine(GenerateCode(oneExpression, info));
                }
                data.AppendSameLine(",\n");
            }
            --data.Indentation;
            data.Append("}");
            data.LocalType = LanguageManager.LocalTypeManager.TypeArray;
            return (data);
        }

        public StringData GenerateCodeAssign(IExpression passedExpression, ParsingInfo info)
        {
            IAssignExpression expression = (IAssignExpression)passedExpression;
            StringData data = GenerateCode(expression.Target, info);
            data.AppendSameLine(" = ");
            StringData valueData = GenerateCode(expression.Expression, info);

            // Now we have to check some incorrect cast...
            // And fix them when possible...

            LocalType variableType = data.LocalType;
            LocalType valueType = valueData.LocalType;

            Debug.Assert(variableType != null);
            Debug.Assert(valueType != null);

            if (variableType.Same(valueType) == false)
            {
                // The types are not the same! Do we have to fix?

                if (LanguageManager.LocalTypeManager.TypeInt32.Same(valueType))
                {
                    // Implicit cast from int, to...

                    if (LanguageManager.LocalTypeManager.TypeChar.Same(variableType))
                    {
                        // Force the cast from int to char
                        data.AppendSameLine("(System.Char)(");
                        data.AppendSameLine(valueData);
                        data.AppendSameLine(")");
                        data.LocalType = LanguageManager.LocalTypeManager.TypeChar;
                        return (data);
                    }
                    if (LanguageManager.LocalTypeManager.TypeUInt16.Same(variableType))
                    {
                        // Force the cast from int to ushort
                        data.AppendSameLine("(System.UInt16)(");
                        data.AppendSameLine(valueData);
                        data.AppendSameLine(")");
                        data.LocalType = LanguageManager.LocalTypeManager.TypeUInt16;
                        return (data);
                    }
                    if (LanguageManager.LocalTypeManager.TypeInt16.Same(variableType))
                    {
                        // Force the cast from int to ushort
                        data.AppendSameLine("(System.Int16)(");
                        data.AppendSameLine(valueData);
                        data.AppendSameLine(")");
                        data.LocalType = LanguageManager.LocalTypeManager.TypeInt16;
                        return (data);
                    }
                }
            }

            data.AppendSameLine(valueData);
            return (data);
        }

        public StringData GenerateCodeBaseReference(IExpression passedExpression, ParsingInfo info)
        {
            StringData data = new StringData("base");
            data.EmbeddedType = info.BaseDeclaringType;     // On purpose, use the embedded type
            return (data);
        }

        public StringData GenerateCodeBinary(IExpression passedExpression, ParsingInfo info)
        {
            // This list MUST match BinaryOperator order
            // For performance reason, we might want to create this only one time...
            string[] operators = new string[]
            {
                "+",
                "-",
                "*",
                "/",
                "%",
                "<<",
                ">>",
                "==",
                "!=",
                "==",
                "!=",
                "|",
                "&",
                "^",
                "||",
                "&&",
                "<",
                "<=",
                ">",
                ">="
            };

            StringData data = new StringData();
            IBinaryExpression expression = (IBinaryExpression)passedExpression;
            IExpression leftExpression = expression.Left;
            IExpression rightExpression = expression.Right;

            StringData leftData = GenerateCode(leftExpression, info);
            StringData rightData = GenerateCode(rightExpression, info);

            LocalType leftType = leftData.LocalType;
            LocalType rightType = rightData.LocalType;

            LocalType operationType;
            if (LanguageManager.LocalTypeManager.TypeChar.Same(leftType) || LanguageManager.LocalTypeManager.TypeChar.Same(rightType))
            {
                // Binary operations on char are actually returning an int!
                operationType = LanguageManager.LocalTypeManager.TypeInt32;
            }
            else
            {
                operationType = leftType;
            }

            LocalType[] booleanResult = new LocalType[]
            {
                operationType,  //"+",
                operationType,  //"-",
                operationType,  //"*",
                operationType,  //"/",
                operationType,  //"%",
                operationType,  //"<<",
                operationType,  //">>",
                LanguageManager.LocalTypeManager.TypeBool,       //"==",
                LanguageManager.LocalTypeManager.TypeBool,       //"!=",
                LanguageManager.LocalTypeManager.TypeBool,       //"==",
                LanguageManager.LocalTypeManager.TypeBool,       //"!=",
                operationType,  //"|",
                operationType,  //"&",
                operationType,  //"^",
                LanguageManager.LocalTypeManager.TypeBool,       //"||",
                LanguageManager.LocalTypeManager.TypeBool,       //"&&",
                LanguageManager.LocalTypeManager.TypeBool,       //"<",
                LanguageManager.LocalTypeManager.TypeBool,       //"<=",
                LanguageManager.LocalTypeManager.TypeBool,       //">",
                LanguageManager.LocalTypeManager.TypeBool,       //">="
            };

            if (NeedEncapsulation(leftExpression))
            {
                data.AppendSameLine("((");
                data.AppendSameLine(leftData);
                data.AppendSameLine(") ");
            }
            else
            {
                data.AppendSameLine("(");
                data.AppendSameLine(leftData);
                data.AppendSameLine(" ");
            }

            data.AppendSameLine(operators[(int)expression.Operator]);

            if (NeedEncapsulation(rightExpression))
            {
                data.AppendSameLine(" (");
                data.AppendSameLine(rightData);
                data.AppendSameLine("))");
            }
            else
            {
                data.AppendSameLine(" ");
                data.AppendSameLine(rightData);
                data.AppendSameLine(")");
            }

            data.LocalType = booleanResult[(int)expression.Operator];
            return (data);
        }

        private bool NeedEncapsulation(IExpression expression)
        {
            if (expression is IBinaryExpression)
            {
                return (true);
            }
            if (expression is IUnaryExpression)
            {
                return (true);
            }
            if (expression is ICanCastExpression)
            {
                return (true);
            }
            if (expression is IAssignExpression)
            {
                return (true);
            }
            if (expression is IConditionExpression)
            {
                return (true);
            }
            return (false);
        }

        public StringData GenerateCodeCanCast(IExpression passedExpression, ParsingInfo info)
        {
            ICanCastExpression expression = (ICanCastExpression)passedExpression;
            StringData data = GenerateCode(expression.Expression, info);
            data.Append(" is ");
            data.Append(LanguageManager.ReferenceGenerator.GenerateCodeType(expression.TargetType, info));
            data.LocalType = LanguageManager.LocalTypeManager.TypeBool;
            return (data);
        }

        public StringData GenerateCodeCast(IExpression passedExpression, ParsingInfo info)
        {
            ICastExpression expression = (ICastExpression)passedExpression;
            StringData data;
            StringData typeToCast = LanguageManager.ReferenceGenerator.GenerateCodeType(expression.TargetType, info);
            StringData subExpression;
            if (info.InRefOrOut)
            {
                // Specific case under a ref or out...
                // This code:
                //  unsafe private static void WaitAndEnterCriticalSection(System.Int32 * spinLockPointer)
                //  {
                //      System.Int32 num1 = System.Threading.Interlocked.CompareExchange(ref *spinLockPointer, 1, 0);
                //  }
                // generates:
                //      System.Int32 num1 = System.Threading.Interlocked.CompareExchange(ref ((System.Int32)(ref spinLockPointer)), 1, 0);

                // The goal is to replace:
                //      ((System.Int32)(ref spinLockPointer))
                // By:
                //      *(System.Int32*)(spinLockPointer)

                IExpression addressExpression = null;
                IAddressReferenceExpression addressRef = expression.Expression as IAddressReferenceExpression;
                if (addressRef != null)
                {
                    addressExpression = addressRef.Expression;
                }
                IAddressOutExpression addressOut = expression.Expression as IAddressOutExpression;
                if (addressOut != null)
                {
                    addressExpression = addressOut.Expression;
                }

                if (addressExpression != null)
                {
                    // We detected the sequence "ref cast ref" or "out cast out", we can patch...

                    data = new StringData("*(");
                    data.AppendSameLine(typeToCast);
                    data.AppendSameLine("*)(");
                    subExpression = GenerateCode(addressExpression, info);
                    data.AppendSameLine(subExpression);
                    data.AppendSameLine(")");
                    data.LocalType = typeToCast.LocalType;
                    info.UnsafeMethod = true;
                    return (data);
                }
            }

            // General case... (Actually with the stackalloc case as well ;)
            data = new StringData("((");
            data.AppendSameLine(typeToCast);
            data.AppendSameLine(")");

            // In the current IL generation, it is impossible to have a correct generated code
            // as the type is casted from a System.Byte[]
            // As such we push the information down and try to have the state returned correctly
            if (info.CurrentCast == null)
            {
                info.CurrentCast = new Stack<IType>();
            }
            info.CurrentCast.Push(expression.TargetType);
            subExpression = GenerateCode(expression.Expression, info);
            info.CurrentCast.Pop();

            if (info.CurrentCast.Count == 0)
            {
                // Highest level of cast, we can look here at the StackAlloc
                if (info.InStackAlloc)
                {
                    // It was a cast for the stack alloc, don't cast anything
                    // Return directly the sub-expression
                    info.InStackAlloc = false;  // Clear for next time...
                    return (subExpression);
                }
                // If we are here, it means that it was a standard cast
            }
            else
            {
                // Not yet in the highest level of case, so don't handle the InStackAlloc yet...
                // For example: (T *)stackalloc T[(int)boo()];
            }

            // During cast, force the parenthesis, so it also fixes cases like (Int64)-1
            //            if (NeedEncapsulation(expression.Expression))
            {
                data.AppendSameLine("(");
                data.AppendSameLine(subExpression);
                data.AppendSameLine("))");
            }
            /*
                        else
                        {
                            data.AppendSameLine(subExpression);
                            data.AppendSameLine(")");
                        }
            */
            data.LocalType = typeToCast.LocalType;

            return (data);
        }

        public StringData GenerateCodeCondition(IExpression passedExpression, ParsingInfo info)
        {
            IConditionExpression expression = (IConditionExpression)passedExpression;
            StringData data = GenerateCode(expression.Condition, info);
            data.AppendSameLine(" ? ");
            StringData thenData = GenerateCode(expression.Then, info);
            StringData elseData = GenerateCode(expression.Else, info);

            if (thenData.LocalType.Same(elseData.LocalType))
            {
                // Exact same type, no issue
                data.AppendSameLine(thenData);
                data.AppendSameLine(" : ");
                data.AppendSameLine(elseData);
                data.LocalType = thenData.LocalType;
            }
            else
            {
                // Not the same type, we might be in a case of implicit conversion
                // We have to generate the cast here to the lowest size...

                // This loop might be a bit slow, but we don't expect it to happen too often though...

                LocalType[] types = new LocalType[] {   LanguageManager.LocalTypeManager.TypeBool, LanguageManager.LocalTypeManager.TypeByte, LanguageManager.LocalTypeManager.TypeSByte, LanguageManager.LocalTypeManager.TypeInt16, LanguageManager.LocalTypeManager.TypeUInt16,
                                                        LanguageManager.LocalTypeManager.TypeInt32, LanguageManager.LocalTypeManager.TypeUInt32, LanguageManager.LocalTypeManager.TypeInt64, LanguageManager.LocalTypeManager.TypeUInt64 };
                int indexThen = 0;
                foreach (LocalType thenType in types)
                {
                    if (thenType.Same(thenData.LocalType))
                    {
                        break;
                    }
                    ++indexThen;
                }

                int indexElse = 0;
                foreach (LocalType elseType in types)
                {
                    if (elseType.Same(elseData.LocalType))
                    {
                        break;
                    }
                    ++indexElse;
                }

                if (indexThen < indexElse)
                {
                    // "then" type being the one to keep
                    data.AppendSameLine(thenData);
                    data.AppendSameLine(" : (");
                    data.AppendSameLine(types[indexThen].ToString());
                    data.AppendSameLine(")");
                    data.AppendSameLine(elseData);
                    data.LocalType = thenData.LocalType;
                }
                else if (indexThen > indexElse)
                {
                    // "else" type being the one to keep
                    data.AppendSameLine("(");
                    data.AppendSameLine(types[indexElse].ToString());
                    data.AppendSameLine(")");
                    data.AppendSameLine(thenData);
                    data.AppendSameLine(" : ");
                    data.AppendSameLine(elseData);
                    data.LocalType = elseData.LocalType;
                }
                else
                {
                    // Type different, but can't determine exactly what to do here...
                    // Try to do the standard way...
                    data.AppendSameLine(thenData);
                    data.AppendSameLine(" : ");
                    data.AppendSameLine(elseData);
                    data.LocalType = thenData.LocalType;    // Take "then" type...
                }
            }
            return (data);
        }

        public StringData GenerateCodeDelegateCreate(IExpression passedExpression, ParsingInfo info)
        {
            IDelegateCreateExpression expression = (IDelegateCreateExpression)passedExpression;
            StringData data = new StringData("new ");
            StringData delegateType = LanguageManager.ReferenceGenerator.GenerateCodeTypeReference(expression.DelegateType, info);
            data.AppendSameLine(delegateType);
            data.AppendSameLine("(");
            data.AppendSameLine(GenerateCode(expression.Target, info));
            data.AppendSameLine(".");
            data.AppendSameLine(LanguageManager.ReferenceGenerator.GenerateCodeMethodReference(expression.Method, info));
            data.AppendSameLine(")");
            data.LocalType = LanguageManager.LocalTypeManager.TypeDelegate;
            return (data);
        }

        public StringData GenerateCodeDelegateInvoke(IExpression passedExpression, ParsingInfo info)
        {
            Debug.Fail("Not implemented!");
            return (null);
        }

        public StringData GenerateCodeEventReference(IExpression passedExpression, ParsingInfo info)
        {
            IEventReferenceExpression expression = (IEventReferenceExpression)passedExpression;
            StringData data = GenerateCode(expression.Target, info);
            data.AppendSameLine(".");
            data.AppendSameLine(LanguageManager.ReferenceGenerator.GenerateCodeEventReference(expression.Event));
            return (data);
        }

        public StringData GenerateCodeFieldOf(IExpression passedExpression, ParsingInfo info)
        {
            Debug.Fail("Not implemented!");
            return (null);
        }

        public StringData GenerateCodeFieldReference(IExpression passedExpression, ParsingInfo info)
        {
            IFieldReferenceExpression expression = (IFieldReferenceExpression)passedExpression;
            StringData data = GenerateCode(expression.Target, info);
            if (data.EmbeddedType is IPointerType)  // On purpose, use the embedded type
            {
                data.AppendSameLine("->");
            }
            else
            {
                data.AppendSameLine(".");
            }
            StringData fieldData = LanguageManager.ReferenceGenerator.GenerateCodeFieldReference(expression.Field, info);
            data.AppendSameLine(fieldData);
            data.LocalType = fieldData.LocalType;
            return (data);
        }

        public StringData GenerateCodeGenericDefault(IExpression passedExpression, ParsingInfo info)
        {
            Debug.Fail("Not implemented!");
            return (null);
        }

        // Code duplicated with NormalizeStringExpression for performance reason...
        private static string NormalizeCharExpression(char text)
        {
            ushort value = (ushort)text;
            if (value >= 0x0080)
            {
                string str = "'\\u";
                str += value.ToString("x4");
                str += "'";
                return (str);
            }
            else
            {
                switch (text)
                {
                    case '\r':
                        return ("'\\r'");

                    case '\t':
                        return ("'\\t'");

                    case '\'':
                        return ("'\\''");

                    case '\0':
                        return ("'\\0'");

                    case '\n':
                        return ("'\\n'");

                    case '\\':
                        return ("'\\\\'");

                    default:
                        return ("'" + text.ToString() + "'");
                }
            }
        }

        private static string NormalizeStringExpression(string text)
        {
            StringBuilder sb = new StringBuilder("\"", text.Length);
            foreach (char c in text)
            {
                ushort value = (ushort)c;
                if (value >= 0x0080)
                {
                    sb.Append("\\u");
                    sb.Append(value.ToString("x4"));
                }
                else
                {
                    switch (c)
                    {
                        case '\r':
                            sb.Append("\\r");
                            break;
                        case '\t':
                            sb.Append("\\t");
                            break;
                        case '"':
                            sb.Append("\\\"");
                            break;
                        case '\0':
                            sb.Append("\\0");
                            break;
                        case '\n':
                            sb.Append("\\n");
                            break;
                        case '\\':
                            sb.Append("\\\\");
                            break;

                        default:
                            sb.Append(c);
                            break;
                    }
                }
            }
            sb.Append("\"");
            return (sb.ToString());
        }

        public StringData GenerateCodeLiteral(IExpression passedExpression, ParsingInfo info)
        {
            ILiteralExpression expression = (ILiteralExpression)passedExpression;
            object value = expression.Value;
            string text;
            LocalType type;
            if (value == null)
            {
                // Initialized with null
                text = "null";
                type = LanguageManager.LocalTypeManager.TypeNull;
            }
            else
            {
                // For optimization reason, should we use an hashtable here?
                Type valueType = value.GetType();
                if (valueType == typeof(String))
                {
                    // We need to add the quote for the strings
                    text = NormalizeStringExpression((String)value);
                    type = LanguageManager.LocalTypeManager.TypeString;
                }
                else if (valueType == typeof(char))
                {
                    text = NormalizeCharExpression((Char)value);
                    type = LanguageManager.LocalTypeManager.TypeChar;
                }
                else if (valueType == typeof(bool))
                {
                    bool boolValue = (bool)value;
                    if (boolValue)
                    {
                        text = "true";
                    }
                    else
                    {
                        text = "false";
                    }
                    type = LanguageManager.LocalTypeManager.TypeBool;
                }
                else if (valueType == typeof(float))
                {
                    text = String.Format("{0:R}f", value);
                    type = LanguageManager.LocalTypeManager.TypeSingle;
                }
                else if (valueType == typeof(double))
                {
                    text = String.Format("{0:R}f", value);
                    type = LanguageManager.LocalTypeManager.TypeDouble;
                }
                else if (valueType == typeof(int))
                {
                    text = value.ToString();
                    type = LanguageManager.LocalTypeManager.TypeInt32;
                }
                else if (valueType == typeof(byte))
                {
                    text = value.ToString();
                    type = LanguageManager.LocalTypeManager.TypeByte;
                }
                else if (valueType == typeof(sbyte))
                {
                    text = value.ToString();
                    type = LanguageManager.LocalTypeManager.TypeSByte;
                }
                else if (valueType == typeof(ulong))
                {
                    text = value.ToString() + "UL";
                    type = LanguageManager.LocalTypeManager.TypeUInt64;
                }
                else if (valueType == typeof(long))
                {
                    text = value.ToString() + "L";
                    type = LanguageManager.LocalTypeManager.TypeInt64;
                }
                else if (valueType == typeof(short))
                {
                    text = value.ToString();
                    type = LanguageManager.LocalTypeManager.TypeInt16;
                }
                else if (valueType == typeof(ushort))
                {
                    text = value.ToString();
                    type = LanguageManager.LocalTypeManager.TypeUInt16;
                }
                else if (valueType == typeof(uint))
                {
                    text = value.ToString() + "U";
                    type = LanguageManager.LocalTypeManager.TypeUInt32;
                }
                else
                {
                    //text = "[" + value.GetType().ToString() + " - " + value.ToString() + "]";
                    text = value.ToString();
                    type = LanguageManager.LocalTypeManager.TypeUnknown;
                    Debug.Fail("Should we be here?");
                }
            }
            StringData data = new StringData(text);
            data.LocalType = type;
            return (data);
        }

        public StringData GenerateCodeMemberInitializer(IExpression passedExpression, ParsingInfo info)
        {
            // These seems to be used to initialize attribute properties...
            // For example, something like:
            // [System.Security.Permissions.HostProtectionAttribute(System.Security.Permissions.SecurityAction.LinkDemand, MayLeakOnAbort = true)]
            IMemberInitializerExpression expression = (IMemberInitializerExpression)passedExpression;
            StringData data = LanguageManager.ReferenceGenerator.GenerateCodeMemberReference(expression.Member, info);
            data.AppendSameLine(" = ");
            data.AppendSameLine(GenerateCode(expression.Value, info));
            return (data);
        }

        public StringData GenerateCodeMethodInvoke(IExpression passedExpression, ParsingInfo info)
        {
            IMethodInvokeExpression expression = (IMethodInvokeExpression)passedExpression;
            StringData data = GenerateCode(expression.Method, info);
            StringData arguments = GenerateCodeMethodInvokeArguments(expression.Arguments, info);
            data.AppendSameLine(arguments);
            return (data);
        }

        public StringData GenerateCodeMethodInvokeArguments(IExpressionCollection arguments, ParsingInfo info)
        {
            StringData data = new StringData("(");
            if (info.MethodType == MethodType.NORMAL)
            {
                int index = 0;
                // Backup the methodReference as it could be overwritten by sub-expressions.
                IMethodReference methodReference = info.MethodReference;
                foreach (IExpression expression in arguments)
                {
                    if (index != 0)
                    {
                        data.AppendSameLine(", ");
                    }
                    StringData argument = GenerateCode(expression, info);

                    // Before we append, we need to test some implicit conversions...
                    // Int32 to Char for example...
                    // This code is costly, might want to optimize it or better find when it needs to be applied

                    // The same kind of check is done in CppReference.GenerateCodeMethodReference for unsafe
                    // TODO: Change the code so we do both checks...
                    // Try to optimize it more (maybe use an hashtable of method reference?)
                    if (index < methodReference.Parameters.Count)
                    {
                        // In some case we can have arguments > parameters
                        // I don't understand yet why it can happen... Probably with named parameters...
                        // TODO: Fix this...

                        IParameterDeclaration parameterDeclaration = methodReference.Parameters[index];
                        StringData parameter = LanguageManager.ReferenceGenerator.GenerateCodeType(parameterDeclaration.ParameterType, info);
                        LocalType parameterType = parameter.LocalType;
                        LocalType valueType = argument.LocalType;

                        Debug.Assert(parameterType != null);
                        Debug.Assert(valueType != null);

                        if (parameterType.Same(valueType) == false)
                        {
                            // The types are not the same! Do we have to fix?

                            if (LanguageManager.LocalTypeManager.TypeChar.Same(parameterType)
                                && LanguageManager.LocalTypeManager.TypeInt32.Same(valueType))
                            {
                                // Force the cast from int to char
                                // TODO: The best would be to convert the value ;)
                                data.AppendSameLine("(System.Char)");
                            }
                        }

                        // At the same time, fix any issue with ref and out
                        // With Reflector, ref and out can be switched with DllImport methods
                        // This code is a simplified version of CppReference.GenerateCodeParameterDeclarationCollection
                        // This one doesn't have to be as complete...

                        // TODO: Move this code to CppReference so it can be somewhat shared with the other...
                        // Try to add the unit test as well...
                        bool inAttribute = false, outAttribute = false;
                        if (parameterDeclaration.Attributes.Count > 0)
                        {
                            // This slow comparison is done only if the method has some attributes
                            // (Mostly the DllImport methods).
                            foreach (ICustomAttribute customAttribute in parameterDeclaration.Attributes)
                            {
                                string attrText = customAttribute.Constructor.DeclaringType.ToString();
                                if (attrText == "InAttribute")
                                {
                                    inAttribute = true;
                                }
                                else if (attrText == "OutAttribute")
                                {
                                    outAttribute = true;
                                }
                                else
                                {
                                    // Unknown, do nothing...
                                }
                            }

                            string expectedText = null;
                            string incorrectText = null;
                            bool alsoRef = false;

                            if (parameter.Text.EndsWith("&"))
                            {
                                alsoRef = true;
                            }

                            // Determine the correct combination
                            if (inAttribute)
                            {
                                if (outAttribute)
                                {
                                    // In and out, it's a ref and not a out
                                    if (alsoRef)
                                    {
                                        expectedText = "ref ";
                                    }
                                    else
                                    {
                                        // If no "&", then we don't push explicitly the ref
                                        expectedText = "";
                                    }
                                    incorrectText = "out ";
                                }
                                else
                                {
                                    if (alsoRef)
                                    {
                                        expectedText = "ref ";
                                        incorrectText = "out ";
                                    }
                                }
                            }
                            else if (outAttribute)
                            {
                                // out only, it's an out and not a ref
                                if (alsoRef)
                                {
                                    expectedText = "out ";
                                }
                                else
                                {
                                    expectedText = "";
                                }
                                incorrectText = "ref ";
                            }

                            if (incorrectText != null)
                            {
                                if (argument.Text.StartsWith(expectedText))
                                {
                                    // All good...
                                }
                                else if (argument.Text.StartsWith(incorrectText))
                                {
                                    // The argument started with the incorrect combination
                                    // Fix it...
                                    string text = argument.Text;
                                    text = text.Substring(incorrectText.Length);
                                    text = expectedText + text;

                                    // Replace the text completely
                                    argument.Replace(text);
                                }
                                else
                                {
                                    // We don't recognize the combination, ref or out is certainly missing...
                                    string text = argument.Text;
                                    argument.PrefixSameLine(expectedText);
                                }
                            }
                        }
                    }

                    data.AppendSameLine(argument);
                    ++index;
                }
            }
            else
            {
                // Backup the methodName as it could be overwritten by sub-expressions.
                // In that case method name corresponds to the operator...
                string methodName = info.MethodName;
                MethodType methodType = info.MethodType;
                // Special case for operators...
                if (arguments.Count == 2)
                {
                    // Binary operator...
                    StringData left = GenerateCode(arguments[0], info);
                    StringData right = GenerateCode(arguments[1], info);

                    data.AppendSameLine("(");
                    data.AppendSameLine(left);
                    data.AppendSameLine(") ");
                    data.AppendSameLine(methodName);
                    data.AppendSameLine(" (");
                    data.AppendSameLine(right);
                    data.AppendSameLine(")");
                }
                else if (arguments.Count == 1)
                {
                    // Unary operator...
                    StringData expression = GenerateCode(arguments[0], info);

                    if (methodType == MethodType.OPERATOR_TRUE)
                    {
                        // used in case like if (myType)
                        data.AppendSameLine(expression);
                    }
                    else if (methodType == MethodType.OPERATOR_FALSE)
                    {
                        // don't know what to generate here...
                        Debug.Fail("Not implemented yet!");
                    }
                    else
                    {
                        data.AppendSameLine(methodName);
                        data.AppendSameLine("(");
                        data.AppendSameLine(expression);
                        data.AppendSameLine(")");
                    }
                }
                else
                {
                    Debug.Fail("Should not be here!");
                }
            }
            data.AppendSameLine(")");
            return (data);
        }

        public StringData GenerateCodeMethodOf(IExpression passedExpression, ParsingInfo info)
        {
            Debug.Fail("Not implemented!");
            return (null);
        }

        public StringData GenerateCodeMethodReference(IExpression passedExpression, ParsingInfo info)
        {
            IMethodReferenceExpression expression = (IMethodReferenceExpression)passedExpression;
            StringData data;
            StringData target = GenerateCode(expression.Target, info);
            StringData methodReference = LanguageManager.ReferenceGenerator.GenerateCodeMethodReference(expression.Method, info);
            if (info.MethodType == MethodType.NORMAL)
            {
                // Not an operator, append the method name with the target...

                IType methodDeclaringType = expression.Method.DeclaringType;

                bool standardCall = (target.EmbeddedType.CompareTo(methodDeclaringType) == 0);
                if ((target.Text == "this") || (target.Text == "base"))
                {
                    // If it is "base", mostly from the base constructor do a standard call
                    // As we don't want a cast because the types are not different
                    standardCall = true;
                }

                if (standardCall)
                {
/*
                    if (target.Text == "System.Threading.Interlocked")
                    {
                        Trace.WriteLine("Boo!");
                    }
*/
                    IMethodDeclaration methodDeclaration = expression.Method.Resolve();
                    if ((methodDeclaration.Body == null) && methodDeclaration.NewSlot)
                    {
                        // It means either it is an abstract method or an interface, extern
                        // In that case forces the cast, as the method could be explicitly overriden
                        // like: void System.IDisposable.Dispose() {}
                        // And as such we can't call this.Dispose();
                        // The only way is to call it like this:
                        // ((System.IDisposable)(this)).Dispose();

                        // If that's not a new slot (i.e. a base class contained the function already), that's not it
                        // If static method, that's not it (actually covered by the new slot test already).

                        standardCall = false;
                    }
                    if (methodDeclaration.Visibility == MethodVisibility.Family)
                    {
                        // if the original method is protected, remove the cast in order to avoid error CS1540
                        // Cannot access protected member 'member' via a qualifier of type 'type1'; the qualifier must be of type 'type2' (or derived from it)
                        // Although a derived class can access protected members of its base class, it cannot do so through an instance of the base class.
                        standardCall = true;
                    }
                }
                ITypeReference declaringReferenceType = methodDeclaringType as ITypeReference;
                if (declaringReferenceType != null)
                {
                    // This kind of cast does not work if generics are involved in C#
                    // I'm wondering why...
                    if (declaringReferenceType.GenericArguments.Count != 0)
                    {
                        standardCall = true;
                    }
                }

                // TODO: Improve here and restrict to real use case issues...
                // For example implicit conversion from char to int32
                // So we are not poluting all the other cases...

                if (standardCall)
                {
                    // Type of the target exactly the same as the type of the method
                    if (expression.Target is IAssignExpression)
                    {
                        // Special case if that's an assign expression...
                        // We have to encapsulate it with parenthesis, otherwise the meaning won't be the same...
                        data = new StringData("(");
                        data.AppendSameLine(target);
                        data.AppendSameLine(")");
                    }
                    else
                    {
                        data = target;
                    }
                    data.AppendSameLine(".");
                    data.AppendSameLine(methodReference);
                }
                else
                {
                    // Not exactly the same, this can be due because we are calling a base type method
                    // or because there was an implicit cast...
                    // For the moment, make it explicit...

                    data = new StringData("((");
                    data.AppendSameLine(LanguageManager.ReferenceGenerator.GenerateCodeType(methodDeclaringType, info));
                    data.AppendSameLine(")(");
                    data.AppendSameLine(target);
                    data.AppendSameLine(")).");
                    data.AppendSameLine(methodReference);
                }
            }
            else
            {
                // Do nothing, everything will be done by invoke arguments later...
                data = new StringData();
            }
            data.EmbeddedType = expression.Method.ReturnType.Type;      // EmbeddedType used on purpose
            return (data);
        }

        /*
        public StringData GenerateCodeNamedArgument(IExpression passedExpression, ParsingInfo info)
        {
            // These seems to be used to initialize attribute properties...
            // For example, something like:
            // [System.Security.Permissions.HostProtectionAttribute(System.Security.Permissions.SecurityAction.LinkDemand, MayLeakOnAbort = true)]
            INamedArgumentExpression expression = (INamedArgumentExpression)passedExpression;
            StringData data = LanguageManager.ReferenceGenerator.GenerateCodeMemberReference(expression.Member, info);
            data.AppendSameLine(" = ");
            data.AppendSameLine(GenerateCode(expression.Value, info));
            return (data);
        }
         */

        public StringData GenerateCodeNullCoalescing(IExpression passedExpression, ParsingInfo info)
        {
            Debug.Fail("Not implemented!");
            return (null);
        }

        public StringData GenerateCodeObjectCreate(IExpression passedExpression, ParsingInfo info)
        {
            IObjectCreateExpression expression = (IObjectCreateExpression)passedExpression;
            StringData data = new StringData("new ");
            StringData declaringType = LanguageManager.ReferenceGenerator.GenerateCodeType(expression.Type, info);
            data.AppendSameLine(declaringType);

            info.MethodReference = expression.Constructor;
            StringData arguments = GenerateCodeMethodInvokeArguments(expression.Arguments, info);
            data.AppendSameLine(arguments);
            data.LocalType = declaringType.LocalType;
            return (data);
        }

        /*
        public StringData GenerateCodeObjectInitialize(IExpression passedExpression, ParsingInfo info)
        {
            // Used for generics, default(T) returns null if T is a class, return 0 if T is a struct
            IObjectInitializeExpression expression = (IObjectInitializeExpression)passedExpression;
            StringData data = new StringData("default(");
            StringData genericClass = LanguageManager.ReferenceGenerator.GenerateCodeType(expression.Type, info);
            data.AppendSameLine(genericClass);
            data.AppendSameLine(")");
            data.LocalType = LocalTypeManager.TypeNull;
            return (data);
        }
         */

        public StringData GenerateCodePropertyIndexer(IExpression passedExpression, ParsingInfo info)
        {
            IPropertyIndexerExpression expression = (IPropertyIndexerExpression)passedExpression;
            StringData data = GenerateCode(expression.Target, info);
            data.AppendSameLine("[");
            bool first = true;
            foreach (IExpression eachIndice in expression.Indices)
            {
                if (first == false)
                {
                    data.AppendSameLine(", ");
                }
                first = false;
                data.AppendSameLine(GenerateCode(eachIndice, info));
            }
            data.AppendSameLine("]");
            return (data);
        }

        public StringData GenerateCodePropertyReference(IExpression passedExpression, ParsingInfo info)
        {
            IPropertyReferenceExpression expression = (IPropertyReferenceExpression)passedExpression;
            StringData data;
            StringData target = GenerateCode(expression.Target, info);
            StringData property = LanguageManager.ReferenceGenerator.GenerateCodePropertyReference(expression.Property);
            // By testing expression.Property.Parameters.Count, we might be able to get rid of hard-coded "Item" and "Chars"

            if (expression.Target is IAssignExpression)
            {
                // Special case if that's an assign expression...
                // We have to encapsulate it with parenthesis, otherwise the meaning won't be the same...
                data = new StringData("(");
                data.AppendSameLine(target);
                data.AppendSameLine(")");
            }
            else
            {
                data = target;
            }

            if (expression.Property.Parameters.Count != 0)
            {
                // There are some parameters for the property, it's an indexer then...
                // Only the target is needed
            }
            else
            {
                data.AppendSameLine(".");
                data.AppendSameLine(property);
            }
            data.EmbeddedType = expression.Property.PropertyType;       // EmbeddedType used on purpose
            return (data);
        }

        public StringData GenerateCodeSizeOf(IExpression passedExpression, ParsingInfo info)
        {
            ISizeOfExpression expression = (ISizeOfExpression)passedExpression;
            StringData data = new StringData("sizeof(");
            data.AppendSameLine(LanguageManager.ReferenceGenerator.GenerateCodeType(expression.Type, info));
            data.AppendSameLine(")");
            info.UnsafeMethod = true; // sizeof of predefined type is NOT unsafe, but non-predefined type is unsafe
                                      // predefined type won't generate sizeof opcode (the size will directly be hard-coded).
                                      // so if we have a sizeof opcode, it means that we are in an unsafe method
            data.LocalType = LanguageManager.LocalTypeManager.TypeInt32;
            return (data);
        }

        public StringData GenerateCodeSnippet(IExpression passedExpression, ParsingInfo info)
        {
            Debug.Fail("Not implemented!");
            return (null);
        }

        public StringData GenerateCodeStackAllocate(IExpression passedExpression, ParsingInfo info)
        {
            IStackAllocateExpression expression = (IStackAllocateExpression)passedExpression;
            StringData data = new StringData("stackalloc ");

            // Because of the stackalloc incorrectly generated code
            //      System.Int32 * numPtr1 = stackalloc System.Int32[100];
            //  Generates:
            //      System.Int32 * numPtr1 = (System.Int32 *)stackalloc System.Byte[4 * 100];

            StringData stackallocType = null;
            switch (info.CurrentCast.Count)
            {
                case 1:
                    IType overrideType = info.CurrentCast.Peek();
                    // use the type on the stack (from the cast) instead of the one provided with stackalloc
                    stackallocType = LanguageManager.ReferenceGenerator.GenerateCodeType(overrideType, info);
                    // stackalloc is casting with a pointer, but the type is not the pointer
                    // Remove one level of pointer
                    stackallocType.TrimOneCharLeft('*');
                    stackallocType.TrimOneCharLeft(' ');   // And the final space as well
                    data.AppendSameLine(stackallocType);
                    break;

                default:
                    // We should not be here as only one cast is expected!
                    // But do nothing and try to recover from it

                case 0:
                    // Somehow there was not cast!
                    // Maybe we were allocating System.Byte already!
                    data.AppendSameLine(LanguageManager.ReferenceGenerator.GenerateCodeType(expression.Type, info));
                    break;
            }

            StringData size = GenerateCode(expression.Expression, info);

            if (stackallocType != null)
            {
                // We override the type, it means that the size is not correct
                // We have to update the size accordingly
                // (Pretty much put it get back the number that was passed originally)

                size.PrefixSameLine("(");
                size.AppendSameLine(") / sizeof(");
                // So remove one level of pointer...
                size.AppendSameLine(stackallocType);
                size.AppendSameLine(")");
                data.LocalType = stackallocType.LocalType;
            }
            else
            {
                // Rare case (I can think only of stackalloc System.Byte[...]),
                // But still use the default pointer type, to make the code more reliable...
                data.LocalType = LanguageManager.LocalTypeManager.TypePointer;
            }

            data.AppendSameLine("[");
            data.AppendSameLine(size);
            data.AppendSameLine("]");
            info.UnsafeMethod = true;
            info.InStackAlloc = true;
            return (data);
        }

        public StringData GenerateCodeThisReference(IExpression passedExpression, ParsingInfo info)
        {
            StringData data = new StringData("this");
            data.EmbeddedType = info.DeclaringType;     // EmbeddedType used on purpose
            return (data);
        }

        public StringData GenerateCodeTryCast(IExpression passedExpression, ParsingInfo info)
        {
            ITryCastExpression expression = (ITryCastExpression)passedExpression;
            StringData data = new StringData("(");
            data.AppendSameLine(GenerateCode(expression.Expression, info));
            data.AppendSameLine(" as ");
            StringData targetType = LanguageManager.ReferenceGenerator.GenerateCodeType(expression.TargetType, info);
            data.AppendSameLine(targetType);
            data.AppendSameLine(")");
            data.LocalType = targetType.LocalType;
            return (data);
        }

        public StringData GenerateCodeTypedReferenceCreate(IExpression passedExpression, ParsingInfo info)
        {
            Debug.Fail("Not implemented!");
            return (null);
        }

        public StringData GenerateCodeTypeOf(IExpression passedExpression, ParsingInfo info)
        {
            ITypeOfExpression expression = (ITypeOfExpression)passedExpression;
            StringData data = new StringData("typeof(");
            data.Append(LanguageManager.ReferenceGenerator.GenerateCodeType(expression.Type, info));
            data.Append(")");
            data.LocalType = LanguageManager.LocalTypeManager.TypeOfType;
            return (data);
        }

        public StringData GenerateCodeTypeOfTypedReference(IExpression passedExpression, ParsingInfo info)
        {
            Debug.Fail("Not implemented!");
            return (null);
        }

        public StringData GenerateCodeTypeReference(IExpression passedExpression, ParsingInfo info)
        {
            ITypeReferenceExpression expression = (ITypeReferenceExpression)passedExpression;
            return (LanguageManager.ReferenceGenerator.GenerateCodeTypeReference(expression.Type, info));
        }

        public StringData GenerateCodeUnary(IExpression passedExpression, ParsingInfo info)
        {
            // This list MUST match UnaryOperator order
            string[] operators = new string[]
            {
                "-{0}",
                "!{0}",
                "~{0}",
                "++{0}",
                "--{0}",
                "{0}++",
                "{0}--",
            };

            IUnaryExpression expression = (IUnaryExpression)passedExpression;

            IExpression subExpression = expression.Expression;
            StringData data = null;
            StringData subData = GenerateCode(subExpression, info);
            if (NeedEncapsulation(subExpression))
            {
                data = new StringData();
                data.AppendSameLine("(");
                data.AppendSameLine(subData);
                data.AppendSameLine(")");
            }
            else
            {
                data = subData;
            }

            // TODO: Optimize by not using String.Format
            //  Also the return value might be a bool when using ! operator
            string text = String.Format(operators[(int)expression.Operator], data.Text);
            StringData data2 = new StringData(text);
            data2.LocalType = subData.LocalType;
            return (data2);
        }

        public StringData GenerateCodeValueOfTypedReference(IExpression passedExpression, ParsingInfo info)
        {
            Debug.Fail("Not implemented!");
            return (null);
        }

        public StringData GenerateCodeVariableDeclaration(IExpression passedExpression, ParsingInfo info)
        {
            IVariableDeclarationExpression expression = (IVariableDeclarationExpression)passedExpression;
            StringData data = LanguageManager.ReferenceGenerator.GenerateCodeVariableDeclaration(expression.Variable, info);
            return (data);
        }

        public StringData GenerateCodeVariableReference(IExpression passedExpression, ParsingInfo info)
        {
            IVariableReferenceExpression expression = (IVariableReferenceExpression)passedExpression;
            StringData data = LanguageManager.ReferenceGenerator.GenerateCodeVariableReference(expression.Variable);
            return (data);
        }

        public void ParseExpression(IExpression expression, ParsingInfo info)
        {
            if (expression == null)
            {
                return;
            }

            // Try to use the cache first
            // Get the type of the expression (i.e. the implementation)
            Type expressionType = expression.GetType();
            // See if there is a corresponding delegate for this type...
            ParseExpressionDelegate del;
            bool foundIt = sParseDelegates.TryGetValue(expressionType, out del);
            if (foundIt)
            {
                // Yes, there is one delegate
                if (del != null)
                {
                    // And it is not null, so it means that the expression has to be parsed...
                    del(expression, info);
                }
                return;
            }

            // There was no corresponding delegate, do the slow comparison
            // At the same time, it will populate the hashtable for the next time...

            del = null;

            if (expression is IAddressDereferenceExpression)
            {
                del = ParseAddressDereference;
            }
            else if (expression is IAddressOfExpression)
            {
                del = ParseAddressOf;
            }
            else if (expression is IAddressOutExpression)
            {
                del = ParseAddressOut;
            }
            else if (expression is IAddressReferenceExpression)
            {
                del = ParseAddressReference;
            }
            else if (expression is IAnonymousMethodExpression)
            {
                del = ParseAnonymousMethod;
            }
            else if (expression is IArgumentListExpression)
            {
                del = ParseArgumentList;
            }
            else if (expression is IArgumentReferenceExpression)
            {
                del = ParseArgumentReference;
            }
            else if (expression is IArrayCreateExpression)
            {
                del = ParseArrayCreate;
            }
            else if (expression is IArrayIndexerExpression)
            {
                del = ParseArrayIndexer;
            }
            else if (expression is IAssignExpression)
            {
                del = ParseAssign;
            }
            else if (expression is IBaseReferenceExpression)
            {
                del = ParseBaseReference;
            }
            else if (expression is IBinaryExpression)
            {
                del = ParseBinary;
            }
            else if (expression is ICanCastExpression)
            {
                del = ParseCanCast;
            }
            else if (expression is ICastExpression)
            {
                del = ParseCast;
            }
            else if (expression is IConditionExpression)
            {
                del = ParseCondition;
            }
            else if (expression is IDelegateCreateExpression)
            {
                del = ParseDelegateCreate;
            }
            else if (expression is IDelegateInvokeExpression)
            {
                del = ParseDelegateInvoke;
            }
            else if (expression is IEventReferenceExpression)
            {
                del = ParseEventReference;
            }
            else if (expression is IFieldOfExpression)
            {
                del = ParseFieldOf;
            }
            else if (expression is IFieldReferenceExpression)
            {
                del = ParseFieldReference;
            }
            else if (expression is IGenericDefaultExpression)
            {
                del = ParseGenericDefault;
            }
            else if (expression is ILiteralExpression)
            {
                del = ParseLiteral;
            }
            else if (expression is IMemberInitializerExpression)
            {
                del = ParseMemberInitializer;
            }
            else if (expression is IMethodInvokeExpression)
            {
                del = ParseMethodInvoke;
            }
            else if (expression is IMethodOfExpression)
            {
                del = ParseMethodOf;
            }
            else if (expression is IMethodReferenceExpression)
            {
                del = ParseMethodReference;
            }
            else if (expression is INullCoalescingExpression)
            {
                del = ParseNullCoalescing;
            }
            else if (expression is IObjectCreateExpression)
            {
                del = ParseObjectCreate;
            }
            else if (expression is IPropertyIndexerExpression)
            {
                del = ParsePropertyIndexer;
            }
            else if (expression is IPropertyReferenceExpression)
            {
                del = ParsePropertyReference;
            }
            else if (expression is ISizeOfExpression)
            {
                del = ParseSizeOf;
            }
            else if (expression is ISnippetExpression)
            {
                del = ParseSnippet;
            }
            else if (expression is IStackAllocateExpression)
            {
                del = ParseStackAllocate;
            }
            else if (expression is IThisReferenceExpression)
            {
                del = ParseThisReference;
            }
            else if (expression is ITryCastExpression)
            {
                del = ParseTryCast;
            }
            else if (expression is ITypedReferenceCreateExpression)
            {
                del = ParseTypedReferenceCreate;
            }
            else if (expression is ITypeOfExpression)
            {
                del = ParseTypeOf;
            }
            else if (expression is ITypeOfTypedReferenceExpression)
            {
                del = ParseTypeOfTypedReference;
            }
            else if (expression is ITypeReferenceExpression)
            {
                del = ParseTypeReference;
            }
            else if (expression is IUnaryExpression)
            {
                del = ParseUnary;
            }
            else if (expression is IValueOfTypedReferenceExpression)
            {
                del = ParseValueOfTypedReference;
            }
            else if (expression is IVariableDeclarationExpression)
            {
                del = ParseVariableDeclaration;
            }
            else if (expression is IVariableReferenceExpression)
            {
                del = ParseVariableReference;
            }
            else
            {
                del = null;
            }

            // Add the delegate on the hashtable to optimize the next time
            sParseDelegates[expressionType] = del;
            if (del != null)
            {
                del(expression, info);
            }
        }

        public void ParseAddressDereference(IExpression passedExpression, ParsingInfo info)
        {
            IAddressDereferenceExpression expression = (IAddressDereferenceExpression)passedExpression;
            ParseExpression(expression.Expression, info);
        }

        public void ParseAddressOf(IExpression passedExpression, ParsingInfo info)
        {
            IAddressOfExpression expression = (IAddressOfExpression)passedExpression;
            ParseExpression(expression.Expression, info);
        }

        public void ParseAddressOut(IExpression passedExpression, ParsingInfo info)
        {
            IAddressOutExpression expression = (IAddressOutExpression)passedExpression;
            ParseExpression(expression.Expression, info);
        }

        public void ParseAddressReference(IExpression passedExpression, ParsingInfo info)
        {
            IAddressDereferenceExpression expression = (IAddressDereferenceExpression)passedExpression;
            ParseExpression(expression.Expression, info);
        }

        public void ParseAnonymousMethod(IExpression passedExpression, ParsingInfo info)
        {
            IAnonymousMethodExpression expression = (IAnonymousMethodExpression)passedExpression;
            LanguageManager.StatementGenerator.ParseBlock(expression.Body, info);
        }

        public void ParseArgumentList(IExpression passedExpression, ParsingInfo info)
        {
            IArgumentListExpression expression = (IArgumentListExpression)passedExpression;
            // Do nothing...
        }

        public void ParseArgumentReference(IExpression passedExpression, ParsingInfo info)
        {
            IArgumentReferenceExpression expression = (IArgumentReferenceExpression)passedExpression;
            info.UseParameter(expression.Parameter.Name, expression.Parameter.Resolve());
        }

        public void ParseArrayCreate(IExpression passedExpression, ParsingInfo info)
        {
            IArrayCreateExpression expression = (IArrayCreateExpression)passedExpression;
            foreach (IExpression subExpression in expression.Dimensions)
            {
                ParseExpression(subExpression, info);
            }

            ParseBlock(expression.Initializer, info);
        }

        public void ParseArrayIndexer(IExpression passedExpression, ParsingInfo info)
        {
            IArrayIndexerExpression expression = (IArrayIndexerExpression)passedExpression;
            ParseExpression(expression.Target, info);

            foreach (IExpression subExpression in expression.Indices)
            {
                ParseExpression(subExpression, info);
            }
        }

        public void ParseAssign(IExpression passedExpression, ParsingInfo info)
        {
            IAssignExpression expression = (IAssignExpression)passedExpression;
            ParseExpression(expression.Target, info);
            ParseExpression(expression.Expression, info);
        }

        public void ParseBaseReference(IExpression passedExpression, ParsingInfo info)
        {
            IBaseReferenceExpression expression = (IBaseReferenceExpression)passedExpression;
            info.UsePredefinedMember("base", Kind.Base);
        }

        public void ParseBinary(IExpression passedExpression, ParsingInfo info)
        {
            IBinaryExpression expression = (IBinaryExpression)passedExpression;
            ParseExpression(expression.Left, info);
            ParseExpression(expression.Right, info);
        }

        public void ParseBlock(IExpression passedExpression, ParsingInfo info)
        {
            IBlockExpression expression = (IBlockExpression)passedExpression;
            foreach (IExpression subExpression in expression.Expressions)
            {
                ParseExpression(subExpression, info);
            }
        }

        public void ParseCanCast(IExpression passedExpression, ParsingInfo info)
        {
            ICanCastExpression expression = (ICanCastExpression)passedExpression;
            ParseExpression(expression.Expression, info);
        }

        public void ParseCast(IExpression passedExpression, ParsingInfo info)
        {
            ICastExpression expression = (ICastExpression)passedExpression;
            ParseExpression(expression.Expression, info);
        }

        public void ParseCondition(IExpression passedExpression, ParsingInfo info)
        {
            IConditionExpression expression = (IConditionExpression)passedExpression;
            ParseExpression(expression.Condition, info);
            ParseExpression(expression.Then, info);
            ParseExpression(expression.Else, info);
        }

        public void ParseDelegateCreate(IExpression passedExpression, ParsingInfo info)
        {
            IDelegateCreateExpression expression = (IDelegateCreateExpression)passedExpression;
            ParseExpression(expression.Target, info);
        }

        public void ParseDelegateInvoke(IExpression passedExpression, ParsingInfo info)
        {
            IDelegateInvokeExpression expression = (IDelegateInvokeExpression)passedExpression;
            ParseExpression(expression.Target, info);
            foreach (IExpression subExpression in expression.Arguments)
            {
                ParseExpression(subExpression, info);
            }
        }

        public void ParseEventReference(IExpression passedExpression, ParsingInfo info)
        {
            IEventReference expression = (IEventReference)passedExpression;
            // Do nothing...
        }

        public void ParseFieldOf(IExpression passedExpression, ParsingInfo info)
        {
            IFieldOfExpression expression = (IFieldOfExpression)passedExpression;
            // Do nothing...
        }

        public void ParseFieldReference(IExpression passedExpression, ParsingInfo info)
        {
            IFieldReferenceExpression expression = (IFieldReferenceExpression)passedExpression;
            ParseExpression(expression.Target, info);
        }

        public void ParseGenericDefault(IExpression passedExpression, ParsingInfo info)
        {
            IGenericDefaultExpression expression = (IGenericDefaultExpression)passedExpression;
            // Do nothing...
        }

        public void ParseLiteral(IExpression passedExpression, ParsingInfo info)
        {
            ILiteralExpression expression = (ILiteralExpression)passedExpression;
            // Do nothing...
        }

        public void ParseMemberInitializer(IExpression passedExpression, ParsingInfo info)
        {
            IMemberInitializerExpression expression = (IMemberInitializerExpression)passedExpression;
            ParseExpression(expression.Value, info);
        }

        public void ParseMethodInvoke(IExpression passedExpression, ParsingInfo info)
        {
            IMethodInvokeExpression expression = (IMethodInvokeExpression)passedExpression;
            ParseExpression(expression.Method, info);

            foreach (IExpression subExpression in expression.Arguments)
            {
                ParseExpression(subExpression, info);
            }
        }

        public void ParseMethodOf(IExpression passedExpression, ParsingInfo info)
        {
            IMethodOfExpression expression = (IMethodOfExpression)passedExpression;
            // Do nothing...
        }

        public void ParseMethodReference(IExpression passedExpression, ParsingInfo info)
        {
            IMethodReferenceExpression expression = (IMethodReferenceExpression)passedExpression;
            ParseExpression(expression.Target, info);
        }

        public void ParseNullCoalescing(IExpression passedExpression, ParsingInfo info)
        {
            INullCoalescingExpression expression = (INullCoalescingExpression)passedExpression;
            ParseExpression(expression.Condition, info);
            ParseExpression(expression.Expression, info);
        }

        public void ParseObjectCreate(IExpression passedExpression, ParsingInfo info)
        {
            IObjectCreateExpression expression = (IObjectCreateExpression)passedExpression;

            foreach (IExpression subExpression in expression.Arguments)
            {
                ParseExpression(subExpression, info);
            }

            ParseBlock(expression.Initializer, info);
        }

        public void ParsePropertyIndexer(IExpression passedExpression, ParsingInfo info)
        {
            IPropertyIndexerExpression expression = (IPropertyIndexerExpression)passedExpression;
            ParsePropertyReference(expression.Target, info);

            foreach (IExpression subExpression in expression.Indices)
            {
                ParseExpression(subExpression, info);
            }
        }

        public void ParsePropertyReference(IExpression passedExpression, ParsingInfo info)
        {
            IPropertyReferenceExpression expression = (IPropertyReferenceExpression)passedExpression;
            ParseExpression(expression.Target, info);
        }

        public void ParseSizeOf(IExpression passedExpression, ParsingInfo info)
        {
            ISizeOfExpression expression = (ISizeOfExpression)passedExpression;
            // Do nothing...
        }

        public void ParseSnippet(IExpression passedExpression, ParsingInfo info)
        {
            ISnippetExpression expression = (ISnippetExpression)passedExpression;
            // Do nothing...
        }

        public void ParseStackAllocate(IExpression passedExpression, ParsingInfo info)
        {
            IStackAllocateExpression expression = (IStackAllocateExpression)passedExpression;
            ParseExpression(expression.Expression, info);
        }

        public void ParseThisReference(IExpression passedExpression, ParsingInfo info)
        {
            IThisReferenceExpression expression = (IThisReferenceExpression)passedExpression;
            info.UsePredefinedMember("this", Kind.This);
        }

        public void ParseTryCast(IExpression passedExpression, ParsingInfo info)
        {
            ITryCastExpression expression = (ITryCastExpression)passedExpression;
            ParseExpression(expression.Expression, info);
        }

        public void ParseTypedReferenceCreate(IExpression passedExpression, ParsingInfo info)
        {
            ITypedReferenceCreateExpression expression = (ITypedReferenceCreateExpression)passedExpression;
            ParseExpression(expression.Expression, info);
        }

        public void ParseTypeOf(IExpression passedExpression, ParsingInfo info)
        {
            ITypeOfExpression expression = (ITypeOfExpression)passedExpression;
            // Do nothing...
        }

        public void ParseTypeOfTypedReference(IExpression passedExpression, ParsingInfo info)
        {
            ITypeOfTypedReferenceExpression expression = (ITypeOfTypedReferenceExpression)passedExpression;
            ParseExpression(expression.Expression, info);
        }

        public void ParseTypeReference(IExpression passedExpression, ParsingInfo info)
        {
            ITypeReferenceExpression expression = (ITypeReferenceExpression)passedExpression;
            // Do nothing...
        }

        public void ParseUnary(IExpression passedExpression, ParsingInfo info)
        {
            IUnaryExpression expression = (IUnaryExpression)passedExpression;
            ParseExpression(expression.Expression, info);
        }

        public void ParseValueOfTypedReference(IExpression passedExpression, ParsingInfo info)
        {
            IValueOfTypedReferenceExpression expression = (IValueOfTypedReferenceExpression)passedExpression;
            ParseExpression(expression.Expression, info);
        }

        public void ParseVariableDeclaration(IExpression passedExpression, ParsingInfo info)
        {
            IVariableDeclarationExpression expression = (IVariableDeclarationExpression)passedExpression;
            info.DeclareVariable(expression.Variable.Name, expression.Variable);
        }

        public void ParseVariableReference(IExpression passedExpression, ParsingInfo info)
        {
            IVariableReference expression = (IVariableReference)passedExpression;
        }

        private IDictionary<Type, GenerateExpressionDelegate> sGenerateDelegates = new Dictionary<Type, GenerateExpressionDelegate>();
        private IDictionary<Type, ParseExpressionDelegate> sParseDelegates = new Dictionary<Type, ParseExpressionDelegate>();
    }
}

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

namespace CrossNet.CppRuntime
{
    public class CppExpressionGenerator : IExpressionGenerator
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
            StringData expr = GenerateCode(expression.Expression, info);

            IType embeddedType = expr.EmbeddedType;
            IPointerType pointerType = embeddedType as IPointerType;
            if (pointerType != null)
            {
                // 99% of the type, this should be the case
                // Remove one level of indirection

                StringData data = new StringData("*(");
                data.AppendSameLine(expr);
                data.AppendSameLine(")");
                info.UnsafeMethod = true; // Address dereference implies unsafe code

                // Update the corresponding type
                data.EmbeddedType = pointerType.ElementType;
                return (data);
            }
            else
            {
                // Otherwise, no change (no indirection, same type...)
                // Reflector sometime returns the incorrect level of pointer
                // We actually correct it here...
                return (expr);
            }
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
#if REF_OUT || true
            StringData data = new StringData("& ");
#else
            StringData data = new StringData();
#endif
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
            StringData data;
/*
 * This should be fixed now that this on struct is actually interpreted generated as (*this)
 * So there is no differentiation between struct field and "this" poiting to a struct 
 * 
            if (expression.Expression is IThisReferenceExpression)
            {
                // We cannot get reference on this
                // It can happen when calling Foo(ref this); With "this" pointing to a struct type
                data = new StringData();
            }
            else
 */
            {
#if REF_OUT || true
                data = new StringData("& ");
#else
                data = new StringData();
#endif
            }
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

            Debug.Assert(info.AnonymousMethodClass != null);

            info.WithinAnonymousMethod = true;

            // Backup and set the anonymous return type, so the generated code is correct...
            LocalType backupReturnType = info.ReturnedType;
            info.ReturnedType = LanguageManager.LocalTypeManager.GetLocalType(expression.ReturnType.Type);

            StringData anonymousMethodBody = LanguageManager.StatementGenerator.GenerateCodeBlock(expression.Body, info);

            // After generation, restore the return type
            info.ReturnedType = backupReturnType;

            info.WithinAnonymousMethod = false;

            string methodName = LanguageManager.TypeGenerator.AddAnonymousMethod(info.AnonymousMethodClass, expression.ReturnType, expression.Parameters, anonymousMethodBody, info);

            StringData delegateCreation = GenerateCodeDelegateCreate(expression.DelegateType as ITypeReference, info.AnonymousMethodClass, info.AnonymousMethodObject, "&" + info.AnonymousMethodClass + "::" + methodName, info);
            data.Append(delegateCreation);

            data.LocalType = LanguageManager.LocalTypeManager.GetLocalType(expression.DelegateType);
            return (data);
        }

        public StringData GenerateCodeArgumentList(IExpression passedExpression, ParsingInfo info)
        {
            StringData data = new StringData("__arglist");
            data.LocalType = LanguageManager.LocalTypeManager.TypeUnknown;
            return (data);
        }

        public StringData GenerateCodeArgumentReference(IExpression passedExpression, ParsingInfo info)
        {
            IArgumentReferenceExpression expression = (IArgumentReferenceExpression)passedExpression;
            StringData data = LanguageManager.ReferenceGenerator.GenerateCodeParameterReference(expression.Parameter);

#if REF_OUT || true // Disabled (*var) for ref and out variable as it should be handled by & now...
            // This whole code is now invalid...
            // TODO: revisit this...
            VariableInfo varInfo;
            string varName = data.Text;

            if (info.Variables != null)
            {
                AnonymousVariable var;
                if (info.Variables.TryGetValue(expression.Parameter.Name, out var))
                {
                    if (var.Declared == Declared.Outside)
                    {
                        if (info.WithinAnonymousMethod)
                        {
                            // Should not be necessary but at the same time this should help detect more issues...
                            data.PrefixSameLine("this->");
                        }
                        else
                        {
                            // This parameter is used within the anonymous method, we have to use the shared variable instead
                            data.Replace(var.NewName);
                        }
                    }
                }
            }

            if (info.Parameters.TryGetValue(varName, out varInfo))
            {
                if ((varInfo.Mode == VariableMode.OUT) || (varInfo.Mode == VariableMode.REF))
                {
                    StringData variable = new StringData("(*");
                    variable.AppendSameLine(data);
                    variable.AppendSameLine(")");

                    // Because we added the dereferencing, we should skip one level of indirection...
                    // Like:
                    // void Foo(ref Int32 a)
                    // Is going to be transformed internally by .NET in:
                    // void Foo(Int32 & a)
                    // Which in return is going to be transformed by CrossNet in:
                    // void Foo(Int32 * a)

                    // But then when we do:
                    //  a = 10;

                    // This is transformed within CrossNet by:
                    //  (*a) = 10;
                    //  The type of (*a) being Int32

                    // We need to transform the type from "Int32 &" to "Int32"
                    // This is valid for any complex type...
                    IReferenceType refType = data.EmbeddedType as IReferenceType;
                    if (refType != null)
                    {
                        LocalType localType = LanguageManager.LocalTypeManager.GetLocalType(refType.ElementType);
                        variable.LocalType = localType;
                    }
                    else
                    {
                        Debug.Fail("We didn't find a level of indirection to skip!");
                    }
                    return (variable);
                }
            }
            else
            {
                // This case can happen during a set property with the parameter "value"
                // But in that case, the parameter is not a ref nor a out, so there is no consequence

                // Sept 07: See if this assert is relevant anymore with anonymous methods
                //Debug.Assert(varName == "value", "We should find the parameter!");
            }
#else
            // TODO: Update the comment
            IReferenceType refType = data.EmbeddedType as IReferenceType;
            if (refType != null)
            {
                LocalType localType = LanguageManager.LocalTypeManager.GetLocalType(refType.ElementType);
                data.LocalType = localType;
            }
#endif
            return (data);
        }

        public StringData GenerateCodeArrayCreate(IExpression passedExpression, ParsingInfo info)
        {
            IArrayCreateExpression expression = (IArrayCreateExpression)passedExpression;
            StringData data = new StringData();

            StringData originalTypeName;
            IArrayType arrayType = expression.Type as IArrayType;
/*
 * Commented as it seems the code was not correct...
 * 
            if (arrayType != null)
            {
                originalTypeName = LanguageManager.ReferenceGenerator.GenerateCodeTypeWithPostfix(arrayType.ElementType, info);
            }
            else
 */
            {
                originalTypeName = LanguageManager.ReferenceGenerator.GenerateCodeTypeWithPostfix(expression.Type, info);
            }

            string typeName = "::System::Array__G< " + originalTypeName.Text + " >";

            string nextTempVariable = null;
            if (expression.Initializer != null)
            {
                StringData initializer = originalTypeName;
                nextTempVariable = CppUtil.GetNextTempVariable();
                initializer.AppendSameLine(" " + nextTempVariable + "[] = ");

                // Before we generate the initializer, we backup the level and set it to zero
                // The reason is that initializers can be called in two cases
                // In here, we always need fully qualified array (as we are inside a creation)
                // In the other case (jagged array), the level is already encapsulated

                // TODO: Refactor this a bit so we don't have to backup / restore the initializer
                int backupLevel = info.ArrayInitializerLevel;
                info.ArrayInitializerLevel = 0;
                initializer.AppendSameLine(GenerateCodeArrayInitializer(expression.Initializer, info, originalTypeName.LocalType));
                info.ArrayInitializerLevel = backupLevel;     // Restore the state

                info.AddToPreStatements(initializer);
            }

            // We actually have to create a temporary variable for array, and this for on case only :(
            // a new array can be passed by ref, this is actually incompatible with a function returning a pointer
            // Cannot do: Foo(&MyFunction());
            // We have to do instead:
            //  temp = MyFunction();
            //  Foo(&temp);

            string arrayCreateTempVariable = CppUtil.GetNextTempVariable();
            StringData arrayCreate = new StringData(typeName + " * ");
            arrayCreate.AppendSameLine(arrayCreateTempVariable + " = ");
            arrayCreate.AppendSameLine(typeName + "::__Create__(");
            bool prev = false;
            foreach (IExpression oneDimension in expression.Dimensions)
            {
                if (prev)
                {
                    arrayCreate.AppendSameLine(", ");
                }
                arrayCreate.AppendSameLine(GenerateCode(oneDimension, info));
                prev = true;
            }
            if (nextTempVariable != null)
            {
                arrayCreate.AppendSameLine(", ");
                arrayCreate.AppendSameLine(nextTempVariable);
            }
            arrayCreate.AppendSameLine(")");

            // Push the array creation before the current statement
            info.AddToPreStatements(arrayCreate);

            data = new StringData(arrayCreateTempVariable);
            data.LocalType = LanguageManager.LocalTypeManager.TypeArray;
            return (data);
        }

        public StringData GenerateCodeArrayIndexer(IExpression passedExpression, ParsingInfo info)
        {
            IArrayIndexerExpression expression = (IArrayIndexerExpression)passedExpression;

            info.PushLazyPropertyGet();     // We expect a get and not a set from the target
            StringData data = GenerateCode(expression.Target, info);
            info.PopLazyPropertyGet();      // get should not have been transformed in set_used

            string localTypeString = data.LocalType.ToString();
            const string TYPENAME = "typename ";
            if (localTypeString.StartsWith(TYPENAME))
            {
                localTypeString = localTypeString.Substring(TYPENAME.Length);
            }
            if (localTypeString.StartsWith("::System::Array__G<") == false)
            {
                // It s not an array, so handle it like a pointer...
                Debug.Assert(expression.Indices.Count == 1);
                Debug.Assert(localTypeString.EndsWith("*"));
                data.AppendSameLine("[");
                info.PushLazyPropertyGet();     // Make sure if there is a property that it is interpreted as get
                data.AppendSameLine(GenerateCode(expression.Indices[0], info));
                info.PopLazyPropertyGet();
                data.AppendSameLine("]");

                if (data.EmbeddedType is IPointerType)
                {
                    // If it is a pointer, then we have to skip the pointer...
                    // In C++, an array indexer de-referenced one pointer level
                    IPointerType pointerType = (IPointerType)(data.EmbeddedType);
                    // Remove one pointer level
                    data.EmbeddedType = pointerType.ElementType;
                }
                return (data);
            }

            data.AppendSameLine("->Item(");
            bool prev = false;
            info.PushLazyPropertyGet();     // Make sure if there is a property that it is interpreted as get
            foreach (IExpression eachIndice in expression.Indices)
            {
                if (prev)
                {
                    data.AppendSameLine(", ");
                }
                data.AppendSameLine(GenerateCode(eachIndice, info));
                prev = true;
            }
            info.PopLazyPropertyGet();
            data.AppendSameLine(")");

            // The return type is different, as we have to skip as many necessary [] (or *) in the type...
            // First skip the LocalType
            IType returnedType = data.EmbeddedType;     // On purpose use the embedded type
            if (returnedType is IReferenceType)
            {
                // Remove the reference level...
                returnedType = ((IReferenceType)returnedType).ElementType;
            }
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

        public StringData GenerateCodeArrayInitializer(IBlockExpression expression, ParsingInfo info, LocalType arrayType)
        {
            StringData data = new StringData();
            bool firstLevel = (info.ArrayInitializerLevel == 0);
            if (firstLevel)
            {
                data.AppendSameLine("{");
                ++data.Indentation;
            }
            ++info.ArrayInitializerLevel;
            StringBuilder currentLine = new StringBuilder();
            int numExpressions = expression.Expressions.Count;
            int i = 0;
            foreach (IExpression oneExpression in expression.Expressions)
            {
                currentLine.Append(" ");
                StringData value;
                if (oneExpression is IBlockExpression)
                {
                    value = GenerateCodeArrayInitializer((IBlockExpression)oneExpression, info, arrayType);
                }
                else
                {
                    value = GenerateCode(oneExpression, info);
                }
                value = LanguageManager.LocalTypeManager.DoesNeedCast(arrayType, value.LocalType, value);
                currentLine.Append(value);
                if (i != numExpressions - 1)
                {
                    // Append comma for every expression except the last
                    currentLine.Append(",");
                }

                if (currentLine.Length > 80)
                {
                    // Length is too long for a given line
                    data.Append(currentLine.ToString());
                    data.AppendSameLine("\n");
                    currentLine.Length = 0;
                }
                ++i;
            }

            if (currentLine.Length != 0)
            {
                // Remaining text to write...
                data.Append(currentLine.ToString());
                data.AppendSameLine("\n");
            }
            --info.ArrayInitializerLevel;
            if (firstLevel)
            {
                --data.Indentation;
                data.Append("}");
            }
            data.LocalType = LanguageManager.LocalTypeManager.TypeArray;
            return (data);
        }

        public StringData GenerateCodeAssign(IExpression passedExpression, ParsingInfo info)
        {
            IAssignExpression expression = (IAssignExpression)passedExpression;
            bool addAssignText = false;

            if (info.CurrentPropertyType == null)
            {
                info.CurrentPropertyType = new Stack<PropertyType>();
            }
            info.CurrentPropertyType.Push(PropertyType.ENABLE_SET);     // Assignment, we have to handle the target differently
                                                                        // We expect a set at this level
            StringData data = GenerateCode(expression.Target, info);
            PropertyType usedProperty = info.CurrentPropertyType.Pop();
            if (usedProperty == PropertyType.SET_USED)
            {
                // This is actually a set property...
                // The value is actually added as last parameter of the set property...

                // If that's an indexer, then we need to add the parameter separator
                if (data.Text.EndsWith("(") == false)
                {
                    // Not a standard property -> Then it's an indexer...
                    data.AppendSameLine(", ");
                }
            }
            else
            {
                bool done = false;
                if (expression.Expression is IObjectCreateExpression)
                {
                    ITypeInfo typeInfo = TypeInfoManager.GetTypeInfo(data.LocalType);
                    if ((typeInfo != null) && (typeInfo.Type == ObjectType.STRUCT))
                    {
                        // Tell that the parent layer is an assignment
                        info.InAssign = true;
                        done = true;    // Don't add the equal

                        if (expression.Target is IVariableDeclarationExpression)
                        {
                            // We know a bit more, we are actually declaring a struct variable and initializing
                            // This is another special sub-case to handle
                            info.InStructVariableDeclaration = true;
                        }
                    }
                }
                
                if (done == false)
                {
                    // Standard assignation...
                    addAssignText = true;
                }
            }

            info.CurrentPropertyType.Push(PropertyType.ENABLE_GET);
            StringData valueData = GenerateCode(expression.Expression, info);
            // After being used, pop it...
            info.CurrentPropertyType.Pop();

            // We have to handle one specific case here for nullable types (like int?)
            if (    (data.LocalType.FullName.StartsWith("::System::Nullable__G1<"))
                &&  (valueData.LocalType == LanguageManager.LocalTypeManager.TypeNull)  )
            {
                // This case is something like:
                // int? i = new int?();     (1)
                // or
                // i = null;                (2)
                // For (1), we actually have to call Nullable default construction
                // For (2), we have to call explicitly the constructor...

                // In this case, we don't expect set property to be used...
                Debug.Assert(usedProperty != PropertyType.SET_USED);

                if (expression.Target is IVariableDeclarationExpression)
                {
                    // We are in the case;
                    // int? i = new int?();     (1)
                    // Don't add "()" as the compiler would interpret it as a function declaration...
                    //data.AppendSameLine("()");
                }
                else
                {
                    // We are in the case:
                    // i = null;                (2)
                    // We have to explictly call the constructor
                    data.AppendSameLine(".__ctor__()");
                }
                return (data);
            }

            if (addAssignText)
            {
                data.AppendSameLine(" = ");
            }

            LocalType variableType = data.LocalType;
            LocalType valueType = valueData.LocalType;

            Debug.Assert(variableType != null);
            Debug.Assert(valueType != null);

            data.AppendSameLine(LanguageManager.LocalTypeManager.DoesNeedCast(variableType, valueType, valueData));

            if (usedProperty == PropertyType.SET_USED)
            {
                // Close the function call...
                data.AppendSameLine(")");
            }
            return (data);
        }

        public StringData GenerateCodeBaseReference(IExpression passedExpression, ParsingInfo info)
        {
            ITypeInfo typeInfo = TypeInfoManager.GetTypeInfo(info.DeclaringType as ITypeDeclaration);
            StringData data;

            if (typeInfo.BaseType != null)
            {
                data = new StringData(typeInfo.BaseType.FullName);
                data.EmbeddedType = info.BaseDeclaringType;             // On purpose, use the embedded type
            }
            else
            {
                LocalType typeObject = LanguageManager.LocalTypeManager.TypeObject;
                if (typeInfo.IsValueType == false)
                {
                    // System::Object is base for reference object
                    data = new StringData(typeObject.FullName);
                }
                else
                {
                    // For value type, we are using a fake structure
                    data = new StringData("::CrossNetRuntime::BaseStruct");
                }
                data.LocalType = typeObject;
            }
            if (info.WithinAnonymousMethod)
            {
                data.Replace("__base__");   // Replace the text so we keep the type...
            }
            return (data);
        }

        public StringData GenerateCodeBinary(IExpression passedExpression, ParsingInfo info)
        {
            StringData data = new StringData();
            IBinaryExpression expression = (IBinaryExpression)passedExpression;
            IExpression leftExpression = expression.Left;
            IExpression rightExpression = expression.Right;

            StringData leftData = GenerateCode(leftExpression, info);
            StringData rightData = GenerateCode(rightExpression, info);

            LocalType leftType = leftData.LocalType;
            LocalType rightType = rightData.LocalType;

            LocalType operationType;

            // Handle a couple of specific cases
            if (LanguageManager.LocalTypeManager.TypeChar.Same(leftType)
                || LanguageManager.LocalTypeManager.TypeChar.Same(rightType))
            {
                // Binary operations on char are actually returning an int!
                operationType = LanguageManager.LocalTypeManager.TypeInt32;
            }
            else
            {
                // By default, the operation type is the left type
                operationType = leftType;

                ITypeInfo leftTypeInfo = TypeInfoManager.GetTypeInfo(leftType);
                if ((leftTypeInfo != null) && (leftTypeInfo.Type == ObjectType.ENUM))
                {
                    // Binary operations on enums return an integer...
                    operationType = LanguageManager.LocalTypeManager.TypeInt32;
                }
                else
                {
                    ITypeInfo rightTypeInfo = TypeInfoManager.GetTypeInfo(rightType);
                    if ((rightTypeInfo != null) && (rightTypeInfo.Type == ObjectType.ENUM))
                    {
                        // Binary operations on enums return an integer...
                        operationType = LanguageManager.LocalTypeManager.TypeInt32;
                    }
                }
            }

            if (    (expression.Operator == BinaryOperator.Modulus)
                &&  (   LanguageManager.LocalTypeManager.TypeSingle.Same(rightType)
                    ||  LanguageManager.LocalTypeManager.TypeDouble.Same(rightType)    )   )
            {
                // That's a very specific case...
                // C# enables modulo with float or double as right parameter
                // This is not valid in C++, we are going to create a fake function call for that...

                data = new StringData("::CrossNetRuntime::__Math__::Modulo(");
                data.AppendSameLine(leftData);
                data.AppendSameLine(", ");
                data.AppendSameLine(rightData);
                data.AppendSameLine(")");
                data.LocalType = leftType;
                return (data);
            }

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

            LocalType[] resultTypeArray = new LocalType[]
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

            // This list MUST match BinaryOperator order
            // For performance reason, we might want to create this only one time...
            string[] operatorMethods = new string[]
                {
                    "op_Addition",
                    "op_Subtraction",
                    "op_Multiply",
                    "op_Division",
                    "op_Modulus",
                    "op_LeftShift",
                    "op_RightShift",
                    "op_Equality",
                    "op_Inequality",
                    "op_Equality",
                    "op_Inequality",
                    "op_BitwiseOr",
                    "op_BitwiseAnd",
                    "op_ExclusiveOr",
                    "op_Unknown",
                    "op_Unknown",
                    "op_LessThan",
                    "op_LessThanOrEqual",
                    "op_GreaterThan",
                    "op_GreaterThanOrEqual",
                };

            // Check that we are not 
            if ((expression.Operator == BinaryOperator.ValueEquality)
                || (expression.Operator == BinaryOperator.ValueInequality))
            {
                // String comparison
                if (LanguageManager.LocalTypeManager.TypeString.Same(leftType)
                    && LanguageManager.LocalTypeManager.TypeString.Same(rightType))
                {
                    string operatorName = operatorMethods[(int)expression.Operator];

                    // We are doing a string comparison (== or !=) use the operator instead of the pointer comparison
                    data = new StringData("::System::String::");
                    data.AppendSameLine(operatorName);
                    data.AppendSameLine("(");
                    data.AppendSameLine(leftData);
                    data.AppendSameLine(", ");
                    data.AppendSameLine(rightData);
                    data.AppendSameLine(")");
                    data.LocalType = LanguageManager.LocalTypeManager.TypeBool;
                    return (data);
                }
            }

            // Check that we are not in a case like "if (a == null)"
            if ((expression.Operator != BinaryOperator.IdentityEquality) && (expression.Operator != BinaryOperator.IdentityInequality))
            {
                string operatorName = operatorMethods[(int)expression.Operator];

                if (TypeInfoManager.HasImplicitOperator(leftType) == false)
                {
                    // Not a base type, try to find the corresponding operator...
                    ITypeInfo typeInfo = TypeInfoManager.GetTypeInfo(leftType);
                    if (typeInfo != null)
                    {
                        IParameterDeclarationCollection collection = Util.CreateParameterDeclaration(leftType, rightType);
                        // Try to find the method with the left type
                        IMethodDeclaration methodDeclaration = Util.FindMethod(typeInfo, operatorMethods[(int)expression.Operator], collection, null, true);
                        if (methodDeclaration != null)
                        {
                            data = new StringData(typeInfo.FullName);
                            data.AppendSameLine("::");
                            data.AppendSameLine(operatorName);
                            data.AppendSameLine("(");
                            data.AppendSameLine(leftData);
                            data.AppendSameLine(", ");
                            data.AppendSameLine(rightData);
                            data.AppendSameLine(")");
                            data.EmbeddedType = methodDeclaration.ReturnType.Type;      // We have to use embedded type here
                            // As ReturnType.Type returns an IType
                            return (data);
                        }
                    }
                    else
                    {
                        // This can happen with unsafe code... There is no operator in that case...
                    }
                }
                if (TypeInfoManager.HasImplicitOperator(rightType) == false)
                {
                    // It means that we have to use the corresponding operator
                    ITypeInfo typeInfo = TypeInfoManager.GetTypeInfo(rightType);
                    if (typeInfo != null)
                    {
                        IParameterDeclarationCollection collection = Util.CreateParameterDeclaration(leftType, rightType);
                        // Try to find the method with the right type
                        IMethodDeclaration methodDeclaration = Util.FindMethod(typeInfo, operatorMethods[(int)expression.Operator], collection, null, true);
                        if (methodDeclaration != null)
                        {
                            data = new StringData(typeInfo.FullName);
                            data.AppendSameLine("::");
                            data.AppendSameLine(operatorName);
                            data.AppendSameLine("(");
                            data.AppendSameLine(leftData);
                            data.AppendSameLine(", ");
                            data.AppendSameLine(rightData);
                            data.AppendSameLine(")");
                            data.EmbeddedType = methodDeclaration.ReturnType.Type;      // We have to use embedded type here
                            // As ReturnType.Type returns an IType
                            return (data);
                        }
                    }
                    else
                    {
                        // This can happen with unsafe code... There is no operator in that case...
                    }
                }
            }

            bool leftTypePointer = leftType.FullName.EndsWith("*");
            bool rightTypePointer = rightType.FullName.EndsWith("*");
            if (leftTypePointer && rightTypePointer && (expression.Operator == BinaryOperator.Subtract))
            {
                // That's a specific case for pointer arithmetic...
                // We have to cast the left and right member to integer
                // as .NET does divide by sizeof(MyStruct)
                // We also have to return the type as integer

                // As a side note, when doing pointer arithmetic in C++, the compiler does exactly the same thing
                // but transparently...
                leftData.PrefixSameLine("::CrossNetRuntime::PointerToInt32(");
                leftData.AppendSameLine(")");
                rightData.PrefixSameLine("::CrossNetRuntime::PointerToInt32(");
                rightData.AppendSameLine(")");
                data.LocalType = LanguageManager.LocalTypeManager.TypeInt32;
                info.UnsafeMethod = true;
            }
            else
            {
                data.LocalType = resultTypeArray[(int)expression.Operator];
            }

            // Otherwise we use the standard method...
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
            StringData data = new StringData("::CrossNetRuntime::IsCast<");
            StringData targetType = LanguageManager.ReferenceGenerator.GenerateCodeType(expression.TargetType, info);
            if (targetType.LocalType.IsPrimitiveType)
            {
                data.AppendSameLine("CrossNetRuntime::BaseTypeWrapper<" + targetType + " >");
            }
            else if (targetType.LocalType.FullName.StartsWith("::System::Array"))
            {
                string trim = targetType.Text.TrimEnd('*', ' ');
                data.AppendSameLine(trim);
            }
            else
            {
                data.AppendSameLine(targetType);
            }
            data.AppendSameLine(" >(");
            data.AppendSameLine(GenerateCode(expression.Expression, info));
            data.AppendSameLine(")");
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

            string cast = LanguageManager.LocalTypeManager.DoesNeedCast(typeToCast.LocalType, subExpression.LocalType);
            data = new StringData("(" + cast);

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

        public StringData GenerateCodeDelegateCreate(ITypeReference delegateTypeReference, string targetType, string target, string methodReference, ParsingInfo info)
        {
            StringData data = new StringData("new ");
            string delegateType = LanguageManager.ReferenceGenerator.GenerateCodeTypeReferenceAsString(delegateTypeReference, info);

            int delegateNumGenericArgs = delegateTypeReference.GenericArguments.Count;
            ITypeDeclaration ownerTypeDeclaration = delegateTypeReference.Owner as ITypeDeclaration;
            int ownerNumGenericArgs = 0;
            if (ownerTypeDeclaration != null)
            {
                ownerNumGenericArgs = ownerTypeDeclaration.GenericArguments.Count;
            }

            if (delegateNumGenericArgs != ownerNumGenericArgs)
            {
                // The number of gen args are not the same between the owner and the delegate, it means that the delegate is a generic itself
                // Look at the delegate creation in CppTypeGenerator.cs as it does a similar work
                // Because the delegate is generic, it means that it should finish by a template
                Debug.Assert(delegateType.EndsWith(">"));
            }

            delegateNumGenericArgs -= ownerNumGenericArgs;

            string appendedTemplate = "";
            if (delegateType.EndsWith(">"))
            {
                // It means that the delegate is actually a generic, we need to find the corresponding parameters...
                // As it could be recursive generics Function< MyType<int > >, we need to do a bit of parsing in this case...

                int index = delegateType.Length - 1;
                int level = 0;          // We could slightly optimize here as we know that the last char is '>'
                // But for simplicity and maintainability, we are actually not taking advantage of this

                // So the last char will directly increase the level, and we will continue backward until we reach a level of zero
                while (index >= 0)
                {
                    switch (delegateType[index])
                    {
                        case '>':
                            ++level;
                            break;

                        case '<':
                            --level;
                            break;

                        default:
                            // Do nothing...
                            break;
                    }
                    if (level == 0)
                    {
                        // We reached the beginning of the last generic group, we are done...
                        break;
                    }
                    --index;    // Look for previous char
                    Debug.Assert(level > 0);
                }

                appendedTemplate = ", " + delegateType.Substring(index + 1, (delegateType.Length - 1) - (index + 1));
                delegateType = delegateType.Substring(0, index);
            }

            string newType = delegateType + "__FUNCTOR__";

            newType += "<" + targetType + appendedTemplate + " >";
            data.AppendSameLine(newType);
            data.AppendSameLine("(");
            data.AppendSameLine(target);
            data.AppendSameLine(", ");
            data.AppendSameLine(methodReference);
            data.AppendSameLine(")");

            data.LocalType = LanguageManager.LocalTypeManager.TypeDelegate;
            return (data);
        }

        // Refactor this method to use the method above...
        public StringData GenerateCodeDelegateCreate(IExpression passedExpression, ParsingInfo info)
        {
            IDelegateCreateExpression expression = (IDelegateCreateExpression)passedExpression;
            StringData data = new StringData("new ");
            string delegateType = LanguageManager.ReferenceGenerator.GenerateCodeTypeReferenceAsString(expression.DelegateType, info);

            int delegateNumGenericArgs = expression.DelegateType.GenericArguments.Count;
            ITypeDeclaration ownerTypeDeclaration = expression.DelegateType.Owner as ITypeDeclaration;
            int ownerNumGenericArgs = 0;
            if (ownerTypeDeclaration != null)
            {
                ownerNumGenericArgs = ownerTypeDeclaration.GenericArguments.Count;
            }

            if (delegateNumGenericArgs != ownerNumGenericArgs)
            {
                // The number of gen args are not the same between the owner and the delegate, it means that the delegate is a generic itself
                // Look at the delegate creation in CppTypeGenerator.cs as it does a similar work
                // Because the delegate is generic, it means that it should finish by a template
                Debug.Assert(delegateType.EndsWith(">"));
            }

            delegateNumGenericArgs -= ownerNumGenericArgs;

            string appendedTemplate = "";
            if (delegateType.EndsWith(">"))
            {
                // It means that the delegate is actually a generic, we need to find the corresponding parameters...
                // As it could be recursive generics Function< MyType<int > >, we need to do a bit of parsing in this case...

                int index = delegateType.Length - 1;
                int level = 0;          // We could slightly optimize here as we know that the last char is '>'
                                        // But for simplicity and maintainability, we are actually not taking advantage of this

                // So the last char will directly increase the level, and we will continue backward until we reach a level of zero
                while (index >= 0)
                {
                    switch (delegateType[index])
                    {
                        case '>':
                            ++level;
                            break;

                        case '<':
                            --level;
                            break;

                        default:
                            // Do nothing...
                            break;
                    }
                    if (level == 0)
                    {
                        // We reached the beginning of the last generic group, we are done...
                        break;
                    }
                    --index;    // Look for previous char
                    Debug.Assert(level > 0);
                }

                appendedTemplate = ", " + delegateType.Substring(index + 1 , (delegateType.Length - 1) - (index + 1));
                delegateType = delegateType.Substring(0, index);
            }

            string newType = delegateType + "__FUNCTOR__";

            info.PushLazyPropertyGet();     // We expect a get and not a set from the target
            StringData target = GenerateCode(expression.Target, info);
            info.PopLazyPropertyGet();      // get should not have been transformed in set_used

            string targetType = target.LocalType.ToString();

            newType += "<" + targetType + appendedTemplate + " >";
            data.AppendSameLine(newType);
            data.AppendSameLine("(");

            if ((expression.Target is ITypeReferenceExpression) || (expression.Target is IBaseReferenceExpression))
            {
                // Static method...
                data.AppendSameLine("&");
                data.AppendSameLine(target);        // Target is the scope
            }
            else
            {
                // Non static method...
                data.AppendSameLine(target);        // Target is the instance
                data.AppendSameLine(", &");
                data.AppendSameLine(targetType);    // And use the type of the instance
            }
            data.AppendSameLine("::");
            data.AppendSameLine(LanguageManager.ReferenceGenerator.GenerateCodeMethodReference(expression.Method, info));
            data.AppendSameLine(")");

            data.LocalType = LanguageManager.LocalTypeManager.TypeDelegate;
            return (data);
        }

        public StringData GenerateCodeDelegateInvoke(IExpression passedExpression, ParsingInfo info)
        {
            StringData data = new StringData("[==3==]");
            data.LocalType = LanguageManager.LocalTypeManager.TypeUnknown;
            return (data);
        }

        public StringData GenerateCodeEventReference(IExpression passedExpression, ParsingInfo info)
        {
            IEventReferenceExpression expression = (IEventReferenceExpression)passedExpression;

            info.PushLazyPropertyGet();     // We expect a get and not a set from the target
            StringData data = GenerateCode(expression.Target, info);
            info.PopLazyPropertyGet();      // get should not have been transformed in set_used

            data.AppendSameLine("->");
            StringData eventRef = LanguageManager.ReferenceGenerator.GenerateCodeEventReference(expression.Event);
            data.AppendSameLine(eventRef);
            data.LocalType = eventRef.LocalType;
            return (data);
        }

        public StringData GenerateCodeFieldOf(IExpression passedExpression, ParsingInfo info)
        {
            StringData data = new StringData("[==4==]");
            data.LocalType = LanguageManager.LocalTypeManager.TypeUnknown;
            return (data);
        }

        public StringData GenerateCodeFieldReference(IExpression passedExpression, ParsingInfo info)
        {
            IFieldReferenceExpression expression = (IFieldReferenceExpression)passedExpression;

            info.PushLazyPropertyGet();     // We expect a get and not a set from the target
            StringData data = GenerateCode(expression.Target, info);
            info.PopLazyPropertyGet();      // get should not have been transformed in set_used

            ITypeInfo typeInfo = data.LocalType.GetTypeInfo();

            if ((expression.Target is ITypeReferenceExpression) || (expression.Target is IBaseReferenceExpression))
            {
                // If the target was referencing a type, it means that it is a static method
                // In C# :: . and -> are the same, but unfortunately not in C++ :(

                if (data.LocalType.IsPrimitiveType
                    && (typeInfo.Type != ObjectType.ENUM) )
                {
                    // That's a special case, it is a base type, meaning that he type doesn't exist as struct
                    // And as such we need to encapsulate it...

                    // An example is this:
                    //  int i = int.MinValue;

                    // It is going to be generated as:
                    //  System::Int32 i = CrossNetRuntime::BaseTypeWrapper<System::Int32>::MinValue;

                    StringData tempTarget;
                    tempTarget = new StringData("CrossNetRuntime::BaseTypeWrapper<");
                    tempTarget.AppendSameLine(data);
                    tempTarget.AppendSameLine(" >");
                    data = tempTarget;
                }
                data.AppendSameLine("::");
            }
            else
            {
                if ((typeInfo != null) && (typeInfo.IsValueType) /*&& (expression.Target is IThisReferenceExpression == false)*/)
                {
                    // The corresponding test might not work with unsafe code
                    data.AppendSameLine(".");
                }
                else
                {
                    data.AppendSameLine("->");
                }
            }
            StringData fieldData = LanguageManager.ReferenceGenerator.GenerateCodeFieldReference(expression.Field, info);
            data.AppendSameLine(fieldData);

            if ((typeInfo != null) && (typeInfo.Type == ObjectType.ENUM) && (info.InCase == false))
            {
                // For enums, we make the casts explicit, so there is no ambiguity between two enums
                // Remember that enum values are actually simple integers...
                // We also make sure that we don't do that when listing switch case constants
                // (as it doesn't have the issue and connot do the cast anyway)

                // Before doing so, let's compare the enum type and the enum value
                // If they are the same, we have to patch the enum value as it is not valid in C++ (it is in C#)
                if (typeInfo.Name == expression.Field.Name)
                {
                    // TODO: Move this inside GenerateCodeFieldReference?
                    data.Replace(typeInfo.FullName + "::__" + typeInfo.Name + "__");
                }

                // In some case (like parameter of a struct constructor is an enum), the expression might be misunderstood by the compiler
                // And interpreted as a local function definition instead when used for variablt declaration.
                // Using (EnumType)(EnumValue) instead of EnumType(EnumValue) should resolve that
                // Look at TestRegressionEnum.cs for more details
                string text = "(" + typeInfo.FullName + ")(" + data.Text + ")";
                data.Replace(text);
            }

            data.LocalType = fieldData.LocalType;
            return (data);
        }

        public StringData GenerateCodeGenericDefault(IExpression passedExpression, ParsingInfo info)
        {
            // Used for generics, default(T) returns null if T is a class, return 0 if T is a struct
            IGenericDefaultExpression expression = (IGenericDefaultExpression)passedExpression;
            StringData data = new StringData("__DEFAULT__(");
            StringData genericClass = LanguageManager.ReferenceGenerator.GenerateCodeType(expression.GenericArgument, info);
            data.AppendSameLine(genericClass);
            data.AppendSameLine(")");
            data.LocalType = LanguageManager.LocalTypeManager.TypeDefault;
            return (data);
        }

        // Code duplicated with NormalizeStringExpression for performance reason...
        private static string NormalizeCharExpression(char text)
        {
            ushort value = (ushort)text;
            if ((value >= 0x0080) || (value < 0x0020))
            {
                string str = "'\\x";
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
                        return ("'\\x0000'");

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
                if ((value >= 0x0080) || (value < 0x0020))
                {
                    sb.Append("\\x");
                    // Something interestesting with hex escape sequence in strings
                    // The size is not pre-defined
                    // So if you have, 0-9 a-f characters after the hex sequence, the compiler will still read them and interpret them wrongly
                    // Usually reporting that the number is too wide
                    // We solve this issue by breaking the string sequence
                    sb.Append(value.ToString("x4"));
                    sb.Append("\" L\"");
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
                            // Something interestesting with hex escape sequence in strings
                            // The size is not pre-defined
                            // So if you have, 0-9 a-f characters after the hex sequence, the compiler will still read them and interpret them wrongly
                            // Usually reporting that the number is too wide
                            // We solve this issue by breaking the string sequence
                            sb.Append("\\x0000\" L\"");
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
                text = "NULL";
                type = LanguageManager.LocalTypeManager.TypeNull;
            }
            else
            {
                // For optimization reason, should we use an hashtable here?
                Type valueType = value.GetType();
                if (valueType == typeof(String))
                {
                    // We need to add the quote for the strings
#if false   //  Deactivated as we are doing string pooling
                    string generatedString = NormalizeStringExpression((String)value);
                    text = "::System::String::__Create__(L" + generatedString + ")";      // L for 16 bits char...
#else
                    text = StringPool.CreateString((String)value);
#endif
                    type = LanguageManager.LocalTypeManager.TypeString;
                }
                else if (valueType == typeof(char))
                {
                    text = "L" + NormalizeCharExpression((Char)value);
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
                    float f = (float)value;
                    if (float.IsNaN(f))
                    {
                        text = "::CrossNetRuntime::BaseTypeWrapper<System::Single>::NaN";
                    }
                    else if (float.IsNegativeInfinity(f))
                    {
                        text = "::CrossNetRuntime::BaseTypeWrapper<System::Single>::NegativeInfinity";
                    }
                    else if (float.IsPositiveInfinity(f))
                    {
                        text = "::CrossNetRuntime::BaseTypeWrapper<System::Single>::PositiveInfinity";
                    }
                    else
                    {
                        if (f >= 0.0f)
                        {
                            text = "";
                        }
                        else
                        {
                            text = "-";
                        }
                        f = Math.Abs(f);
                        if (f == float.MaxValue)
                        {
                            // There is a precision difference between C# generated float and the C++ compiler...
                            // We just patch it and use the C++ constant instead...
                            text += "::CrossNetRuntime::BaseTypeWrapper<System::Single>::MaxValue";
                        }
                        else if (f == float.MinValue)
                        {
                            // There is a precision difference between C# generated float and the C++ compiler...
                            // We just patch it and use the C++ constant instead...
                            text += "::CrossNetRuntime::BaseTypeWrapper<System::Single>::MinValue";
                        }
                        else if (f == float.Epsilon)
                        {
                            // There is a precision difference between C# generated float and the C++ compiler...
                            // We just patch it and use the C++ constant instead...
                            text += "::CrossNetRuntime::BaseTypeWrapper<System::Single>::Epsilon";
                        }
                        else
                        {
                            text = String.Format("{0:R}", value);
                            if (text.IndexOfAny(mFloatNumbers) == -1)
                            {
                                text += ".";    // For the floating part
                            }
                            text += "f";        // Specify a float instead of double
                        }
                    }
                    type = LanguageManager.LocalTypeManager.TypeSingle;
                }
                else if (valueType == typeof(double))
                {
                    double f = (double)value;
                    if (double.IsNaN(f))
                    {
                        text = "::CrossNetRuntime::BaseTypeWrapper<System::Double>::NaN";
                    }
                    else if (double.IsNegativeInfinity(f))
                    {
                        text = "::CrossNetRuntime::BaseTypeWrapper<System::Double>::NegativeInfinity";
                    }
                    else if (double.IsPositiveInfinity(f))
                    {
                        text = "::CrossNetRuntime::BaseTypeWrapper<System::Double>::PositiveInfinity";
                    }
                    else
                    {
                        if (f >= 0.0)
                        {
                            text = "";
                        }
                        else
                        {
                            text = "-";
                        }
                        f = Math.Abs(f);
                        if (f == double.MaxValue)
                        {
                            // There is a precision difference between C# generated double and the C++ compiler...
                            // We just patch it and use the C++ constant instead...
                            text += "::CrossNetRuntime::BaseTypeWrapper<System::Double>::MaxValue";
                        }
                        else if (f == double.MinValue)
                        {
                            // There is a precision difference between C# generated double and the C++ compiler...
                            // We just patch it and use the C++ constant instead...
                            text += "::CrossNetRuntime::BaseTypeWrapper<System::Double>::MinValue";
                        }
                        else if (f == double.Epsilon)
                        {
                            // There is a precision difference between C# generated double and the C++ compiler...
                            // We just patch it and use the C++ constant instead...
                            text += "::CrossNetRuntime::BaseTypeWrapper<System::Double>::Epsilon";
                        }
                        else
                        {
                            text = String.Format("{0:R}", value);
                            if (text.IndexOfAny(mFloatNumbers) == -1)
                            {
                                text += ".";    // For the floating part
                            }
                        }
                    }
                    type = LanguageManager.LocalTypeManager.TypeDouble;
                }
                else if (valueType == typeof(int))
                {
                    int i = (int)value;
                    if (i == int.MinValue)
                    {
                        // There is an interesting issue with min int...
                        // This will generate -2147483648
                        // But because that's a two stage process (on VC++ at least)
                        // (first read 2147483648, promote it to unsigned int, then try to negate it unsuccessfully
                        // - because it's unsigned...) - so there will be a warning...
                        // let's fix this and use the constant instead
                        text = "::CrossNetRuntime::BaseTypeWrapper<System::Int32>::MinValue";
                    }
                    else if (i == int.MaxValue)
                    {
                        text = "::CrossNetRuntime::BaseTypeWrapper<System::Int32>::MaxValue";
                    }
                    else
                    {
                        text = value.ToString();
                    }
                    type = LanguageManager.LocalTypeManager.TypeInt32;
                }
                else if (valueType == typeof(byte))
                {
                    byte b = (byte)value;
                    if (b == byte.MinValue)
                    {
                        text = "::CrossNetRuntime::BaseTypeWrapper<System::Byte>::MinValue";
                    }
                    else if (b == byte.MaxValue)
                    {
                        text = "::CrossNetRuntime::BaseTypeWrapper<System::Byte>::MaxValue";
                    }
                    else
                    {
                        text = value.ToString();
                    }
                    type = LanguageManager.LocalTypeManager.TypeByte;
                }
                else if (valueType == typeof(sbyte))
                {
                    sbyte b = (sbyte)value;
                    if (b == sbyte.MinValue)
                    {
                        text = "::CrossNetRuntime::BaseTypeWrapper<System::SByte>::MinValue";
                    }
                    else if (b == sbyte.MaxValue)
                    {
                        text = "::CrossNetRuntime::BaseTypeWrapper<System::SByte>::MaxValue";
                    }
                    else
                    {
                        text = value.ToString();
                    }
                    type = LanguageManager.LocalTypeManager.TypeSByte;
                }
                else if (valueType == typeof(ulong))
                {
                    ulong u = (ulong)value;
                    if (u == ulong.MinValue)
                    {
                        text = "::CrossNetRuntime::BaseTypeWrapper<System::UInt64>::MinValue";
                    }
                    else if (u == ulong.MaxValue)
                    {
                        text = "::CrossNetRuntime::BaseTypeWrapper<System::UInt64>::MaxValue";
                    }
                    else
                    {
                        text = value.ToString() + "ULL";
                    }
                    type = LanguageManager.LocalTypeManager.TypeUInt64;
                }
                else if (valueType == typeof(long))
                {
                    long l = (long)value;
                    if (l == long.MinValue)
                    {
                        text = "::CrossNetRuntime::BaseTypeWrapper<System::Int64>::MinValue";
                    }
                    else if (l == long.MaxValue)
                    {
                        text = "::CrossNetRuntime::BaseTypeWrapper<System::Int64>::MaxValue";
                    }
                    else
                    {
                        text = value.ToString() + "LL";
                    }
                    type = LanguageManager.LocalTypeManager.TypeInt64;
                }
                else if (valueType == typeof(short))
                {
                    short s = (short)value;
                    if (s == short.MinValue)
                    {
                        text = "::CrossNetRuntime::BaseTypeWrapper<System::Int16>::MinValue";
                    }
                    else if (s == short.MaxValue)
                    {
                        text = "::CrossNetRuntime::BaseTypeWrapper<System::Int16>::MaxValue";
                    }
                    else
                    {
                        text = value.ToString();
                    }
                    type = LanguageManager.LocalTypeManager.TypeInt16;
                }
                else if (valueType == typeof(ushort))
                {
                    ushort s = (ushort)value;
                    if (s == ushort.MinValue)
                    {
                        text = "::CrossNetRuntime::BaseTypeWrapper<System::UInt16>::MinValue";
                    }
                    else if (s == ushort.MaxValue)
                    {
                        text = "::CrossNetRuntime::BaseTypeWrapper<System::UInt16>::MaxValue";
                    }
                    else
                    {
                        text = value.ToString();
                    }
                    type = LanguageManager.LocalTypeManager.TypeUInt16;
                }
                else if (valueType == typeof(uint))
                {
                    uint s = (uint)value;
                    if (s == uint.MinValue)
                    {
                        text = "::CrossNetRuntime::BaseTypeWrapper<System::UInt32>::MinValue";
                    }
                    else if (s == uint.MaxValue)
                    {
                        text = "::CrossNetRuntime::BaseTypeWrapper<System::UInt32>::MaxValue";
                    }
                    else
                    {
                        text = value.ToString() + "U";
                    }
                    type = LanguageManager.LocalTypeManager.TypeUInt32;
                }
                else if (valueType == typeof(decimal))
                {
                    // decimal don't exist in C++, for the moment use it like a double...
                    decimal d = (decimal)value;
                    if (d == decimal.MinValue)
                    {
                        text = "::CrossNetRuntime::BaseTypeWrapper<System::Decimal>::MinValue";
                    }
                    else if (d == decimal.MaxValue)
                    {
                        text = "::CrossNetRuntime::BaseTypeWrapper<System::Decimal>::MaxValue";
                    }
                    else if (d == decimal.Zero)
                    {
                        text = "::CrossNetRuntime::BaseTypeWrapper<System::Decimal>::Zero";
                    }
                    else if (d == decimal.One)
                    {
                        text = "::CrossNetRuntime::BaseTypeWrapper<System::Decimal>::One";
                    }
                    else if (d == decimal.MinusOne)
                    {
                        text = "::CrossNetRuntime::BaseTypeWrapper<System::Decimal>::MinusOne";
                    }
                    else
                    {
                        text = String.Format("{0:R}", (double)d);
                        if (text.IndexOfAny(mFloatNumbers) == -1)
                        {
                            text += ".";
                        }
                    }
                    type = LanguageManager.LocalTypeManager.TypeDecimal;
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
            StringData data;
            bool alreadyOneParameter = true;

            bool inInterfaceCall = info.PopInterfaceCall();
            if (inInterfaceCall)
            {
                // We are in interface call, that means the opening ( has been pushed already
                // as well as the first instance parameter
                data = new StringData();
                alreadyOneParameter = true;
            }
            else
            {
                // We are in the standard case (no interface call)
                // We have the full list of parameters here
                data = new StringData("(");
                alreadyOneParameter = false;
            }

//            if (info.MethodType == MethodType.NORMAL)
//  Same: Handle the operator explictly...
            if (true)
            {
                int index = 0;
                // Backup the methodReference as it could be overwritten by sub-expressions.
                IMethodReference methodReference = info.MethodReference;
                foreach (IExpression expression in arguments)
                {
                    if (alreadyOneParameter)
                    {
                        data.AppendSameLine(", ");
                    }
                    alreadyOneParameter = true; // For the next iteration

                    StringData argument = GenerateCode(expression, info);

                    // Before we append, we need to test some implicit conversions...
                    // Int32 to Char for example...
                    // This code is costly, might want to optimize it or better find when it needs to be applied

                    // The same kind of check is done in CppReference.GenerateCodeMethodReference for unsafe
                    // TODO: Change the code so we do both checks...
                    // Try to optimize it more (maybe use an hashtable of method reference?)

                    Debug.Assert(index < methodReference.Parameters.Count);
                    if (index < methodReference.Parameters.Count)
                    {
                        // In some case we can have arguments > parameters
                        // I don't understand yet why it can happen... Probably with named parameters...
                        // TODO: Fix this...

                        IParameterDeclaration parameterDeclaration = methodReference.Parameters[index];

                        if (argument.Text == "")
                        {
                            // This is a rare case... It happens only with anonymous method expression
                            // (At least for the moment...)
#warning Fix this anonymous method expression...
                            Debug.Assert(expression is IAnonymousMethodExpression);
                        }
                        else
                        {
                            AppendParameter(data, parameterDeclaration, argument, info);
                        }
                    }
                    else
                    {   Debug.Fail("Should not be here!");
                        data.AppendSameLine(argument);
                    }

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
                        data.AppendSameLine("[==7==]");
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

        private void AppendParameter(StringData data, IParameterDeclaration parameterDeclaration, StringData argument, ParsingInfo info)
        {
            if (parameterDeclaration.ParameterType == null)
            {
#warning ReflectorBUG - This case can happen with Reflector when using __arglist
                data.AppendSameLine(argument);
                return;
            }
            StringData parameter = LanguageManager.ReferenceGenerator.GenerateCodeType(parameterDeclaration.ParameterType, info);
            LocalType destinationType = parameter.LocalType;
            LocalType sourceType = argument.LocalType;

            Debug.Assert(destinationType != null);
            Debug.Assert(sourceType != null);

            string cast = LanguageManager.LocalTypeManager.DoesNeedCast(destinationType, sourceType);

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
#if REF_OUT || true
                            expectedText = "& ";
#else
                            expectedText = "";
#endif
                        }
                        else
                        {
                            // If no "&", then we don't push explicitly the ref
                            expectedText = "";
                        }
                        incorrectText = "out ";     // In C++, no diff between ref and out
                                                    // use "out" to disable the test
                    }
                    else
                    {
                        if (alsoRef)
                        {
#if REF_OUT || true
                            expectedText = "& ";
#else
                            expectedText = "";
#endif
                            incorrectText = "out "; // In C++, no diff between ref and out
                                                    // use "out" to disable the test
                        }
                    }
                }
                else if (outAttribute)
                {
                    // out only, it's an out and not a ref
                    if (alsoRef)
                    {
#if REF_OUT || true
                        expectedText = "& ";
#else
                        expectedText = "";
#endif
                    }
                    else
                    {
                        expectedText = "";
                    }
                    incorrectText = "ref ";         // In C++, no diff between ref and out
                    // use "out" to disable the test
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

            {
                // Other cases
                // TODO: Add pointer detection?
                if (parameterDeclaration.ParameterType is IPointerType)
                {
                    // That's a pointer, use the wrapper to disambiguate with ref / out
                    // (as they are also handled with pointer).
                    if (argument.Text == "NULL")
                    {
                        // Specific case for NULL, C++ compiler interprets it as int 0 :(
                        // So put it explicitly
                        argument.Replace("::CrossNetRuntime::CreatePointerWrapper((" + destinationType.FullName + ")NULL)");
                    }
                    else
                    {
                        argument.Replace("::CrossNetRuntime::CreatePointerWrapper(" + argument.Text + ")");
                    }
                    // If we use the PointerWrapper, we don't have to cast...
                    cast = "";
                }
            }

            if (cast != "")
            {
                data.AppendSameLine(cast);
                data.AppendSameLine("(");
                data.AppendSameLine(argument);
                data.AppendSameLine(")");
            }
            else
            {
                data.AppendSameLine(argument);
            }
        }

        public StringData GenerateCodeMethodOf(IExpression passedExpression, ParsingInfo info)
        {
            StringData data = new StringData("[==8==]");
            data.LocalType = LanguageManager.LocalTypeManager.TypeUnknown;
            return (data);
        }

        public StringData GenerateCodeMethodReference(IExpression passedExpression, ParsingInfo info)
        {
            IMethodReferenceExpression expression = (IMethodReferenceExpression)passedExpression;
            StringData data;
            info.PushLazyPropertyGet();     // We expect a get and not a set from the target
            StringData target = GenerateCode(expression.Target, info);
            info.PopLazyPropertyGet();      // get should not have been transformed in set_used

            bool useGenWrapper = false;
            if (    (target.LocalType.EmbeddedType is IGenericArgument)
                ||  target.LocalType.IsPrimitiveType )
            {
                // If the target is a template argument, we won't be able to have always the correct code
                // for example assuming that we type this code:
                //  string Foo<T>(T o)
                //  {
                //      return (o.ToString());
                //  }
                //  With C++ templates, the generated code will have to be different depending if T is actually a base type, a struct or a class
                // But unfortunately, we can't determine that when generating the template.
                // So we have to generate a code that will work in any of these cases.

                // This is the same case for target returning a base type, like this code:
                // int i = 5;
                // System.Console.WriteLine(i.ToString());

                // To solve this we are using a wrapper that will work in any of the configuration...

                // We are actually doing the gen wrapper after the cast if needed below
                // If we do the wrapping here, we could potentially cast the result then call the function
                // At least by doing it later, we cast to the proper type first, then wrap it to do the function call
                useGenWrapper = true;
            }

            StringData methodReference = LanguageManager.ReferenceGenerator.GenerateCodeMethodReference(expression.Method, info);

            // By default, we assume that we are not in interface call
            bool inInterfaceCall = false;

// In C#, we handle differently the operators...
//          if (info.MethodType == MethodType.NORMAL)
// Not in C++, we handle them as method call
            if (true)
            {
                // Not an operator, append the method name with the target...

                IType methodDeclaringType = expression.Method.DeclaringType;

                bool standardCall = (target.EmbeddedType.CompareTo(methodDeclaringType) == 0);
                if ((expression.Target is IThisReferenceExpression) || (expression.Target is IBaseReferenceExpression))
                {
                    // If it is "base", mostly from the base constructor do a standard call
                    // As we don't want a cast because the types are not different
                    standardCall = true;
                }

                if (standardCall)
                {
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

#warning Revisit if this is really necessary...
#if DISABLED    // It caused some issues with the foreach / generic unit-tests...
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
#endif

                // TODO: Improve here and restrict to real use case issues...
                // For example implicit conversion from char to int32
                // So we are not poluting all the other cases...

                if (standardCall)
                {
                    ITypeInfo targetTypeInfo = TypeInfoManager.GetTypeInfo(target.LocalType);

                    bool done = false;
                    // Type of the target exactly the same as the type of the method
                    if (expression.Target is IAssignExpression)
                    {
                        // Special case if that's an assign expression...
                        // We have to encapsulate it with parenthesis, otherwise the meaning won't be the same...
                        data = new StringData("(");
                        data.AppendSameLine(target);
                        data.AppendSameLine(")");
                        data.LocalType = target.LocalType;
                    }
                    else
                    {
                        if ((targetTypeInfo != null) && (targetTypeInfo.Type == ObjectType.INTERFACE))
                        {
                            // We are doing an interface call

                            StringData tempData = new StringData(targetTypeInfo.GetInstanceText(info));
                            tempData.AppendSameLine(" ");
                            string tempVariable = CppUtil.GetNextTempVariable();
                            tempData.AppendSameLine(tempVariable);
                            tempData.AppendSameLine(" = ");
                            target = AddWrapper(target, expression, useGenWrapper); // Here we have to wrap the target (as it could be a template)
                            StringData result = LanguageManager.LocalTypeManager.DoesNeedCast(targetTypeInfo.LocalType, target.LocalType, target);
                            tempData.AppendSameLine(result);
                            info.AddToPreStatements(tempData);

                            // Look up the real type for the interface (as the method might be part of a base interface).
                            ITypeInfo typeInfo = TypeInfoManager.GetTypeInfo(expression.Method.DeclaringType);
                            data = new StringData(CppUtil.InterfaceCallEx(tempVariable, typeInfo.FullName, methodReference.Text));
                            inInterfaceCall = true;
                            done = true;
                        }
                        else
                        {
                            data = target;
                        }
                    }

                    if (done == false)
                    {
                        data.Replace(AddWrapper(data, expression, useGenWrapper).Text);

                        if ((expression.Target is ITypeReferenceExpression) || (expression.Target is IBaseReferenceExpression))
                        {
                            // If the target was referencing a type, it means that it is a static method
                            // In C# :: . and -> are the same, but unfortunately not in C++ :(

                            data.AppendSameLine("::");
                        }
                        else
                        {
                            ITypeInfo typeInfo = TypeInfoManager.GetTypeInfo(data.LocalType);
                            if ((typeInfo != null) && (typeInfo.IsValueType) /* && (expression.Target is IThisReferenceExpression == false) */ )
                            {
                                // The corresponding test might not work with unsafe code
                                data.AppendSameLine(".");
                            }
                            else
                            {
                                data.AppendSameLine("->");
                            }
                        }

                        // TODO:    Add a potential optimization for sealed classes
                        //          If the class is sealed, nothing prevent us to fully qualify the method (with the corresponding type)
                        //          In some cases, we can actually do a static call instead of a virtual call...
                        data.AppendSameLine(methodReference);
                    }
                }
                else
                {
                    // Not exactly the same, this can be due because we are calling a base type method
                    // or because there was an implicit cast...
                    // For the moment, make it explicit...

                    LocalType localType = LanguageManager.LocalTypeManager.GetLocalType(methodDeclaringType);
                    ITypeInfo methodTypeInfo = TypeInfoManager.GetTypeInfo(localType);
                    if ((methodTypeInfo != null) && (methodTypeInfo.Type == ObjectType.INTERFACE))
                    {
                        // We are actually doing an interface call

                        StringData tempData = new StringData(methodTypeInfo.GetInstanceText(info));
                        tempData.AppendSameLine(" ");
                        string tempVariable = CppUtil.GetNextTempVariable();
                        tempData.AppendSameLine(tempVariable);
                        tempData.AppendSameLine(" = ");
                        target = AddWrapper(target, expression, useGenWrapper);
                        StringData result = LanguageManager.LocalTypeManager.DoesNeedCast(methodTypeInfo.LocalType, target.LocalType, target);
                        tempData.AppendSameLine(result);
                        info.AddToPreStatements(tempData);

                        data = new StringData(CppUtil.InterfaceCallEx(tempVariable, methodTypeInfo.FullName, methodReference.Text));
                        inInterfaceCall = true;
                    }
                    else
                    {
                        if ((expression.Target is ITypeReferenceExpression) || (expression.Target is IBaseReferenceExpression))
                        {
                            // If the target was referencing a type, it means that it is a static method
                            // In C# :: . and -> are the same, but unfortunately not in C++ :(
                            data = AddWrapper(target, expression, useGenWrapper);
                            data.AppendSameLine("::");
                        }
                        else
                        {
                            // Standard C++ call (we cast to the expected type just so we can have the correct method - like implicit cast from char to int).
                            data = new StringData("(");
                            StringData castedVersion = LanguageManager.LocalTypeManager.DoesNeedCast(localType, target.LocalType, target);
                            castedVersion = AddWrapper(castedVersion, expression, useGenWrapper);
                            data.AppendSameLine(castedVersion);
                            data.AppendSameLine(")->");
                        }
                        data.AppendSameLine(methodReference);
                    }
                }
            }
            else
            {
                // Do nothing, everything will be done by invoke arguments later...
                data = new StringData();
            }
            IGenericArgument genArg = expression.Method.ReturnType.Type as IGenericArgument;
            if (genArg != null) // && (genArg.Position < expression.Method.GenericArguments.Count))
            {
                // If that's a generic argument, try to get the real type...
                data.EmbeddedType = genArg.Owner.GenericArguments[genArg.Position];
            }
            else
            {
                data.EmbeddedType = expression.Method.ReturnType.Type;      // EmbeddedType used on purpose
            }
            info.PushInterfaceCall(inInterfaceCall);
            return (data);
        }

        private StringData AddWrapper(StringData target, IMethodReferenceExpression expression, bool wrap)
        {
            if (wrap == false)
            {
                return (target);
            }
            StringData tempTarget;
            if (expression.Target is ITypeReferenceExpression)
            {
                // Actually the target is a type reference, so it means that's a static call...
                // We need the type wrapper in this case...
                tempTarget = new StringData("CrossNetRuntime::BaseTypeWrapper<" + target + " >");
            }
            else
            {
                tempTarget = new StringData("CrossNetRuntime::GenWrapperConvert(" + target + ")");
            }
            tempTarget.LocalType = target.LocalType;
            return (tempTarget);
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
            INullCoalescingExpression expression = (INullCoalescingExpression)passedExpression;
            StringData condition = GenerateCode(expression.Condition, info);
            StringData data = new StringData("(" + condition.Text + " != NULL)");
            data.AppendSameLine(" ? ");
            data.AppendSameLine(condition);
            data.AppendSameLine(" : ");
            StringData otherExpression = GenerateCode(expression.Expression, info);
            data.AppendSameLine(otherExpression);
            data.LocalType = otherExpression.LocalType;
            return (data);
        }

        public StringData GenerateCodeObjectCreate(IExpression passedExpression, ParsingInfo info)
        {
            IObjectCreateExpression expression = (IObjectCreateExpression)passedExpression;
            StringData data;
            bool close = true;  // By default, there will be nested parenthesis
            
            ITypeInfo typeInfo = TypeInfoManager.GetTypeInfo(expression.Type);
            if (typeInfo != null)
            {
                if (typeInfo.IsValueType)
                {
                    if (info.InAssign)
                    {
                        info.InAssign = false;  // Remove the flags...

                        // Parent expression is an assignment...
                        // We'll just pass the parameters so we'll call the standard constructor

                        if (info.InStructVariableDeclaration)
                        {
                            info.InStructVariableDeclaration = false;

                            // The user wrote in C# something like this:
                            //  MyStruct s = new MyStruct(optParameters);
                            // And we need to generate something like this:
                            //  MyStruct s(optParameters);
                            data = new StringData();

                            if (expression.Arguments.Count == 0)
                            {
                                // There is no parameter, return directly
                                // Otherwise the compiler might interpret it as an incorrect function declaration
                                data.LocalType = typeInfo.LocalType;
                                data = GenerateInitializer(data, expression.Initializer, typeInfo, info);
                                return (data);
                            }
                        }
                        else
                        {
                            // The user wrote in C# something like this:
                            //  s = new MyStruct(optParameters); with s of the type MyStruct
                            // In that case, we need to generate something like this:
                            //  s.__ctor__(optParameters);
                            data = new StringData(".__ctor__");
                        }

                        close = false;          // No nested parenthesis in this case
                    }
                    else
                    {
                        // Otherwise for struct construction, we'll use a temporary construction
                        data = new StringData("(" + typeInfo.FullName);
                    }
                }
                else
                {
                    // For class type, we call the create method
                    data = new StringData("(" + typeInfo.GetPrefixedFullName("__Create__"));
                }
                data.LocalType = typeInfo.LocalType;
            }
            else if (expression.Type is IGenericArgument)
            {
                // That's a generic parameter...
                // Reflector can returns new T() - with T a template parameter
                // When it sometime recognizes the sequence:
                //  temp = default(T);
                //  temp = (temp == null) ? Activator.CreateInstance<T>() : default(T);
                // It doesn't always recognize it though...
                data = new StringData("__PARAMETERLESS_NEW__(");
                StringData declaringType = LanguageManager.ReferenceGenerator.GenerateCodeType(expression.Type, info);
                data.AppendSameLine(declaringType.Text);
                data.AppendSameLine(")");
                data.LocalType = declaringType.LocalType;
                // In that case, there is no other parameters to add... So don't add invoked parameters...

                data = GenerateInitializer(data, expression.Initializer, typeInfo, info);
                return (data);
            }
            else
            {
                // For everything else, we call new
                // The type is certainly unsafe...
                data = new StringData("(new ");
                StringData declaringType = LanguageManager.ReferenceGenerator.GenerateCodeType(expression.Type, info);
                data.AppendSameLine(declaringType.Text);
                data.LocalType = declaringType.LocalType;
            }

            info.MethodReference = expression.Constructor;
            info.PushInterfaceCall(false);      // Construction is not an interface call
            StringData arguments = GenerateCodeMethodInvokeArguments(expression.Arguments, info);
            data.AppendSameLine(arguments);
            if (close)
            {
                data.AppendSameLine(")");
            }

            data = GenerateInitializer(data, expression.Initializer, typeInfo, info);
            return (data);
        }

        StringData GenerateInitializer(StringData data, IBlockExpression initializer, ITypeInfo typeInfo, ParsingInfo info)
        {
            if (initializer == null)
            {
                return (data);
            }
            if (initializer.Expressions.Count == 0)
            {
                return (data);
            }

            // There is an initializer, we need to flatten the statement as Reflector puts everything in one line
            // Like:     return new Matrix0 { M11 = m1.M11 + m2.M11, M12 = m1.M12 + m2.M12, ... M43 = m1.M43 + m2.M43, M44 = m1.M44 + m2.M44 };

            // So we are using the pre-statements to handle this gracefully
            StringData newLine = new StringData();
            string tempVariable = CppUtil.GetNextTempVariable();
            if (typeInfo != null)
            {
                newLine.AppendSameLine(typeInfo.FullName + " " + typeInfo.GetInstancePostFix() + " " + tempVariable + " = ");
                newLine.AppendSameLine(data);
            }
            else
            {
                // Unknown type, use the LocalType hoping it will be enough
                newLine.AppendSameLine(data.LocalType.FullName + " " + tempVariable + " = ");
                newLine.AppendSameLine(data);
            }
            info.AddToPreStatements(newLine);

            // Then let's handle each expression in the initializer
            foreach (IExpression subExpression in initializer.Expressions)
            {
//                IAssignExpression assignExpression = subExpression as IAssignExpression;
                if (subExpression is IMemberInitializerExpression)
                {
/*
                    IExpression target = assignExpression.Target;
                    IExpression value = assignExpression.Expression;

                    StringData targetData = GenerateCode(target, info);
                    StringData valueData = GenerateCode(value, info);

                    valueData = LanguageManager.LocalTypeManager.DoesNeedCast(targetData.LocalType, valueData.LocalType, valueData);

                    StringData oneLine = new StringData(tempVariable);
                    oneLine.AppendSameLine("->");
                    oneLine.AppendSameLine(targetData);
                    oneLine.AppendSameLine(" = ");
                    oneLine.AppendSameLine(valueData);
                    oneLine.AppendSameLine(";\n");
                    info.AddToPreStatements(oneLine);
 */

                    StringData memberInitialization = GenerateCodeMemberInitializer(subExpression, info);

                    StringData oneLine = new StringData(tempVariable);
                    oneLine.AppendSameLine("->");
                    oneLine.AppendSameLine(memberInitialization);
                    info.AddToPreStatements(oneLine);
                }
                else
                {
                    StringData value = GenerateCode(subExpression, info);
                    StringData oneLine = new StringData(tempVariable);
                    oneLine.AppendSameLine("->Add(");
                    oneLine.AppendSameLine(value);
                    oneLine.AppendSameLine(")");
                    info.AddToPreStatements(oneLine);
                }
            }

            StringData result = new StringData(tempVariable);
            result.LocalType = data.LocalType;
            return (result);
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

#warning Remove commented code and put a correct comment
//            info.PushLazyPropertyGet();     // We expect a get and not a set from the target
            StringData data = GenerateCode(expression.Target, info);
//            info.PopLazyPropertyGet();      // get should not have been transformed in set_used

            data.AppendSameLine("(");

            bool first = true;
            string indexerTarget = info.PopPropertyIndexerTarget();
            if (indexerTarget != null)
            {
                data.AppendSameLine(indexerTarget);
                first = false;
            }

            int index = 0;
            IParameterDeclarationCollection parameterCollection = expression.Target.Property.Parameters;
            foreach (IExpression eachIndice in expression.Indices)
            {
                if (first == false)
                {
                    data.AppendSameLine(", ");
                }
                first = false;

                StringData argument = GenerateCode(eachIndice, info);
                IParameterDeclaration parameterDeclaration = parameterCollection[index];
                // By appending the parameter, this will solve cast of the parameter as well...
                AppendParameter(data, parameterDeclaration, argument, info);
                ++index;
            }

            Stack<PropertyType> currentPropertyType = info.CurrentPropertyType;
            if (    (currentPropertyType == null)
                ||  (currentPropertyType.Count == 0)
                ||  (info.CurrentPropertyType.Peek() != PropertyType.SET_USED)
                )
            {
                // If it's not a set property, we can close now the parameters
                // For set, if it has been consumed, we close it at the assign level
                data.AppendSameLine(")");
            }
            return (data);
        }

        public StringData GenerateCodePropertyReference(IExpression passedExpression, ParsingInfo info)
        {
            IPropertyReferenceExpression expression = (IPropertyReferenceExpression)passedExpression;
            StringData data;

            info.PushLazyPropertyGet();     // We expect a get and not a set from the target
            StringData target = GenerateCode(expression.Target, info);
            info.PopLazyPropertyGet();      // get should not have been transformed in set_used

            StringData originalTarget = target;
            StringData property = LanguageManager.ReferenceGenerator.GenerateCodePropertyReference(expression.Property);
            // By testing expression.Property.Parameters.Count, we might be able to get rid of hard-coded "Item" and "Chars"

            ITypeInfo typeInfo = TypeInfoManager.GetTypeInfo(expression.Property.DeclaringType);
            if ((typeInfo != null) && (typeInfo.Type == ObjectType.INTERFACE))
            {
                // In that case, we do a property call on an interface!
                // This is a special behavior...

                // First copy the target on a temp variable (we don't want the target to be evaluated several times)
                StringData tempData = new StringData(typeInfo.GetInstanceText(info));
                tempData.AppendSameLine(" ");
                string tempVariable = CppUtil.GetNextTempVariable();
                tempData.AppendSameLine(tempVariable);
                tempData.AppendSameLine(" = ");
                tempData.AppendSameLine(LanguageManager.LocalTypeManager.DoesNeedCast(typeInfo.LocalType, target.LocalType, target));
                info.AddToPreStatements(tempData);

                target = new StringData(CppUtil.InterfaceCall(tempVariable, typeInfo.FullName));
                originalTarget = new StringData(tempVariable);
            }

            bool getProperty = true;    // By default, it's a get property...
            Stack<PropertyType> currentPropertyType = info.CurrentPropertyType;
            if (
                    (currentPropertyType != null)
                &&  (currentPropertyType.Count != 0)
                &&  (currentPropertyType.Peek() == PropertyType.ENABLE_SET)
                )
            {
                // It  should be actually a set property
                getProperty = false;
            }

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

            // 2007/05/13: Even if that's an assignment, do the correct set_Item / get_Item...
            // That may be wrong but try to fix the code like:
            //  return ((x2 = x)->set_Item(100, (x2->get_Item(100) + 1));

            {
                if (expression.Property.Parameters.Count != 0)
                {
                    // Special case for indexer...

                    if ((expression.Target is ITypeReferenceExpression) || (expression.Target is IBaseReferenceExpression))
                    {
                        data.AppendSameLine("::");
                    }
                    else
                    {
                        if ((typeInfo != null) && (typeInfo.IsValueType) /* && (expression.Target is IThisReferenceExpression == false) */)
                        {
                            // The corresponding test might not work with unsafe code
                            data.AppendSameLine(".");
                        }
                        else
                        {
                            data.AppendSameLine("->");
                        }
                    }

                    if (getProperty)
                    {
                        data.AppendSameLine("get_Item");
                    }
                    else
                    {
                        data.AppendSameLine("set_Item");

                        // Here change the current property type state, says that is has been consummed
                        // This way the caller can act accordingly (and transform set_Item(12) = 10 to set_Item(12, 10)...)
                        info.CurrentPropertyType.Pop();
                        info.CurrentPropertyType.Push(PropertyType.SET_USED);
                    }
                }
            }

            if (expression.Property.Parameters.Count != 0)
            {
                // There are some parameters for the property, it's an indexer then...
                // Only the target is needed

                if ((typeInfo != null) && (typeInfo.Type == ObjectType.INTERFACE))
                {
                    info.PushPropertyIndexerTarget(originalTarget.Text);
                }
                else
                {
                    info.PushPropertyIndexerTarget(null);
                }
            }
            else
            {
                if ((expression.Target is ITypeReferenceExpression) || (expression.Target is IBaseReferenceExpression))
                {
                    data.AppendSameLine("::");
                }
                else
                {
                    if ((typeInfo != null) && (typeInfo.IsValueType) /* && (expression.Target is IThisReferenceExpression == false) */)
                    {
                        // The corresponding test might not work with unsafe code
                        data.AppendSameLine(".");
                    }
                    else
                    {
                        data.AppendSameLine("->");
                    }
                }

                if (getProperty)
                {
                    data.AppendSameLine("get_");
                }
                else
                {
                    data.AppendSameLine("set_");

                    // Here change the current property type state, says that is has been consummed
                    // This way the caller can act accordingly (and transform set_Item(12) = 10 to set_Item(12, 10)...)
                    info.CurrentPropertyType.Pop();
                    info.CurrentPropertyType.Push(PropertyType.SET_USED);
                }
                data.AppendSameLine(property);
                data.AppendSameLine("(");

                if ((typeInfo != null) && (typeInfo.Type == ObjectType.INTERFACE))
                {
                    data.AppendSameLine(originalTarget);
                }

                if (getProperty)
                {
                    // If it's not a set property, we can close now the parameters
                    // Otherwise we close it at the assign level
                    data.AppendSameLine(")");
                }
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
            StringData data = new StringData("[==10==]");
            data.LocalType = LanguageManager.LocalTypeManager.TypeUnknown;
            return (data);
        }

        public StringData GenerateCodeStackAllocate(IExpression passedExpression, ParsingInfo info)
        {
            IStackAllocateExpression expression = (IStackAllocateExpression)passedExpression;

            // Because of the stackalloc incorrectly parsed code,
            //      System.Int32 * numPtr1 = stackalloc System.Int32[100];
            //  It generates:
            //      System.Int32 * numPtr1 = (System.Int32 *)stackalloc System.Byte[4 * 100];

            // In C++, this is less an issue, here is what we have to generate:
            //      __stackalloc__(4 * 100);

            // Update June 15th 2007:
            // It seems reflector fixed this and now the parsed code is correct
            // So we have to multiply back the size of the type now...

            StringData data = new StringData("__stackalloc__(");
            StringData size = GenerateCode(expression.Expression, info);
            data.AppendSameLine(size);
            data.AppendSameLine(" * sizeof(");
            ITypeInfo typeInfo = TypeInfoManager.GetTypeInfo(expression.Type);
            data.AppendSameLine(typeInfo.FullName);
            data.AppendSameLine("))");
            info.UnsafeMethod = true;

// Commented to let the cast do its job...
// It seems that we should be able to get rid of all this specific code for C# staticcast,
// The rules are not the same between C++ and C#...
//            info.InStackAlloc = false;
            data.LocalType = LanguageManager.LocalTypeManager.TypePointer;
            return (data);
        }

        public StringData GenerateCodeThisReference(IExpression passedExpression, ParsingInfo info)
        {
            StringData data;
            ITypeInfo typeInfo = TypeInfoManager.GetTypeInfo(info.DeclaringType);
            if (typeInfo.Type == ObjectType.STRUCT)
            {
                // If that's a struct, we actually produce *this (for coherency reason with all structure access
                // That way we won't have to do specific case for this on structure everywhere else
                data = new StringData("(*this)");
            }
            else
            {
                if (info.WithinAnonymousMethod)
                {
                    data = new StringData("__this__");
                }
                else
                {
                    data = new StringData("this");
                }
            }
            data.EmbeddedType = info.DeclaringType;     // EmbeddedType used on purpose
            return (data);
        }

        public StringData GenerateCodeTryCast(IExpression passedExpression, ParsingInfo info)
        {
            ITryCastExpression expression = (ITryCastExpression)passedExpression;
            StringData data = new StringData("::CrossNetRuntime::AsCast<");
            StringData targetType = LanguageManager.ReferenceGenerator.GenerateCodeType(expression.TargetType, info);
            // There is no need to test primitive type here as value types can't be used with as (null is meaningless for value type)
            if (targetType.LocalType.FullName.StartsWith("::System::Array"))
            {
                string trim = targetType.Text.TrimEnd('*', ' ');
                data.AppendSameLine(trim);
            }
            else
            {
                data.AppendSameLine(targetType);
            }
            data.AppendSameLine(" >(");
            data.AppendSameLine(GenerateCode(expression.Expression, info));
            data.AppendSameLine(")");
            data.LocalType = targetType.LocalType;
            return (data);
        }

        public StringData GenerateCodeTypedReferenceCreate(IExpression passedExpression, ParsingInfo info)
        {
            StringData data = new StringData("[==2==]");
            data.LocalType = LanguageManager.LocalTypeManager.TypeUnknown;
            return (data);
        }

        public StringData GenerateCodeTypeOf(IExpression passedExpression, ParsingInfo info)
        {
            ITypeOfExpression expression = (ITypeOfExpression)passedExpression;
            StringData typeData = LanguageManager.ReferenceGenerator.GenerateCodeType(expression.Type, info);
            ITypeInfo typeInfo = typeData.LocalType.GetTypeInfo();
            StringData data = new StringData("CN_TYPEOF(");
            if (typeData.Text == "::System::Void")
            {
                typeData.Replace("::System::__Void__"); // void is not a real type so we have to use a fake type instead
            }
            string typeText = typeData.Text;
            typeText = typeText.TrimEnd(' ', '*');  // Remove all pointer information to try to have the non-pointer type
                                                    // So we can append ::__GetInterfaceMap__() to the passed type in the macro
                                                    // Note that this actually fixes the issue for the array ;)
            data.Append(typeText);
            data.Append(")");
            data.LocalType = LanguageManager.LocalTypeManager.TypeOfType;
            return (data);
        }

        public StringData GenerateCodeTypeOfTypedReference(IExpression passedExpression, ParsingInfo info)
        {
            StringData data = new StringData("[==1==]");
            data.LocalType = LanguageManager.LocalTypeManager.TypeUnknown;
            return (data);
        }

        public StringData GenerateCodeTypeReference(IExpression passedExpression, ParsingInfo info)
        {
            ITypeReferenceExpression expression = (ITypeReferenceExpression)passedExpression;
            return (LanguageManager.ReferenceGenerator.GenerateCodeTypeReference(expression.Type, info));
        }

        public StringData GenerateCodeUnary(IExpression passedExpression, ParsingInfo info)
        {
            IUnaryExpression expression = (IUnaryExpression)passedExpression;

            IExpression subExpression = expression.Expression;

            StringData data = null;
            StringData subData = GenerateCode(subExpression, info);

            if (TypeInfoManager.HasImplicitOperator(subData.LocalType) == false)
            {
                // It means that we have to use the corresponding operator

                // This list MUST match UnaryOperator order
                string[] operatorMethods = new string[]
                {
                    "op_UnaryNegation",
                    "op_LogicalNot",
                    "op_OnesComplement",
                    "op_Increment",
                    "op_Decrement",
                    "op_Increment",
                    "op_Decrement",
                };

                ITypeInfo typeInfo = TypeInfoManager.GetTypeInfo(subData.LocalType);
                if (typeInfo != null)
                {
                    data = new StringData(typeInfo.FullName);
                    data.AppendSameLine("<::>");
                    data.AppendSameLine(operatorMethods[(int)expression.Operator]);
                    data.AppendSameLine("(");
                    data.AppendSameLine(subData);
                    data.AppendSameLine(")");
                    data.LocalType = subData.LocalType;
                    return (data);
                }
            }

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

            // TODO: Optimize by not using String.Format
            //  Also the return value might be a bool when using ! operator
            string text = String.Format(operators[(int)expression.Operator], data.Text);
            StringData data2 = new StringData(text);
            data2.LocalType = subData.LocalType;
            return (data2);
        }

        public StringData GenerateCodeValueOfTypedReference(IExpression passedExpression, ParsingInfo info)
        {
            StringData data = new StringData("[==11==]");
            data.LocalType = LanguageManager.LocalTypeManager.TypeUnknown;
            return (data);
        }

        public StringData GenerateCodeVariableDeclaration(IExpression passedExpression, ParsingInfo info)
        {
            IVariableDeclarationExpression expression = (IVariableDeclarationExpression)passedExpression;
            StringData data = LanguageManager.ReferenceGenerator.GenerateCodeVariableDeclaration(expression.Variable, info);

            if (info.Variables != null)
            {
                AnonymousVariable var;
                if (info.Variables.TryGetValue(expression.Variable.Name, out var))
                {
                    // This variable is used within the anonymous method, we have to use the shared variable instead
                    // In that case, we do not declare it just initialize it...
                    if (var.Declared == Declared.Outside)
                    {
                        data.Replace(var.NewName);
                    }
                }
            }

            return (data);
        }

        public StringData GenerateCodeVariableReference(IExpression passedExpression, ParsingInfo info)
        {
            IVariableReferenceExpression expression = (IVariableReferenceExpression)passedExpression;
            StringData data = LanguageManager.ReferenceGenerator.GenerateCodeVariableReference(expression.Variable);

/*
            if (info.WithinAnonymousMethod)
            {
                // Should not be necessary but at the same time this should help detect more issues...
                data.PrefixSameLine("this->");
            }
            else if (info.Variables != null)
            {
                AnonymousVariable var;
                if (info.Variables.TryGetValue(expression.Variable.Resolve().Name, out var))
                {
                    // This variable is used within the anonymous method, we have to use the shared variable instead
                    if (var.Declared == Declared.Outside)
                    {
                        data.Replace(var.NewName);
                    }
                }
            }
 */

            if (info.Variables != null)
            {
                AnonymousVariable var;
                if (info.Variables.TryGetValue(expression.Variable.Resolve().Name, out var))
                {
                    if (var.Declared == Declared.Outside)
                    {
                        if (info.WithinAnonymousMethod)
                        {
                            // Should not be necessary but at the same time this should help detect more issues...
                            data.PrefixSameLine("this->");
                        }
                        else
                        {
                            // This parameter is used within the anonymous method, we have to use the shared variable instead
                            data.Replace(var.NewName);
                        }
                    }
                }
            }

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
            IAddressReferenceExpression expression = (IAddressReferenceExpression)passedExpression;
            ParseExpression(expression.Expression, info);
        }

        public void ParseAnonymousMethod(IExpression passedExpression, ParsingInfo info)
        {
            IAnonymousMethodExpression expression = (IAnonymousMethodExpression)passedExpression;

            info.AtLeastOneAnonymousMethod = true;
            info.WithinAnonymousMethod = true;
            foreach (IParameterDeclaration parameter in expression.Parameters)
            {
                info.DeclareParameter(parameter.Name, parameter);
            }
            LanguageManager.StatementGenerator.ParseBlock(expression.Body, info);
            info.WithinAnonymousMethod = false;
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
            if (expression == null)
            {
                return;
            }
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
            IEventReferenceExpression expression = (IEventReferenceExpression)passedExpression;
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
            IVariableReferenceExpression expression = (IVariableReferenceExpression)passedExpression;
            IVariableDeclaration varDeclaration = expression.Variable.Resolve();
            info.UseVariable(varDeclaration.Name, varDeclaration);
        }

        private char[] mFloatNumbers = { '.', 'e', 'E' };
        private IDictionary<Type, GenerateExpressionDelegate> sGenerateDelegates = new Dictionary<Type, GenerateExpressionDelegate>();
        private IDictionary<Type, ParseExpressionDelegate> sParseDelegates = new Dictionary<Type, ParseExpressionDelegate>();
    }
}

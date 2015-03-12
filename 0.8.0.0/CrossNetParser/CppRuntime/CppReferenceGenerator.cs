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
using Reflector.CodeModel.Memory;

using CrossNet.Common;
using CrossNet.Interfaces;
using CrossNet.Net;

namespace CrossNet.CppRuntime
{
    public class CppReferenceGenerator : IReferenceGenerator
    {
        public StringData GenerateCodeMethodReference(IMethodReference methodReference, ParsingInfo info)
        {
            string methodName;
            if (methodReference.Name == ".ctor")
            {
                // If constructor, use the type name
                IType declaringType = methodReference.DeclaringType;
//                methodName = methodReference.DeclaringType.ToString();
                methodName = GenerateCodeTypeAsString(methodReference.DeclaringType, info);
            }
            else
            {
                // otherwise can use the method name
                methodName = methodReference.Name;
                string outputText;

                if (methodReference.GenericArguments.Count != 0)
                {
                    if (info.CurrentGenericArgument == null)
                    {
                        info.CurrentGenericArgument = new Stack<IType>();
                    }

// 2007/08/03:  Commented as when generics are stacked, the count can actually be non null
//                    Debug.Assert(info.CurrentGenericArgument.Count == 0);     // Nothing should be enqueued already

                    // We push in reverse order, so the parameters are poped in the right order within the same level
                    // And because it is a stack, between levels the order is set properly
                    int count = methodReference.GenericArguments.Count;
                    while (count > 0)
                    {
                        --count;
                        IType genericArgument = methodReference.GenericArguments[count];
                        info.CurrentGenericArgument.Push(genericArgument);
                    }
                }

#if true
                int numGenericArgumentsForMethod = methodReference.GenericArguments.Count;
                ITypeDeclaration declaratingType = methodReference.DeclaringType as ITypeDeclaration;
                int numGenericArgumentsForOwner = 0;
                if (declaratingType != null)
                {
                    numGenericArgumentsForOwner = declaratingType.GenericArguments.Count;
                }

                AddGenericArguments(numGenericArgumentsForMethod - numGenericArgumentsForOwner, false, info, out outputText);
#else
                AddGenericArguments(methodReference.GenericArguments, info, out outputText);
#endif
                methodName += outputText;
            }
            methodName = LanguageManager.NameFixup.UnmangleMethodName(methodName, false);
            methodName = LanguageManager.NameFixup.ConvertMethodName(methodName, methodReference, out info.MethodType);
            info.MethodName = methodName;
            info.MethodReference = methodReference;
            StringData data = new StringData(methodName);

            // Just before we leave the method, scan all the parameters and return value for the method and see if there is an unsafe one
            // Here we don't care about the resulting unusedData, we just want to see "info" updated
/*
 * Not needed for C++ code...
 * 
            if (info.UnsafeMethod == false)
            {
                // To speed things a little bit, we are doing this check only if the method is not unsafe already
                // Use a different ParsingInfo so we are not adding two times the variable...
                ParsingInfo localInfo = new ParsingInfo(methodReference.DeclaringType);
                StringData unusedData = GenerateCodeType(methodReference.ReturnType.Type, localInfo);
                unusedData = GenerateCodeParameterDeclarationCollection(methodReference.Parameters, localInfo);
                info.UnsafeMethod = localInfo.UnsafeMethod;
            }
 */
            return (data);
        }

        public StringData GenerateCodeTypeReference(ITypeReference typeReference, ParsingInfo info)
        {
            string text = GenerateCodeTypeReferenceAsString(typeReference, info);
            StringData data = new StringData(text);
            data.LocalType = LanguageManager.LocalTypeManager.GetLocalType(typeReference, text);
            return (data);
        }

        public String GenerateCodeTypeReferenceAsString(ITypeReference typeReference, ParsingInfo info)
        {
            return (GenerateCodeTypeReferenceAsString(typeReference, info, false));
        }

        private String GenerateCodeTypeReferenceAsString(ITypeReference typeReference, ParsingInfo info, bool nestedGeneric)
        {
            StringBuilder data = new StringBuilder();
            bool alreadyTemplated = false;

            object owner = typeReference.Owner;

            if (typeReference.GenericArguments.Count != 0)
            {
                if (info.CurrentGenericArgument == null)
                {
                    info.CurrentGenericArgument = new Stack<IType>();
                }

// 2007/08/03:  Commented as when generics are stacked, the count can actually be non null
//                Debug.Assert(info.CurrentGenericArgument.Count == 0);     // Nothing should be enqueued already

                // We push in reverse order, so the parameters are poped in the right order within the same level
                // And because it is a stack, between levels the order is set properly
                int count = typeReference.GenericArguments.Count;
                while (count > 0)
                {
                    --count;
                    IType genericArgument = typeReference.GenericArguments[count];
                    info.CurrentGenericArgument.Push(genericArgument);
                }
            }

            Stack<ITypeReference> owners = null;

            while (owner is ITypeReference)
            {
                ITypeReference ownerType = (ITypeReference)owner;
                if (owners == null)
                {
                    owners = new Stack<ITypeReference>();
                }
                owners.Push(ownerType);
                owner = ownerType.Owner;

            }

            if (owners != null)
            {
                while (owners.Count != 0)
                {
                    ITypeReference typeReferenceOwner = owners.Pop();

                    if (typeReferenceOwner.Namespace != "")
                    {
                        string nameSpace = typeReferenceOwner.Namespace;
                        nameSpace = nameSpace.Replace(".", "::");
                        if (nameSpace.StartsWith("<") == false)
                        {
                            // Avoid incorrect namespace (when starting with "<")
                            nameSpace = LanguageManager.NameFixup.GetSafeName(nameSpace);
                            data.Append(nameSpace);
                            data.Append("::");
                        }
                    }

                    string outText = "";
                    alreadyTemplated = false;

                    // Changed behavior here...
                    // We don't add the generic parameters...
                    // We just append __G if there are some generic parameters...

/*un-nest*/         alreadyTemplated = true;        // To not append generic parameters...

                    bool textAdded = AddTypeName(typeReferenceOwner, info, ref alreadyTemplated, ref outText);
                    if (textAdded)
                    {
                        data.Append(outText);

/*start-un-nest*/
                        if (typeReferenceOwner.GenericArguments.Count != 0)
                        {
                            // The type was temapleted, add the generic 
                            data.Append("__G" + typeReferenceOwner.GenericArguments.Count.ToString());
                        }
/*end-un-nest*/

                        data.Append("__");          // A nested class is actually named with the name of the owner + "__" + nested name
                                                    // And at the same namespace level than the owner
                    }
                }
            }

            if (typeReference.Namespace != "")
            {
                string nameSpace = typeReference.Namespace;
                nameSpace = nameSpace.Replace(".", "::");
                if (nameSpace.StartsWith("<") == false)
                {
                    // Avoid incorrect namespace (when starting with "<")
                    nameSpace = LanguageManager.NameFixup.GetSafeFullName(nameSpace);
                    data.Append(nameSpace);
                    data.Append("::");
                }
            }
            alreadyTemplated = false;
            string value = "::" + data.ToString();
            AddTypeName(typeReference, info, ref alreadyTemplated, ref value);
            value = LanguageManager.NameFixup.GetSafeName(value);
            return (value);
        }

        private bool AddTypeName(ITypeReference typeReference, ParsingInfo info, ref bool alreadyTemplated, ref string outText)
        {
            // Unmangle just the name and not previous namespace (which could contain template code).
            string text = LanguageManager.NameFixup.UnmangleName(typeReference.Name);
            outText += text;

            if (alreadyTemplated)
            {
                return (text != "");
            }

            string outputText;

            int numGenericArgumentsForType = typeReference.GenericArguments.Count;
            int numGenericArgumentsForOwner = 0;
/*
 * Commented for un-nesting...
 * 
            ITypeDeclaration declaratingType = typeReference.Owner as ITypeDeclaration;
            if (declaratingType != null)
            {
                numGenericArgumentsForOwner = declaratingType.GenericArguments.Count;
            }
 */

            // Even if we pass all the generic parameters anyway, we have to know if the nested type is a generic itself
            // As the owner could have two classes named Foo, one generic and one not
            // The only way to differentiate them correctly is to use the generic markup (__G)
            bool disableGenericMarkup = false;
            ITypeDeclaration declaratingType = typeReference.Owner as ITypeDeclaration;
            if (declaratingType != null)
            {
                // If the owner has the same number of generic parameters, then the nested type is not a generic
                // So we disable its marking...
                disableGenericMarkup = ((numGenericArgumentsForType - declaratingType.GenericArguments.Count) == 0);
            }

            alreadyTemplated = AddGenericArguments(numGenericArgumentsForType - numGenericArgumentsForOwner, disableGenericMarkup, info, out outputText);

            if (outputText != "")
            {
                outText += outputText;
                return (true);
            }
            return (text != "");
        }
#if false
        private bool AddGenericArguments(ITypeCollection genericArguments, ParsingInfo info, out string data)
        {
            data = "";
            if (info.CurrentGenericArgument == null)
            {
                Debug.Assert(genericArguments.Count == 0);
                return (false);
            }
            if (genericArguments.Count == 0)
            {
                return (false);
            }
            if (info.CurrentGenericArgument.Count == 0)
            {
                return (false);
            }

            // Post-fix the template name with __G so there is no collision between the standard typename and the generic version
            data = "__G<";
            bool first = true;
            while (info.CurrentGenericArgument.Count != 0)
            {
                if (first == false)
                {
                    data += ", ";
                }
                first = false;
                IType genericArgument = info.CurrentGenericArgument.Dequeue();
                data += GenerateStringType(genericArgument, info, true);            // true to say that we are within a generic!

                ITypeInfo typeInfo = TypeInfoManager.GetTypeInfo(genericArgument);
                if (typeInfo != null)
                {
                    data += typeInfo.GetInstancePostFix();
                }
            }
            data += " >";
            return (true);
        }
#endif
        private bool AddGenericArguments(int numArguments, bool disableGenericMarkup, ParsingInfo info, out string data)
        {
            data = "";
            if (info.CurrentGenericArgument == null)
            {
                Debug.Assert(numArguments == 0);
                return (false);
            }
            if (numArguments == 0)
            {
                return (false);
            }
            if (info.CurrentGenericArgument.Count == 0)
            {
                Debug.Assert(numArguments == 0);
                return (false);
            }

            // Post-fix the template name with __G so there is no collision between the standard typename and the generic version
/*
 * Revisit this code... Remove it? As well as the corresponding parameters?
 * 
            if (disableGenericMarkup)
            {
                // Generic markup is disabled, even if it is a template, this is not considered as a generic type
                data = "<";
            }
            else
 */
            {
                // Generic markup not disabled, so we mark the type as generic
                data = "__G" + numArguments.ToString() + "<";
            }

// 2007/08/03: Removed the max as it seems to generate an issue with Rotor's Genmeth4 unit-test
//             The generics are cumul;ated, and as such they collide during the parsing...
//            int templateParameters = Math.Max(numArguments, info.CurrentGenericArgument.Count);
            int templateParameters = numArguments;
            if (templateParameters > 1)
            {
                // If there is at least 2 template parameters, encapsulate them by a __W*__ macro
                // The reason is that the type can be used within a macro, and the comma separator will mess up the macro's parameter
                data += "__W" + templateParameters.ToString() + "__(";
            }

            bool first = true;
            while (info.CurrentGenericArgument.Count != 0)
            {
                if (first == false)
                {
                    data += ", ";
                }
                first = false;
                IType genericArgument = info.CurrentGenericArgument.Pop();
                data += GenerateStringType(genericArgument, info, true);            // "true" to say that we are within a generic!

                ITypeInfo typeInfo = TypeInfoManager.GetTypeInfo(genericArgument);
                if (typeInfo != null)
                {
                    data += typeInfo.GetInstancePostFix();
                }

                --numArguments;
                if (numArguments <= 0)
                {
                    // We are done with our quota
                    break;
                }
            }

            if (templateParameters > 1)
            {
                // Close the macro...
                data += ")";
            }

            data += " >";
            return (true);
        }

        private string GenerateStringType(IType type, ParsingInfo info, bool nestedGeneric)
        {
            if (type == null)
            {
                return ("[NULL]");
            }
            // TODO:    Use an hash-table instead of this bunch of if test...

            ITypeReference typeReference = type as ITypeReference;
            if (typeReference != null)
            {
                string result = GenerateCodeTypeReferenceAsString(typeReference, info, nestedGeneric);
                return (result);
            }

            IReferenceType referenceType = type as IReferenceType;
            if (referenceType != null)
            {
                string result = GenerateStringType(referenceType.ElementType, info, nestedGeneric);

                // Not great as we parse the & to detect that's a ref
                // But we can improve this later...
                result += "&";
                return (result);
            }

            IArrayType arrayType = type as IArrayType;
            if (arrayType != null)
            {
                string elementTypeText;

                ITypeReference elementTypeReference = arrayType.ElementType as ITypeReference;
                if (elementTypeReference != null)
                {
                    ITypeInfo elementTypeInfo = TypeInfoManager.GetTypeInfo(elementTypeReference);
                    elementTypeText = elementTypeInfo.GetInstanceText(info);
                }
                else
                {
                    // We are in a template (for arrays), so we want the postfix as well
                    // Otherwise if the template parameter was a System.Object, we would miss the pointer on the object
                    elementTypeText = GenerateStringType(arrayType.ElementType, info, true);
                }

                string arrayText = "::System::Array__G< ";
                arrayText += elementTypeText;
                arrayText += " > *";
                return (arrayText);
            }

            IDefaultConstructorConstraint defaultConstructorConstraint = type as IDefaultConstructorConstraint;
            if (defaultConstructorConstraint != null)
            {
                return ("[DefaultConstructorConstraint]");
            }

            IFunctionPointer functionPointer = type as IFunctionPointer;
            if (functionPointer != null)
            {
                return ("[FunctionPointer]");
            }

            IGenericArgument genericArgument = type as IGenericArgument;
            if (genericArgument != null)
            {
                int index = genericArgument.Position;
                ITypeCollection collection = genericArgument.Owner.GenericArguments;
                IType genericType = collection[index];

                // Be careful with the following code...
                // Due to reflector circular data, it has a tendency to do infinite recursion

                for (; ; )
                {
                    IGenericParameter genericParameter = genericType as IGenericParameter;
                    if (genericParameter != null)
                    {
                        genericType = genericParameter.Resolve();
                        if (genericType == null)
                        {
                            Debug.Assert(genericParameter.Variance == GenericParameterVariance.NonVariant);
                            return (LanguageManager.NameFixup.GetSafeName(genericParameter.Name));
                        }
                        string result = GenerateCodeTypeAsString(genericType, info, nestedGeneric);
                        return (result);
                    }

                    genericArgument = genericType as IGenericArgument;
                    if (genericArgument != null)
                    {
                        index = genericArgument.Position;
                        collection = genericArgument.Owner.GenericArguments;
                        genericType = collection[index];
                        continue;
                    }

                    typeReference = genericType as ITypeReference;
                    // If it's a ITypeReference, call the ITypeReference directly...
                    if (typeReference != null)
                    {
                        // Don't get the full name directly, execute the reference in case we have to resolve the template
                        // TODO: Revisit this and see if it is really necessary...
                        string result = GenerateCodeTypeReferenceAsString(typeReference, info);

                        if (nestedGeneric)
                        {
                            // If within a generic, we need to add the post fix
                            ITypeInfo typeInfo = TypeInfoManager.GetTypeInfo(typeReference);
                            result += typeInfo.GetInstancePostFix();
                        }
                        return (result);
                    }
                    arrayType = genericType as IArrayType;
                    if (arrayType != null)
                    {
                        // genericType can actually be IArrayType - TODO: Handle this case...
                        // For the moment do something fake with a code markup to detect the issue...
                        return ("[=]" + GenerateStringType(arrayType, info, nestedGeneric));
                    }
                    Debug.Fail("Should never be here!");
                }
            }

            IOptionalModifier optionalModifier = type as IOptionalModifier;
            if (optionalModifier != null)
            {
                return ("[OptionalModifier]");
            }

            IPointerType pointerType = type as IPointerType;
            if (pointerType != null)
            {
                string result = GenerateStringType(pointerType.ElementType, info, nestedGeneric);

                result += " *";
                info.UnsafeMethod = true;   // Usafe of pointers implies unsafe code
                return (result);
            }

            IReferenceTypeConstraint referenceTypeConstraint = type as IReferenceTypeConstraint;
            if (referenceTypeConstraint != null)
            {
                return ("[ReferenceTypeConstraint]");
            }

            IRequiredModifier requiredModifier = type as IRequiredModifier;
            if (requiredModifier != null)
            {
                string modifier = requiredModifier.Modifier.ToString();
                switch (modifier)
                {
                    case "IsVolatile":
                        if (info.ParsingField)
                        {
                            // We put the volatile only when we parse the fields
                            // There are some cases where the volatile type is used during cast!
                            modifier = "volatile ";
                        }
                        else
                        {
                            // Every where else, get rid of volatile
                            // We must to have to find all the other valid places...
                            modifier = "";
                        }
                        break;

                    default:
                        break;
                }

                modifier += GenerateStringType(requiredModifier.ElementType, info, nestedGeneric);
                return (modifier);
            }

            IValueTypeConstraint valueTypeConstraint = type as IValueTypeConstraint;
            if (valueTypeConstraint != null)
            {
                return ("[ValueTypeConstraint]");
            }

            // This should happen only one time every new type...
            // So it's not an issue if it happens late in the process...
            LocalType localType = type as LocalType;
            if (localType != null)
            {
                return (localType.ToString());
            }

            return ("[UNKNOWN]");
        }

        public StringData GenerateCodeType(IType type, ParsingInfo info)
        {
            string typeName = GenerateCodeTypeAsString(type, info);
            StringData data = new StringData(typeName);
            data.LocalType = LanguageManager.LocalTypeManager.GetLocalType(type, typeName);
            return (data);
        }

        public String GenerateCodeTypeAsString(IType type, ParsingInfo info)
        {
            return (GenerateCodeTypeAsString(type, info, false));
        }

        private String GenerateCodeTypeAsString(IType type, ParsingInfo info, bool nestedGeneric)
        {
            string typeName = GenerateStringType(type, info, nestedGeneric);
            if (info.BaseDeclaringType != null)
            {
                if ((info.BaseDeclaringType.GenericArguments.Count != 0)
                    && (typeName.IndexOf('<') >= 0)
                    && info.EnableTypenameForCodeType)
                {
                    // We are within a generic declaration and the type generated is a generic type as well
                    // We add the keyword typename so the compiler can do the right thing...
                    typeName = "typename " + typeName;
                }
            }
            return (typeName);
        }

        public StringData GenerateCodeTypeWithPostfix(IType type, ParsingInfo info)
        {
            StringData data = GenerateCodeType(type, info);
            IType resolvedType;
            ITypeReference typeReference = Util.GetCorrespondingTypeReference(type, out resolvedType);
            if (typeReference != null)
            {
                ITypeInfo typeInfo = TypeInfoManager.GetTypeInfo(typeReference);
                data.AppendSameLine(typeInfo.GetInstancePostFix());
            }
            return (data);
        }

        public StringData GenerateCodeVariableDeclaration(IVariableDeclaration variableDeclaration, ParsingInfo info)
        {
            StringData data = GenerateCodeTypeWithPostfix(variableDeclaration.VariableType, info);

            data.AppendSameLine(" ");
            data.Append(LanguageManager.NameFixup.GetSafeName(variableDeclaration.Name));
            return (data);
        }

        public StringData GenerateCodeVariableReference(IVariableReference variableReference)
        {
            IVariableDeclaration varDeclaration = variableReference.Resolve();
            StringData data = new StringData(LanguageManager.NameFixup.GetSafeName(varDeclaration.Name));
            data.EmbeddedType = varDeclaration.VariableType;
            return (data);
        }

        public StringData GenerateCodeFieldReference(IFieldReference fieldReference, ParsingInfo info)
        {
            GenerateCodeType(fieldReference.FieldType, info);       // Don't use the resulting string, just used to update the info...

            string fieldName = fieldReference.Name;

            ITypeReference declaringType = (ITypeReference)fieldReference.DeclaringType;
            ITypeInfo typeInfo = TypeInfoManager.GetTypeInfo(declaringType);
            fieldName = LanguageManager.NameFixup.UnmangleName(fieldName);
            StringData data = new StringData(fieldName);
            data.EmbeddedType = fieldReference.FieldType;
            return (data);
        }

        public StringData GenerateCodeFieldDeclaration(IFieldDeclaration fieldDeclaration, ParsingInfo info)
        {
            StringData data = GenerateCodeTypeWithPostfix(fieldDeclaration.FieldType, info);

            data.AppendSameLine(" ");
            string fieldName = LanguageManager.NameFixup.UnmangleName(fieldDeclaration.Name);
            data.AppendSameLine(fieldName);
            if (fieldDeclaration.Initializer != null)
            {
                StringData field;
                if (fieldDeclaration.Static)
                {
                    if (info.StaticFieldInitialization == null)
                    {
                        info.StaticFieldInitialization = new StringData();
                    }
                    field = info.StaticFieldInitialization;
                }
                else
                {
                    if (info.FieldInitialization == null)
                    {
                        info.FieldInitialization = new StringData();
                    }
                    field = info.FieldInitialization;
                }

                field.Append(fieldName);
                field.AppendSameLine(" = ");
                field.AppendSameLine(LanguageManager.ExpressionGenerator.GenerateCode(fieldDeclaration.Initializer, info));
                field.AppendSameLine(";\n");
            }
            return (data);
        }

        public StringData GenerateCodeEnumDeclaration(IFieldDeclaration fieldDeclaration, ParsingInfo info)
        {
            string fieldName = fieldDeclaration.Name;
            ITypeReference declaringType = (ITypeReference)fieldDeclaration.DeclaringType;
            fieldName = LanguageManager.NameFixup.UnmangleName(fieldName);

            if (fieldName == info.BaseDeclaringType.Name)
            {
                // It happens that the name of the enum value is the same as the name of the enum type
                // This is not valid in C++, let's patch it
                fieldName = "__" + fieldName + "__";
            }

            StringData data = new StringData(fieldName);
            if (fieldDeclaration.Initializer != null)
            {
                data.AppendSameLine(" = ");
                data.AppendSameLine(LanguageManager.ExpressionGenerator.GenerateCode(fieldDeclaration.Initializer, info));
            }
            return (data);
        }

        public StringData GenerateCodePropertyDeclaration(IPropertyDeclaration propertyDeclaration, ParsingInfo info, out string returnValue)
        {
            StringData data = GenerateCodeTypeWithPostfix(propertyDeclaration.PropertyType, info);

            returnValue = data.Text;
            data.AppendSameLine(" ");
            if (propertyDeclaration.Parameters.Count != 0)
            {
                // If parameters for the property, it means that's an indexer...
                // If possible we have to use the fully qualified name as it enables several indexer with the same name
                string propertyName = propertyDeclaration.Name;
                if (propertyName.EndsWith(ITEM))
                {
                    propertyName = propertyName.Substring(0, propertyName.Length - ITEM.Length);
                    propertyName += "this";
                }
                data.AppendSameLine(propertyName);
                data.AppendSameLine("[");
                data.AppendSameLine(GenerateCodeParameterDeclarationCollection(propertyDeclaration.Parameters, info));
                data.AppendSameLine("]");
            }
            else
            {
                data.AppendSameLine(propertyDeclaration.Name);
            }
            return (data);
        }

        public StringData GenerateCodePropertyDeclarationName(IPropertyDeclaration propertyDeclaration, ParsingInfo info)
        {
            StringData data = new StringData(propertyDeclaration.Name);
            return (data);
        }

        public StringData GenerateCodePropertyDeclarationParameters(IPropertyDeclaration propertyDeclaration, ParsingInfo info)
        {
            if (propertyDeclaration.Parameters.Count != 0)
            {
                // If parameters for the property, it means that's an indexer...
                // If possible we have to use the fully qualified name as it enables several indexer with the same name
                return (GenerateCodeParameterDeclarationCollection(propertyDeclaration.Parameters, info));
            }
            else
            {
                // Not an indexer, there is no parameter...

                // Still create the Parameters so we make sure we are always implemented everywhere...
                info.Parameters = new Dictionary<string, VariableInfo>();
                return (new StringData());
            }
        }

        public string GenerateCodeParameterDeclaration(IParameterDeclaration parameter, ParsingInfo info)
        {
            string text = "";
            // TODO; Clean all of this...
            //  We might not need to have something that complex anymore...

            RecognizedAttribute attr = RecognizedAttribute.None;

            StringData originalTypeString = GenerateCodeTypeWithPostfix(parameter.ParameterType, info);
            // TODO: Check if we have to trim '&' in extern mode
            string typeString = originalTypeString.Text.TrimEnd('&');
            bool alsoRef = false;

            if (typeString != originalTypeString.Text)
            {
                // If type finishes by &, it's by default a ref
                Debug.Assert(originalTypeString.EmbeddedType is ReferenceType);
                attr = RecognizedAttribute.RefParameter;
                alsoRef = true;
            }
            /*
                            text += "PT =  " + parameter.ParameterType.ToString() + "    ";
                            text += "TypeString  =  " + typeString + "     ";
                            text += "OriginalTypeString  =  " + originalTypeString + "    ";
            */

            foreach (ICustomAttribute customAttribute in parameter.Attributes)
            {
                string attrText = customAttribute.Constructor.DeclaringType.ToString();
                /*
                                    text += "[Attribute]  " + attrText;
                */
                if (attrText == "OutAttribute")
                {
                    if (attr == RecognizedAttribute.InParameter)
                    {
                        attr = RecognizedAttribute.InOutParameter;
                    }
                    else
                    {
                        attr = RecognizedAttribute.OutParameter;
                    }
                }
                else if (attrText == "InAttribute")
                {
                    if (attr == RecognizedAttribute.OutParameter)
                    {
                        attr = RecognizedAttribute.InOutParameter;
                    }
                    else
                    {
                        attr = RecognizedAttribute.InParameter;
                    }
                }
                else if (attrText == "ParamArrayAttribute")
                {
                    attr = RecognizedAttribute.ParamsParameter;
                }
                else
                {
                    // Unknown attribute, don't change the last set
                    //TODO: Add other attributes...
                    //text += attrText;
                }
            }

            VariableMode mode = VariableMode.NORMAL;
            switch (attr)
            {
                case RecognizedAttribute.OutParameter:
                    if (info.InExtern)
                    {
                        // For extern code, we keep the attribute as is
                        mode = VariableMode.OUT;
                    }
                    /*else*/
                    if (alsoRef)
                    {
                        // We are adding the keyword "out " only if the "&" exist
                        // And we are not in an extern method
                        mode = VariableMode.OUT;
                    }
                    break;
                case RecognizedAttribute.RefParameter:
                    mode = VariableMode.REF;
                    break;
                case RecognizedAttribute.ParamsParameter:
                    /*
                     * Don't use params for C++
                     * 
                    text += "params ";
                     */
                    break;
                case RecognizedAttribute.InParameter:
                    if (alsoRef)
                    {
                        mode = VariableMode.REF;
                    }
                    break;
                case RecognizedAttribute.InOutParameter:
                    if (alsoRef)
                    {
                        mode = VariableMode.OUT;
                    }
                    break;
                case RecognizedAttribute.None:
                    // No or unknown attribute
                    break;
            }

            if ((mode == VariableMode.OUT) || (mode == VariableMode.REF))
            {
                text += typeString;

                int counter = 1;
                //text += " *";
                IType originalLocalType = originalTypeString.EmbeddedType;
                ReferenceType refType = originalLocalType as ReferenceType;
                // We have to test something specific here... If it is a reference type, and it is passed as out or ref
                // Then we need to add another layer of pointer (classes passed by ref are considered in and out...)
                if (refType != null)
                {
                    IType nonRefType = refType.ElementType;
                    ITypeInfo typeInfo = TypeInfoManager.GetTypeInfo(nonRefType);
                    if ((typeInfo != null) && (typeInfo.IsValueType == false))
                    {
                        // The type was not a value type, so it was a reference type
                        // Add another layer of pointer...
                        //text += " *";
                        ++counter;
                    }

                    // Note: There are some cases where typeInfo == null and still this is a reference type
                    // An example of this is the arrays... Fortunately arrays are automaticlly handled with pointer...
                }

#if REF_OUT || true
                while (counter > 0)
                {
                    --counter;
                    text += " *";
                }
#else
                    while (counter > 1)
                    {
                        --counter;
                        text += " *";
                    }
                    text += " &";
#endif
            }
            else if (originalTypeString.EmbeddedType is IPointerType)
            {
                // If that's a pointer, we use the pointer wrapper...
                // The reason is that in C++, we cannot differentiate between pointer, value and ref/out.
                // This is a major issue with two functions named the same with same parameters except for the modifiers.
                // If ref/out uses pointers, then it collides with other pointer during the method declaration
                // If ref/out uses reference (&), then it collides with standard type during function call...
                // CrossNetRuntime::PointerWrapper disambiguates that...
                IPointerType pointerType = (IPointerType)(originalTypeString.EmbeddedType);

                StringData tempData = GenerateCodeTypeWithPostfix(pointerType.ElementType, info);
                string subType = tempData.Text;

                subType = "::CrossNetRuntime::PointerWrapper<" + typeString + " >";
                text += subType;
            }
            else
            {
                text += typeString;
            }

            if (parameter.Name != null)
            {
                // Add the parameter name if we have one...
                text += " ";
                string varName = LanguageManager.NameFixup.UnmangleName(parameter.Name);
                if (varName != "")
                {
                    text += varName;

                    VariableInfo varInfo = new VariableInfo(typeString, varName, mode);
                    // Don't call Add as because due to anonymous methods, this method can be called several time
                    // With the same parameters...
                    info.Parameters[varName] = varInfo;
                }
            }
            return (text);
        }

        public StringData GenerateCodeParameterDeclarationCollection(IParameterDeclarationCollection parameterDeclarations, ParsingInfo info)
        {
            string text = "";

            info.Parameters = new Dictionary<string, VariableInfo>(parameterDeclarations.Count);

            bool prev = false;
            foreach (IParameterDeclaration parameter in parameterDeclarations)
            {
                if (prev)
                {
                    text += ", ";
                }

                text += GenerateCodeParameterDeclaration(parameter, info);
                prev = true;
            }
            return (new StringData(text));
        }

        public StringData GenerateCodeParameterCollection(IParameterDeclarationCollection parameterDeclarations, ParsingInfo info)
        {
            string text = "";

            bool prev = false;
            foreach (IParameterDeclaration parameter in parameterDeclarations)
            {
                if (prev)
                {
                    text += ", ";
                }

                if (parameter.Name != null)
                {
                    // Add the parameter name if we have one...
                    text += " ";
                    text += LanguageManager.NameFixup.UnmangleName(parameter.Name);
                }
                else
                {
                    Debug.Fail("With this function, we should not be here otherwise it is meaningless...");
                }
                prev = true;
            }
            return (new StringData(text));
        }

        public StringData GenerateCodeParameterReference(IParameterReference parameterReference)
        {
            string parameterName = LanguageManager.NameFixup.UnmangleName(parameterReference.Name);
            StringData data = new StringData(parameterName);
            IParameterDeclaration decl = parameterReference as IParameterDeclaration;
            if (decl != null)
            {
                data.EmbeddedType = decl.ParameterType;
            }
            return (data);
        }

        public StringData GenerateCodePropertyReference(IPropertyReference propertyReference)
        {
            StringData data = new StringData(propertyReference.Name);
            data.EmbeddedType = propertyReference.PropertyType;
            return (data);
        }

        public StringData GenerateCodeEventReference(IEventReference eventReference)
        {
            StringData data = new StringData(eventReference.Name);
            data.EmbeddedType = eventReference.EventType;
            return (data);
        }

        public StringData GenerateCodeEventDeclaration(IEventDeclaration eventDeclaration, ParsingInfo info)
        {
            StringData data = GenerateCodeTypeReference(eventDeclaration.EventType, info);
            ITypeInfo typeInfo = TypeInfoManager.GetTypeInfo(eventDeclaration.EventType);
            data.AppendSameLine(typeInfo.GetInstancePostFix());
            data.AppendSameLine(" ");
            data.AppendSameLine(LanguageManager.NameFixup.GetSafeName(eventDeclaration.Name));
            return (data);
        }

        public StringData GenerateCodeMemberReference(IMemberReference memberReference, ParsingInfo info)
        {
            StringData data = new StringData(memberReference.Name);
            return (data);
        }

        private enum RecognizedAttribute
        {
            None,
            OutParameter,
            RefParameter,
            ParamsParameter,
            InOutParameter,
            InParameter,
        }

        private const string ITEM = "Item";
    }
}

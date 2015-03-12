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

namespace CrossNet.CSharpRuntime
{
    public class CSharpReferenceGenerator : IReferenceGenerator
    {
        public StringData GenerateCodeMethodReference(IMethodReference methodReference, ParsingInfo info)
        {
            string methodName;
            if (methodReference.Name == ".ctor")
            {
                // If constructor, use the type name
                IType declaringType = methodReference.DeclaringType;
//                methodName = methodReference.DeclaringType.ToString();
                methodName = GenerateCodeType(methodReference.DeclaringType, info).Text;
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

                AddGenericArguments(methodReference.GenericArguments, info, out outputText);
                methodName += outputText;
            }
            methodName = LanguageManager.NameFixup.UnmangleMethodName(methodName, false);
            methodName = LanguageManager.NameFixup.ConvertMethodName(methodName, methodReference, out info.MethodType);
            info.MethodName = methodName;
            info.MethodReference = methodReference;
            StringData data = new StringData(methodName);

            // Just before we leave the method, scan all the parameters and return value for the method and see if there is an unsafe one
            // Here we don't care about the resulting unusedData, we just want to see "info" updated
            if (info.UnsafeMethod == false)
            {
                // To speed things a little bit, we are doing this check only if the method is not unsafe already
                StringData unusedData = GenerateCodeType(methodReference.ReturnType.Type, info);
                unusedData = GenerateCodeParameterDeclarationCollection(methodReference.Parameters, info);
            }
            return (data);
        }

        public StringData GenerateCodeTypeReference(ITypeReference typeReference, ParsingInfo info)
        {
            StringData data = new StringData();
            bool alreadyTemplated = false;

            object owner = typeReference.Owner;

            if (typeReference.GenericArguments.Count != 0)
            {
                if (info.CurrentGenericArgument == null)
                {
                    info.CurrentGenericArgument = new Stack<IType>();
                }
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
                // data.AppendSameLine("[Owner]");

                StringData tempData = new StringData();
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
                        if (nameSpace.StartsWith("<") == false)
                        {
                            // Avoid incorrect namespace (when starting with "<")
                            data.AppendSameLine(typeReferenceOwner.Namespace);
                            //                    tempData.AppendSameLine("[A]");
                            data.AppendSameLine(".");
                            //                    tempData.AppendSameLine("[B]");
                        }
                    }

                    StringData tempData = new StringData();
                    alreadyTemplated = false;
                    bool textAdded = AddTypeName(typeReferenceOwner, tempData, info, ref alreadyTemplated);
                    if (textAdded)
                    {
                        //                    tempData.AppendSameLine("[C]");
                        data.AppendSameLine(tempData);
                        data.AppendSameLine(".");
                        //                    tempData.AppendSameLine("[D]");
                    }
                }
            }

            if (typeReference.Namespace != "")
            {
                string nameSpace = typeReference.Namespace;
                if (nameSpace.StartsWith("<") == false)
                {
                    // Avoid incorrect namespace (when starting with "<")
                    // data.AppendSameLine("[Namespace]");
                    data.AppendSameLine(typeReference.Namespace);
                    //                data.AppendSameLine("[E]");
                    data.AppendSameLine(".");
                    //                data.AppendSameLine("[F]");
                }
            }
            alreadyTemplated = false;
            AddTypeName(typeReference, data, info, ref alreadyTemplated);

            if (data.Text == "System.Void")
            {
                data.Replace("void");
            }

            // data.AppendSameLine([Reference]);
            data.LocalType = LanguageManager.LocalTypeManager.GetLocalType(typeReference, data.Text);
            return (data);
        }

        public String GenerateCodeTypeReferenceAsString(ITypeReference typeReference, ParsingInfo info)
        {
            return (GenerateCodeTypeReference(typeReference, info).Text);
        }

        private bool AddTypeName(ITypeReference typeReference, StringData data, ParsingInfo info, ref bool alreadyTemplated)
        {
            // Unmangle just the name and not previous namespace (which could contain template code).
            string text = LanguageManager.NameFixup.UnmangleName(typeReference.Name);
            data.AppendSameLine(text);

            if (alreadyTemplated)
            {
                return (text != "");
            }

            string outputText;
            alreadyTemplated = AddGenericArguments(typeReference.GenericArguments, info, out outputText);
            if (outputText != "")
            {
                data.AppendSameLine(outputText);
                return (true);
            }
            return (text != "");
        }

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

            data = "<";
            bool first = true;
            while (info.CurrentGenericArgument.Count != 0)
            {
                if (first == false)
                {
                    data += ", ";
                }
                first = false;
                IType genericArgument = info.CurrentGenericArgument.Pop();
                data += GenerateStringType(genericArgument, info);
            }
            data += ">";
            return (true);
        }

        private string GenerateStringType(IType type, ParsingInfo info)
        {
            if (type == null)
            {
                return ("[NULL]");
            }
            // TODO:    Use an hash-table instead of this bunch of if test...

            ITypeReference typeReference = type as ITypeReference;
            if (typeReference != null)
            {
                string result = GenerateCodeTypeReference(typeReference, info).Text;
                return (result);
            }

            IReferenceType referenceType = type as IReferenceType;
            if (referenceType != null)
            {
                string result = GenerateStringType(referenceType.ElementType, info);

                // Not great as we parse the & to detect that's a ref
                // But we can improve this later...
                result += "&";
                return (result);
            }

            IArrayType arrayType = type as IArrayType;
            if (arrayType != null)
            {
                string result = GenerateStringType(arrayType.ElementType, info);

                result += "[";
                bool first = true;
                foreach (IArrayDimension dimension in arrayType.Dimensions)
                {
                    if (first == false)
                    {
                        result += ",";
                    }
                    first = false;
                    if (dimension.UpperBound > 0)
                    {
                        result += dimension.UpperBound;
                    }
                }
                result += "]";
                return (result);
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
                ITypeDeclaration decl = genericArgument.Owner as ITypeDeclaration;
                return (genericType.ToString());
            }

            IOptionalModifier optionalModifier = type as IOptionalModifier;
            if (optionalModifier != null)
            {
                return ("[OptionalModifier]");
            }

            IPointerType pointerType = type as IPointerType;
            if (pointerType != null)
            {
                string result = GenerateStringType(pointerType.ElementType, info);

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

                modifier += GenerateStringType(requiredModifier.ElementType, info);
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
/*
        public static string GetInterfaces(object o)
        {
            if (o == null)
            {
                return ("[null]");
            }
            Type[] interfaces = o.GetType().GetInterfaces();
            string text = "";
            foreach (Type oneType in interfaces)
            {
                if (text != "")
                {
                    text += " ";
                }
                text += oneType.ToString();
            }
            return (text);
        }
*/
        public StringData GenerateCodeType(IType type, ParsingInfo info)
        {
            string typeName = GenerateCodeTypeAsString(type, info);
            StringData data = new StringData(typeName);
            data.LocalType = LanguageManager.LocalTypeManager.GetLocalType(type, typeName);
            return (data);
        }

        public String GenerateCodeTypeAsString(IType type, ParsingInfo info)
        {
            string typeName = GenerateStringType(type, info);
            return (typeName);
        }

        public StringData GenerateCodeTypeWithPostfix(IType type, ParsingInfo info)
        {
            // No postfix in C# - regardless of the ObjectType...
            return (GenerateCodeType(type, info));
        }

        public StringData GenerateCodeVariableDeclaration(IVariableDeclaration variableDeclaration, ParsingInfo info)
        {
            StringData data = GenerateCodeType(variableDeclaration.VariableType, info);
            data.Append(" ");
            data.Append(variableDeclaration.Name);
            return (data);
        }

        public StringData GenerateCodeVariableReference(IVariableReference variableReference)
        {
            IVariableDeclaration varDeclaration = variableReference.Resolve();
            StringData data = new StringData(varDeclaration.Name);
            data.EmbeddedType = varDeclaration.VariableType;
            return (data);
        }

        public StringData GenerateCodeFieldReference(IFieldReference fieldReference, ParsingInfo info)
        {
            GenerateCodeType(fieldReference.FieldType, info);       // Don't use the resulting string, just used to update the info...
            string fieldName = LanguageManager.NameFixup.UnmangleName(fieldReference.Name);
            StringData data = new StringData(fieldName);
            data.EmbeddedType = fieldReference.FieldType;
            return (data);
        }

        public StringData GenerateCodeFieldDeclaration(IFieldDeclaration fieldDeclaration, ParsingInfo info)
        {
            StringData data = GenerateCodeType(fieldDeclaration.FieldType, info);
            data.AppendSameLine(" ");
            string fieldName = LanguageManager.NameFixup.UnmangleName(fieldDeclaration.Name);
            data.AppendSameLine(fieldName);
            if (fieldDeclaration.Initializer != null)
            {
                data.AppendSameLine(" = ");
                data.AppendSameLine(LanguageManager.ExpressionGenerator.GenerateCode(fieldDeclaration.Initializer, info));
            }
            return (data);
        }

        public StringData GenerateCodeEnumDeclaration(IFieldDeclaration fieldDeclaration, ParsingInfo info)
        {
            string fieldName = LanguageManager.NameFixup.UnmangleName(fieldDeclaration.Name);
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
            StringData data = GenerateCodeType(propertyDeclaration.PropertyType, info);
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
                return (new StringData());
            }
        }

        public string GenerateCodeParameterDeclaration(IParameterDeclaration parameter, ParsingInfo info)
        {
            string text = "";

            RecognizedAttribute attr = RecognizedAttribute.None;

            string originalTypeString = GenerateCodeType(parameter.ParameterType, info).Text;
            // TODO: Check if we have to trim '&' in extern mode
            string typeString = originalTypeString.TrimEnd('&');
            bool alsoRef = false;

            if (typeString != originalTypeString)
            {
                // If type finishes by &, it's by default a ref
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

            switch (attr)
            {
                case RecognizedAttribute.OutParameter:
                    if (info.InExtern)
                    {
                        // For extern code, we keep the attribute as is
                        text += "[System.Runtime.InteropServices.Out] ";
                    }
                    /*else*/
                    if (alsoRef)
                    {
                        // We are adding the keyword "out " only if the "&" exist
                        // And we are not in an extern method
                        text += "out ";
                    }
                    break;
                case RecognizedAttribute.RefParameter:
                    text += "ref ";
                    break;
                case RecognizedAttribute.ParamsParameter:
                    text += "params ";
                    break;
                case RecognizedAttribute.InParameter:
                    text += "[System.Runtime.InteropServices.In] ";
                    if (alsoRef)
                    {
                        text += "ref ";
                    }
                    break;
                case RecognizedAttribute.InOutParameter:
                    text += "[System.Runtime.InteropServices.In, System.Runtime.InteropServices.Out] ";
                    if (alsoRef /*&& (info.InExtern == false)*/)
                    {
                        text += "ref ";
                    }
                    break;
                case RecognizedAttribute.None:
                    // No or unknown attribute
                    break;
            }

            text += typeString;
            if (parameter.Name != null)
            {
                // Add the parameter name if we have one...
                text += " ";
                text += LanguageManager.NameFixup.UnmangleName(parameter.Name);
            }
            return (text);
        }

        public StringData GenerateCodeParameterDeclarationCollection(IParameterDeclarationCollection parameterDeclarations, ParsingInfo info)
        {
            string text = "";

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
            return (data);
        }

        public StringData GenerateCodeEventDeclaration(IEventDeclaration eventDeclaration, ParsingInfo info)
        {
            StringData data = new StringData("event ");
            data.AppendSameLine(GenerateCodeTypeReference(eventDeclaration.EventType, info));
            data.AppendSameLine(" ");
            data.AppendSameLine(eventDeclaration.Name);
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

        private static string ITEM = "Item";
    }
}

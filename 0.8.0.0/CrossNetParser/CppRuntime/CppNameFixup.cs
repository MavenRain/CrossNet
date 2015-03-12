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

using Reflector.CodeModel;

using CrossNet.Common;
using CrossNet.Interfaces;
using CrossNet.Net;

namespace CrossNet.CppRuntime
{
    public class CppNameFixup : INameFixup
    {

        public CppNameFixup(params string[] keywords)
        {
            // By convention, the user code should not use "__" in the name. (This is being asked by CrossNet to avoid any possible conflict)
            // There is no need to add them to the list (there might be some excpetion though, like with macros...)

            // Remember that in C#, the user can prefix a variable with @ and use any C# keyword, so no keyword is "safe"

            // Add the standard 
            AddKeyword("break", "case", "catch", "class", "const", "const_cast", "continue", "default", "delete", "do", "dynamic_cast", "else", "enum", "explicit");
            AddKeyword("extern", "for", "friend", "goto", "if", "inline", "mutable", "namespace", "new", "NULL", "operator");
            AddKeyword("private", "protected", "public", "register", "reinterpret_cast", "return", "signed", "sizeof", "static");
            AddKeyword("static_cast", "struct", "switch", "template", "this", "throw", "try", "typedef", "typename", "union", "unsigned", "using");
            AddKeyword("virtual", "volatile", "while");

            // Also add all the base types (and related)
            // Also note that all the types returned by CrossNet are all fully qualified
            // i.e. even void is actually manipulated as ::System::Void
            // This way, if we have a void (either as name or as type), it actually meant @void
            AddKeyword("bool", "char", "double", "false", "float", "int", "long", "short", "true", "void");

            // Add some Microst specific keywords (in reality the list is much longer than that...)
            // We'll have to add some macros as well
            AddKeyword("finally", "asm", "naked");

            // Add some standard macros...
            AddKeyword("stdin", "stdout", "stderr", "__FILE__", "__LINE__", "EOF", "OVERFLOW", "_OVERFLOW");

            // Add som global functions that could be misinterpreted with namespace
            // Currently this is fir example used by Rotor's unit-tests
            AddKeyword("pow");

            AddKeyword(keywords);
        }

        public string ConvertMethodName(string methodName, IMethodReference method, out MethodType methodType)
        {
            if (methodName.StartsWith("op_") == false)
            {
                methodType = MethodType.NORMAL;
                return (GetSafeName(methodName));
            }
            string op = methodName;
            switch (methodName)
            {
                case "op_UnaryPlus":
                case "op_UnaryNegation":
                case "op_LogicalNot":
                case "op_OnesComplement":
                case "op_Increment":
                case "op_Decrement":
                case "op_Addition":
                case "op_Subtraction":
                case "op_Multiply":
                case "op_Division":
                case "op_Modulus":
                case "op_BitwiseAnd":
                case "op_BitwiseOr":
                case "op_ExclusiveOr":
                case "op_LeftShift":
                case "op_RightShift":
                case "op_Equality":
                case "op_Inequality":
                case "op_LessThan":
                case "op_GreaterThan":
                case "op_LessThanOrEqual":
                case "op_GreaterThanOrEqual":
                    methodType = MethodType.OPERATOR;
                    return (op);

                case "op_True":
                    methodType = MethodType.OPERATOR_TRUE;
                    return (op);

                case "op_False":
                    methodType = MethodType.OPERATOR_FALSE;
                    return (op);

                case "op_Explicit":
                    op = GetConversionMethodName(method);
                    methodType = MethodType.OPERATOR_EXPLICIT;
                    return (op);

                case "op_Implicit":
                    op = GetConversionMethodName(method);
                    methodType = MethodType.OPERATOR_IMPLICIT;
                    return (op);
            }

            // If unrecognized operator, use the passed one
            methodType = MethodType.NORMAL;
            return (op);
        }

        public string GetConversionMethodName(IMethodReference method)
        {
            LocalType type = LanguageManager.LocalTypeManager.GetLocalType(method.ReturnType.Type);
            // Transform the return type in something that can be used in the method name
            // so the implicit operators are differents between return type
            // The return type can potentially return an unsafe pointer / reference, so take care of that as well

            string mangleTypeName;
            ITypeInfo typeInfo = type.GetTypeInfo();
            if (typeInfo != null)
            {
                mangleTypeName = typeInfo.FullNameWithoutGeneric.Replace("::", "__");
            }
            else
            {
                mangleTypeName = type.FullName;
            }
            mangleTypeName = mangleTypeName.Replace("::", "__");
            mangleTypeName = mangleTypeName.Replace(" *", "__P__");
            mangleTypeName = mangleTypeName.Replace("&", "__R__");
            return (GetSafeName(method.Name + mangleTypeName));
        }

        public string UnmangleName(string text)
        {
            return (UnmangleNameEx(text, INVALID_CHAR_FOR_NAME));
        }

        public string UnmangleMethodName(string text, bool extended)
        {
            if (text.StartsWith("<"))
            {
                int index = text.IndexOf('>');
                if (index != -1)
                {
                    string newText = text.Substring(0, index + 1);
                    newText = UnmangleNameEx(newText, EXT_INVALID_CHAR_FOR_NAME);
                    newText += UnmangleNameEx(text.Substring(index + 1), EXT_INVALID_CHAR_FOR_NAME);
                    return (GetSafeName(newText));
                }
                else
                {
                    if (extended)
                    {
                        return (UnmangleNameEx(text, EXT_INVALID_CHAR_FOR_NAME));
                    }
                    else
                    {
                        return (UnmangleName(text));
                    }
                }
            }
            if (extended)
            {
                return (UnmangleNameEx(text, EXT_INVALID_CHAR_FOR_NAME));
            }
            else
            {
                return (GetSafeName(text));
            }
        }

        public string GetSafeName(string name)
        {
            if (mAllKeywords.ContainsKey(name) == false)
            {
                return (name);
            }

            // There is a collision with a keyword, change the name
            // By convention, user should not use name with "__", so this should reduce any possible collision.
            return ("__" + name + "__");
        }

        public string GetSafeFullName(string fullName)
        {
            string[] split = fullName.Split(sSplitPattern, StringSplitOptions.None);
            int length = split.Length;
            StringBuilder builder = new StringBuilder(fullName.Length);
            for (int i = 0; i < length; ++i)
            {
                string subString = GetSafeName(split[i]);
                if (i != 0)
                {
                    builder.Append("::");
                }
                builder.Append(subString);
            }
            return (builder.ToString());
        }

        private string UnmangleNameEx(string text, char[] invalidChars)
        {
            int index = text.IndexOfAny(invalidChars);
            if (index == -1)
            {
                return (GetSafeName(text));
            }

            // We replace only if we found at least one invalid character
            foreach (char invalidCharacter in invalidChars)
            {
                text = text.Replace(invalidCharacter, VALID_CHARACTER);
            }
            return (GetSafeName(text));
        }

        protected void AddKeyword(params string[] keywords)
        {
            foreach (string oneKeyword in keywords)
            {
                mAllKeywords.Add(oneKeyword, null);
            }
        }

        private static char[] INVALID_CHAR_FOR_NAME = new char[] { '<', '>', '{', '}', '-', '$', '=', '.', ' ' };
        private static char[] EXT_INVALID_CHAR_FOR_NAME = new char[] { '<', '>', '{', '}', '-', '$', '.', '=', ',', ' ' };
        private static char VALID_CHARACTER = '_';
        private static string[] sSplitPattern = new string[] { "::" };
        protected IDictionary<string, object> mAllKeywords = new Dictionary<string, object>();
    }
}

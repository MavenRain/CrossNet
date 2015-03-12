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

namespace CrossNet.CSharpRuntime
{
    public class CSharpNameFixup : INameFixup
    {
        public string ConvertMethodName(string methodName, IMethodReference method, out MethodType methodType)
        {
            if (methodName.StartsWith("op_") == false)
            {
                methodType = MethodType.NORMAL;
                return (methodName);
            }
            methodType = MethodType.OPERATOR;
            string op = "";
            switch (methodName)
            {
                case "op_UnaryPlus":
                    op = "+";
                    break;

                case "op_UnaryNegation":
                    op = "-";
                    break;

                case "op_LogicalNot":
                    op = "!";
                    break;

                case "op_OnesComplement":
                    op = "~";
                    break;

                case "op_Increment":
                    op = "++";
                    break;

                case "op_Decrement":
                    op = "--";
                    break;

                case "op_True":
                    op = "true";
                    methodType = MethodType.OPERATOR_TRUE;
                    break;

                case "op_False":
                    op = "false";
                    methodType = MethodType.OPERATOR_FALSE;
                    break;

                case "op_Addition":
                    op = "+";
                    break;

                case "op_Subtraction":
                    op = "-";
                    break;

                case "op_Multiply":
                    op = "*";
                    break;

                case "op_Division":
                    op = "/";
                    break;

                case "op_Modulus":
                    op = "%";
                    break;

                case "op_BitwiseAnd":
                    op = "&";
                    break;

                case "op_BitwiseOr":
                    op = "|";
                    break;

                case "op_ExclusiveOr":
                    op = "^";
                    break;

                case "op_LeftShift":
                    op = "<<";
                    break;

                case "op_RightShift":
                    op = ">>";
                    break;

                case "op_Equality":
                    op = "==";
                    break;

                case "op_Inequality":
                    op = "!=";
                    break;

                case "op_LessThan":
                    op = "<";
                    break;

                case "op_GreaterThan":
                    op = ">";
                    break;

                case "op_LessThanOrEqual":
                    op = "<=";
                    break;

                case "op_GreaterThanOrEqual":
                    op = ">=";
                    break;

                case "op_Explicit":
                    op = GetConversionMethodName(method);
                    methodType = MethodType.OPERATOR_EXPLICIT;
                    break;

                case "op_Implicit":
                    op = GetConversionMethodName(method);
                    methodType = MethodType.OPERATOR_IMPLICIT;
                    break;
            }

            switch (methodType)
            {
                default:
                    Debug.Fail("Should not be here!");
                    return (methodName);

                case MethodType.OPERATOR:
                    if (op != "")
                    {
                        return (op);
                    }
                    // If unrecognized operator, use the passed one
                    methodType = MethodType.NORMAL;
                    return (methodName);

                case MethodType.OPERATOR_FALSE:
                case MethodType.OPERATOR_TRUE:
                case MethodType.OPERATOR_IMPLICIT:
                case MethodType.OPERATOR_EXPLICIT:
                    return (op);
            }
        }

        public string GetConversionMethodName(IMethodReference method)
        {
            if (method.Name == "op_Implicit")
            {
                return ("implicit ");
            }
            else
            {
                return ("explicit ");
            }
        }

        public string UnmangleName(string text)
        {
            return (UnmangleNameEx(text, INVALID_CHAR_FOR_NAME));
        }

        public string UnmangleMethodName(string text, bool extended)
        {
            if ((int)text[0] >= 0x80)
            {
                return (UnmangleName(text));
            }
            if (text.StartsWith("<"))
            {
                int index = text.IndexOf('>');
                if (index != -1)
                {
                    string newText = text.Substring(0, index + 1);
                    newText = UnmangleNameEx(newText, EXT_INVALID_CHAR_FOR_NAME);
                    newText += text.Substring(index + 1);
                    return (newText);
                }
                else
                {
                    return (UnmangleName(text));
                }
            }
            return (text);
        }

        public string GetSafeName(string name)
        {
            return (name);
        }

        public string GetSafeFullName(string fullName)
        {
            return (fullName);
        }

        private static string UnmangleNameEx(string text, char[] invalidChars)
        {
            // Firsty we determine all the correct char name (usually due to obfuscator)
            bool obfuscated = false;
            foreach (char c in text)
            {
                if ((int)c >= 0x80)
                {
                    obfuscated = true;
                    break;
                }
            }

            if (obfuscated)
            {
                string temp = "";

                foreach (char c in text)
                {
                    if ((int)c >= 0x80)
                    {
                        temp += "_" + ((int)c).ToString() + "_";
                    }
                    else
                    {
                        temp += c;
                    }
                }

                text = temp;
            }

            int index = text.IndexOfAny(invalidChars);
            if (index == -1)
            {
                return (text);
            }

            // We replace only if we found at least one invalid character
            foreach (char invalidCharacter in invalidChars)
            {
                text = text.Replace(invalidCharacter, VALID_CHARACTER);
            }
            return (text);
        }

        private static char[] INVALID_CHAR_FOR_NAME = new char[] { '<', '>', '{', '}', '-', '$', '=' };
        private static char[] EXT_INVALID_CHAR_FOR_NAME = new char[] { '<', '>', '{', '}', '-', '$', '.', '=' };
        private static char VALID_CHARACTER = '_';
    }
}

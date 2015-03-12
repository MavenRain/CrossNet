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

using CrossNet.Common;

namespace CrossNet.CppRuntime
{
    public static class StringPool
    {
        public static void StartAssembly(string outputAssemblyName)
        {
            sOutputAssemblyName = outputAssemblyName;
            sAllStrings.Clear();
            sAllNames.Clear();
        }

        public static StringData GetStringDeclarations()
        {
            StringData data = new StringData();
            foreach (string name in sAllNames.Keys)
            {
                data.Append("extern ::System::String * " + name + ";\n");
            }
            return (data);
        }

        public static StringData GetStringInitializations()
        {
            StringData data = new StringData();
            foreach (KeyValuePair<string, string> pair in sAllStrings)
            {
                string generatedString = NormalizeStringExpression(pair.Key);
                data.Append(pair.Value + " = ::CrossNetRuntime::StringPooler::GetOrCreateString(");
                data.AppendSameLine(generatedString);
                if (pair.Key.IndexOf('\0') >= 0)
                {
                    // There is a zero in the string, use the long version with the passed length
                    data.AppendSameLine(", " + pair.Key.Length);
                }
                data.AppendSameLine(");\n");
            }
            return (data);
        }

        public static StringData GetStringDefinitions()
        {
            StringData data = new StringData();
            foreach (string name in sAllNames.Keys)
            {
                data.Append("::System::String * " + name + ";\n");
            }
            return (data);
        }

        public static string CreateString(string text)
        {
            string value;
            bool result = sAllStrings.TryGetValue(text, out value);
            if (result)
            {
                return (value);
            }

            // The corresponding string doesn't exist yet in the pool, we need to add it
            // First determine the variable name
            string varName = GetVarName(text);
            sAllStrings.Add(text, varName);
            return (varName);
        }

        private static string GetVarName(string text)
        {
            string varName = "s" + sOutputAssemblyName + "__";

            int size = text.Length;
            if (size > NAME_SIZE)
            {
                size = NAME_SIZE;
            }
            string shortText = text.Substring(0, size);

            // Now make the shortText like a variable name
            // We are fine with any character like a-zA-Z0-9_
            foreach (char c in shortText)
            {
                if (    ((c >= 'a') && (c <= 'z'))
                    ||  ((c >= 'A') && (c <= 'Z'))
                    ||  ((c >= '0') && (c <= '9'))  )
                {
                    varName += c;
                }
                else
                {
                    // If it was underscore, we push underscore anyway...
                    varName += "_";
                }
            }

            string postfix = "";
            int counter = 0;

            for (;;)
            {
                // Find the next available varName
                string varNamePostFix = varName + postfix;
                if (sAllNames.ContainsKey(varNamePostFix) == false)
                {
                    sAllNames.Add(varNamePostFix, null);
                    return (varNamePostFix);
                }

                postfix = "__" + counter.ToString();
                ++counter;
            }
        }

        // Duplicated from CppExpressionGenerator, we might want to improve this...
        private static string NormalizeStringExpression(string text)
        {
            StringBuilder sb = new StringBuilder("L\"", text.Length);
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

        private const int NAME_SIZE = 10;
        private static string sOutputAssemblyName;
        private static IDictionary<string, string> sAllStrings = new Dictionary<string, string>();
        private static IDictionary<string, string> sAllNames = new Dictionary<string, string>();
    }
}

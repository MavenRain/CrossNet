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

namespace CrossNet.CppRuntime
{
    class CppUtil
    {
        public static string InterfaceCall(string target, string interfaceName)
        {
            string text = INTERFACE_CALL + "(" + target + ", " + interfaceName + ")";
            return (text);
        }

        public static string InterfaceCall(string target, string interfaceName, string methodName, params string[] parameters)
        {
            string text = InterfaceCallEx(target, interfaceName, methodName, parameters);
            text += ")";
            return (text);
        }

        public static string InterfaceCallEx(string target, string interfaceName, string methodName, params string[] parameters)
        {
            string text = INTERFACE_CALL + "(" + target + ", " + interfaceName + ")->";
            text += methodName + "(" + target;

            if ((parameters != null) && (parameters.Length != 0))
            {
                foreach (string oneParameter in parameters)
                {
                    text += ", ";
                    text += oneParameter;
                }
            }
            return (text);
        }

        // If you want to change this code, make sure that the runtime is updated accordingly...
        // And the other way around too
        //  Search for int CrossNetRuntime::GetHashCode(void * buffer, int size)
        public static Int32 GetStringHashCode(String str)
        {
#if DISABLED    // String pool changed this behavior
            Debug.Assert(str.StartsWith("\""));
            Debug.Assert(str.EndsWith("\""));

            // Remove the first and last "
            str = str.Substring(1, str.Length - 2);

#warning GetStringHashCode doesn't handle correctly escape characters...
#endif

            // The assumption is that the hashcode is calculated at runtime with FNV1
            const UInt32 OFFSET_BASIS = 2166136261;
            const UInt32 FNV_PRIME = 16777619;

            UInt32 hashCode = OFFSET_BASIS;
            foreach (char c in str)
            {
                hashCode ^= (UInt32)c;
                UInt64 result = (UInt64)hashCode * (UInt64)FNV_PRIME;
                hashCode = (UInt32)(result & 0xffffffff);
            }

            if (hashCode == 0)
            {
                // Don't allow hash value of 0
                hashCode = 1;
            }
            unchecked
            {
                // Do the conversion from the unsigned type to the signed type in unchecked mode
                // Otherwise it is surprisingly a pain...
                return (Int32)(hashCode);
            }
        }

        public static string GetNextTempVariable()
        {
            string tempVar = "__temp" + tempVarCounter + "__";
            ++tempVarCounter;
            return (tempVar);
        }

        public static string GetNextAnonymousClass()
        {
            string tempVar = "__AnonymousClass" + tempAnonymousClass + "__";
            ++tempAnonymousClass;
            return (tempVar);
        }

        public static string GetNextAnonymousMethod()
        {
            string tempVar = "__AnonymousMethod" + tempAnonymousMethod + "__";
            ++tempAnonymousMethod;
            return (tempVar);
        }

        private static int tempVarCounter = 0;
        private static int tempAnonymousClass = 0;
        private static int tempAnonymousMethod = 0;
        private const string INTERFACE_CALL = "INTERFACE__CALL";
    }
}

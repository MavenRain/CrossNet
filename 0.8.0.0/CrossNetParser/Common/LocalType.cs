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

using CrossNet.Interfaces;
using CrossNet.Net;

namespace CrossNet.Common
{
    public class LocalType
    {
        public LocalType(string text)
        {
            mText = text;
            mPrimitiveType = false;
        }

        public LocalType(Type systemType, string text)
        {
            mSystemType = systemType;
            mText = text;
            mPrimitiveType = false;
        }

        public LocalType(Type systemType, string text, bool primitiveType)
        {
            mSystemType = systemType;
            mText = text;
            mPrimitiveType = primitiveType;
        }

        public LocalType(string text, IType embeddedType)
        {
            mText = text;
            // Set the property, this will set the base type flag as well...
            EmbeddedType = embeddedType;
        }

        public bool IsPrimitiveType
        {
            get
            {
                return (mPrimitiveType);
            }
        }

        public string FullName
        {
            get
            {
                return (mText);
            }
        }

        public IType EmbeddedType
        {
            get
            {
                return (mEmbeddedType);
            }
            set
            {
                if (object.ReferenceEquals(mEmbeddedType, value) == false)
                {
                    // In some cases, we actually set the same value several times
                    //  (when forcing a parsing the first time)
                    //Debug.Assert(mEmbeddedType == null, "The value should be set only one time!");
                }
                mEmbeddedType = value;

                // Here we need to update the base type state...
                if (mPrimitiveType)
                {
                    // Already marked, it means that we are fixing the value of embedded type
                    return;
                }
                ITypeInfo typeInfo = GetTypeInfo();
                if (typeInfo != null)
                {
                    if (typeInfo.Type == ObjectType.ENUM)
                    {
                        // Enums are compared as base type
                        mPrimitiveType = true;
                    }
                }
            }
        }

        /*
        public int CompareTo(object obj)
        {
            if (object.ReferenceEquals(this, obj))
            {
                // Same reference, same object...
                return (0);
            }
            IType type;
            if (obj is LocalType)
            {
                type = ((LocalType)obj).EmbeddedType;
            }
            else
            {
                type = obj as IType;
                if (type == null)
                {
                    throw new Exception("Incorrect comparison!");
                }
            }
            if (EmbeddedType != null)
            {
                return (EmbeddedType.CompareTo(type));
            }

            // There is no embedded type, we have to create one
            LocalType localType = LanguageManager.LocalTypeManager.GetLocalType(type);
            if (Same(localType))
            {
                return (0);
            }
            return (1);
        }
         */

        public ITypeInfo GetTypeInfo()
        {
            if (mTypeInfo == null)
            {
                if (mEmbeddedType == null)
                {
                    // For some reasons, the type has not been resolved yet
                    // Force a resolve by using the Provider
                    if (mSystemType != null)
                    {
                        mEmbeddedType = Provider.GetEmbeddedType(mSystemType);
                    }
                    Debug.Assert(mEmbeddedType != null);
                }
                mTypeInfo = TypeInfoManager.GetTypeInfo(mEmbeddedType);
            }
            return (mTypeInfo);
        }

        public bool Same(LocalType localType)
        {
            if (object.ReferenceEquals(this, localType))
            {
                // If same reference, they are definicitely the same
                return (true);
            }
            if ((EmbeddedType != null) && (localType.EmbeddedType != null))
            {
                ITypeReference typeReference = EmbeddedType as ITypeReference;
                ITypeReference localTypeReference = localType.EmbeddedType as ITypeReference;
                if ((typeReference != null) && (localTypeReference != null))
                {
                    return (Util.CompareTypeReference(typeReference, localTypeReference));
                }
/*
                if ((typeReference != null) && (localTypeReference != null))
                {
                    int numArgs = typeReference.GenericArguments.Count;
                    if (numArgs != 0)
                    {
                        if (numArgs == localTypeReference.GenericArguments.Count)
                        {
                            // If we are comparing type reference with generics,
                            // We actually need to have a full string comparison...
                            // The reason is that we might compare generic reference with generic declaration
                            // (like method return value and temp variable),
                            // and as such, Reflector might say that the values are actually different...
                            // TODO: Find a better and more efficient way

                            // This part is hacky, as we don't pass the declaring type, we don't clarify if we are within 
                            // a template declaration...
                            ParsingInfo info = new ParsingInfo(null);
                            string firstText = LanguageManager.ReferenceGenerator.GenerateCodeTypeReference(typeReference, info).Text;
                            string secondText = LanguageManager.ReferenceGenerator.GenerateCodeTypeReference(localTypeReference, info).Text;
                            // Check if the two types are the same
                            return (firstText == secondText);
                        }
                    }
                }
 */
            }
            // not the same reference, no embedded type with generic support...
            // They are definitively different
            return (false);
        }

        public override string ToString()
        {
            return (mText);
        }

        public override bool Equals(object obj)
        {
            LocalType other = obj as LocalType;
            if (other == null)
            {
                return (false);
            }
            return (Same(other));
        }

        public override int GetHashCode()
        {
            // Get the hash code from the EmbeddedType
            // If we don't have EmbeddedType yet, retrieve it from the provider...
            // This is important do have the correct hash code otherwise the hashtables will be initialized incorrectly
            if (EmbeddedType == null)
            {
                Debug.Assert(mSystemType != null);
                mEmbeddedType = Provider.GetEmbeddedType(mSystemType);
                Debug.Assert(mEmbeddedType != null);
            }
            if (EmbeddedType != null)
            {
                return (EmbeddedType.GetHashCode());
            }
            // Worst case, we are using the hash code of the text
            // But if this happens, there is big chance it will create a hard to find bug!
            // So at least assert if that even happens...
            Debug.Fail("");
            return (mText.GetHashCode());
        }

        private string mText;
        public IType mEmbeddedType;
        private ITypeInfo mTypeInfo;

        // mSystemType is used only on extremely rare cases
        // It happens when the code generator needs the Reflector type to work (ITypeInfo comparison usually),
        // but somehow the type has not been parsed yet. Even if LocalTypeManager has a list of those possible types,
        // The relation with reflector is done on the fly...
        // And in some case it can happen that the relation is not done yet when we need the type defined
        // The most common reason is because the type has not been a dependency of the assembly
        // Like it is not a member, or a base class of a class defined in the assembly
        // In that case, we are looking at all the assembly loaded and try to match the system type to the reflector type
        // The most common example, is a very simple assembly that contains a foreach statement
        // Generated C++ code needs definition of IEnumerator to work,
        // But the dependency might not define IEnumerator (even the generated statements by reflector might not reference it either).
        // We can see this kind of issue only with small or specific assemblies (like unit-test)
        // A more complex / real life assembly will simply not have this issue...
        private Type mSystemType;
        private bool mPrimitiveType = false;             // By default, not a base type
    }
}

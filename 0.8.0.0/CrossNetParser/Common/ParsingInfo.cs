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

namespace CrossNet.Common
{
    public enum MethodType
    {
        NORMAL,
        OPERATOR,
        OPERATOR_TRUE,
        OPERATOR_FALSE,
        OPERATOR_IMPLICIT,
        OPERATOR_EXPLICIT,
    }

    public enum PropertyType
    {
        NONE,
        ENABLE_GET,
        ENABLE_SET,
        SET_USED,
    };

    public enum VariableMode
    {
        NORMAL,
        REF,
        OUT,
        // Don't do the PARAMS yet...
    }

    public class VariableInfo
    {
        public VariableInfo(string type, string name, VariableMode mode)
        {
            Type = type;
            Name = name;
            Mode = mode;
        }

        public string       Type;
        public string       Name;
        public VariableMode Mode;
    }

    public class ParsingInfo
    {
        public bool UnsafeMethod = false;
        public Stack<IType> CurrentCast = null;
        public bool InStackAlloc = false;
        public bool ParsingField = false;
        public Stack<IType> CurrentGenericArgument = null;
        public MethodType MethodType;
        public string MethodName;
        public IType DeclaringType;
        public ITypeDeclaration BaseDeclaringType;
        public IMethodReference MethodReference;
        public bool InExtern = false;
        public bool InRefOrOut = false;
        public LocalType ReturnedType;
        public Stack<LocalType> CurrentSwitch = null;
        public Stack<PropertyType> CurrentPropertyType = null;
        public IDictionary<string, VariableInfo> Parameters = null;
        public int ArrayInitializerLevel = 0;
        public StringData FieldInitialization = null;
        public StringData StaticFieldInitialization = null;
        public bool InAssign = false;
        public bool InStructVariableDeclaration = false;
        public string ForcedReturnValue = null;
        public IDictionary<string, string> Labels = null;
        public bool InCase = false;
        public bool CombineStatementEnabled = true;
        public bool EnableTypenameForCodeType = true;

        // Code for anonymous variables
        public bool WithinAnonymousMethod = false;
        public string AnonymousMethodObject = null;
        public string AnonymousMethodClass = null;
        public bool AtLeastOneAnonymousMethod = false;

        public IDictionary<string, AnonymousVariable> Variables
        {
            get
            {
                return (mVariablesUsedInAnonymousMethods);
            }
        }

        public void UsePredefinedMember(string membername, Kind kind)
        {
            Debug.Assert((kind == Kind.This) || (kind == Kind.Base));

            if (WithinAnonymousMethod == false)
            {
                return;
            }

            if (mVariablesUsedInAnonymousMethods == null)
            {
                mVariablesUsedInAnonymousMethods = new Dictionary<string, AnonymousVariable>();
            }

            AnonymousVariable var;
            if (mVariablesUsedInAnonymousMethods.TryGetValue(membername, out var) == false)
            {
                // If it has not been declared already, that means it is declared outside
                var = new AnonymousVariable(membername, Declared.Outside, kind);
                mVariablesUsedInAnonymousMethods.Add(membername, var);
            }
        }

        public void UseVariable(string variableName, IVariableDeclaration varDeclaration)
        {
            if (WithinAnonymousMethod == false)
            {
                return;
            }

            if (mVariablesUsedInAnonymousMethods == null)
            {
                mVariablesUsedInAnonymousMethods = new Dictionary<string, AnonymousVariable>();
            }

            AnonymousVariable var;
            if (mVariablesUsedInAnonymousMethods.TryGetValue(variableName, out var) == false)
            {
                // If it has not been declared already, that means it is declared outside
                var = new AnonymousVariable(variableName, Declared.Outside, varDeclaration);
                mVariablesUsedInAnonymousMethods.Add(variableName, var);
            }
        }

        public void UseParameter(string parameterName, IParameterDeclaration parameterDeclaration)
        {
            if (WithinAnonymousMethod == false)
            {
                return;
            }

            if (mVariablesUsedInAnonymousMethods == null)
            {
                mVariablesUsedInAnonymousMethods = new Dictionary<string, AnonymousVariable>();
            }

            AnonymousVariable var;
            if (mVariablesUsedInAnonymousMethods.TryGetValue(parameterName, out var) == false)
            {
                // If it has not been declared already, that means it is declared outside
                var = new AnonymousVariable(parameterName, Declared.Outside, parameterDeclaration);
                mVariablesUsedInAnonymousMethods.Add(parameterName, var);
            }
        }

        public void DeclareVariable(string variableName, IVariableDeclaration varDeclaration)
        {
            if (WithinAnonymousMethod == false)
            {
                return;
            }
            if (mVariablesUsedInAnonymousMethods == null)
            {
                mVariablesUsedInAnonymousMethods = new Dictionary<string, AnonymousVariable>();
            }
            AnonymousVariable var = new AnonymousVariable(variableName, Declared.Inside, varDeclaration);
            // When the variable is declared the first time, it should not be already used ;)
            // So Add() should be safe...
            mVariablesUsedInAnonymousMethods[variableName] = var;
        }

        public void DeclareParameter(string parameterName, IParameterDeclaration parameterDeclaration)
        {
            if (WithinAnonymousMethod == false)
            {
                return;
            }
            if (mVariablesUsedInAnonymousMethods == null)
            {
                mVariablesUsedInAnonymousMethods = new Dictionary<string, AnonymousVariable>();
            }
            AnonymousVariable var = new AnonymousVariable(parameterName, Declared.Inside, parameterDeclaration);
            mVariablesUsedInAnonymousMethods[parameterName] = var;
        }

        public void ClearAnonymousVariables()
        {
            if (mVariablesUsedInAnonymousMethods != null)
            {
                mVariablesUsedInAnonymousMethods.Clear();
            }
            AnonymousMethodObject = null;
            AnonymousMethodClass = null;
        }

        // Code for finally statements
#if DISABLED    // Doesn't handle completely finally
        private Stack<StringData> mFinallyStatements = null;

        public void PushFinallyStatement(StringData finallyStatement)
        {
            if (mFinallyStatements == null)
            {
                mFinallyStatements = new Stack<StringData>();
            }
            mFinallyStatements.Push(finallyStatement);
        }

        public void PopFinallyStatement()
        {
            mFinallyStatements.Pop();
        }

        public StringData RetrieveCurrentFinallyStatement
#endif

//#if DEBUG
        public string DebugTypeFullName;
        public string DebugMethodName;
//#endif

        public ParsingInfo(IType declaringType)
        {
            DeclaringType = declaringType;
            ITypeDeclaration typeDeclaration = declaringType as ITypeDeclaration;
            if (typeDeclaration != null)
            {
                BaseDeclaringType = typeDeclaration;
            }

//#if DEBUG
            ITypeInfo type = TypeInfoManager.GetTypeInfo(declaringType);
            if (type != null)
            {
                DebugTypeFullName = type.FullName;
            }
//#endif
        }

        public void ResetBetweenStatements()
        {
            // Make sure that everything local is reset correctly...
            if (CurrentCast != null)
            {
                Debug.Assert(CurrentCast.Count == 0);
            }
            Debug.Assert(InStackAlloc == false);
            Debug.Assert(ParsingField == false);
            if (CurrentGenericArgument != null)
            {
                Debug.Assert(CurrentGenericArgument.Count == 0);
            }

            MethodType = MethodType.NORMAL;
            MethodName = "";
            InAssign = false;
            InStructVariableDeclaration = false;

            if (mStackOfInterfaceCalls != null)
            {
                Debug.Assert(mStackOfInterfaceCalls.Count == 0);
            }
            if (mStackOfPropertyIndexerTarget != null)
            {
                Debug.Assert(mStackOfPropertyIndexerTarget.Count == 0);
            }

/*
 * See if this is a valid assertion...
 * 
            if (CurrentPropertyType != null)
            {
                Debug.Assert(CurrentPropertyType.Count == 0);
            }
 */
        }

        public void AddToPreStatements(StringData data)
        {
            if (mExpressionPreStatements == null)
            {
                mExpressionPreStatements = new List<StringData>();
            }
            mExpressionPreStatements.Add(data);
        }

        private void AddToPreStatements(ICollection<StringData> data)
        {
            if ((data == null) || (data.Count == 0))
            {
                return;
            }
            if (mExpressionPreStatements == null)
            {
                mExpressionPreStatements = new List<StringData>();
            }
            mExpressionPreStatements.AddRange(data);
        }

        public void AddToPostStatements(StringData data)
        {
            if (mExpressionPostStatements == null)
            {
                mExpressionPostStatements = new List<StringData>();
            }
            mExpressionPostStatements.Add(data);
        }

        private void AddToPostStatements(ICollection<StringData> data)
        {
            if ((data == null) || (data.Count == 0))
            {
                return;
            }
            if (mExpressionPostStatements == null)
            {
                mExpressionPostStatements = new List<StringData>();
            }
            mExpressionPostStatements.AddRange(data);
        }

        public StringData[] GetPreStatements()
        {
            if (mExpressionPreStatements == null)
            {
                return (null);
            }
            if (mExpressionPreStatements.Count == 0)
            {
                return (null);
            }
            return (mExpressionPreStatements.ToArray());
        }

        public StringData[] GetPostStatements()
        {
            if (mExpressionPostStatements == null)
            {
                return (null);
            }
            if (mExpressionPostStatements.Count == 0)
            {
                return (null);
            }
            return (mExpressionPostStatements.ToArray());
        }

        public StatementState RetrieveStatementState()
        {
            return (new StatementState(RetrievePreStatements(), RetrievePostStatements()));
        }

        public void AddStatementState(StatementState state)
        {
            AddToPreStatements(state.PreStatements);
            AddToPreStatements(state.PostStatements);
        }

        private StringData[] RetrievePreStatements()
        {
            StringData[] data = GetPreStatements();
            ClearPreStatements();
            return (data);
        }

        private StringData[] RetrievePostStatements()
        {
            StringData[] data = GetPostStatements();
            ClearPostStatements();
            return (data);
        }

        public void ClearPreStatements()
        {
            if (mExpressionPreStatements != null)
            {
                mExpressionPreStatements.Clear();
            }
        }

        public void ClearPostStatements()
        {
            if (mExpressionPostStatements != null)
            {
                mExpressionPostStatements.Clear();
            }
        }

        public void PushLazyPropertyGet()
        {
            if (CurrentPropertyType == null)
            {
                return;
            }
            CurrentPropertyType.Push(PropertyType.ENABLE_GET);
        }

        public void PopLazyPropertyGet()
        {
            if (CurrentPropertyType == null)
            {
                return;
            }
            PropertyType usedProperty = CurrentPropertyType.Pop();
            Debug.Assert(usedProperty == PropertyType.ENABLE_GET);
        }

        public void PushInterfaceCall(bool value)
        {
            if (mStackOfInterfaceCalls == null)
            {
                mStackOfInterfaceCalls = new Stack<bool>();
            }
            mStackOfInterfaceCalls.Push(value);
        }

        public bool PopInterfaceCall()
        {
            return (mStackOfInterfaceCalls.Pop());
        }

        public void PushPropertyIndexerTarget(string indexerTarget)
        {
            if (mStackOfPropertyIndexerTarget == null)
            {
                mStackOfPropertyIndexerTarget = new Stack<string>();
            }
            mStackOfPropertyIndexerTarget.Push(indexerTarget);
        }

        public string PopPropertyIndexerTarget()
        {
            return (mStackOfPropertyIndexerTarget.Pop());
        }

        private List<StringData> mExpressionPreStatements = null;
        private List<StringData> mExpressionPostStatements = null;
        private Stack<bool> mStackOfInterfaceCalls = null;
        private Stack<string> mStackOfPropertyIndexerTarget = null;
        private IDictionary<string, AnonymousVariable> mVariablesUsedInAnonymousMethods = null;
    }

    public struct StatementState
    {
        public StatementState(StringData[] preStatements, StringData[] postStatements)
        {
            PreStatements = preStatements;
            PostStatements = postStatements;
        }

        public StringData[] PreStatements;
        public StringData[] PostStatements;
    }

    public enum Declared
    {
        Inside,
        Outside,
    }

    public enum Kind
    {
        LocalVariable,
        Parameter,
        This,
        Base,
    }

    public class AnonymousVariable
    {
        public AnonymousVariable(string name, Declared declared, IParameterDeclaration parameterDeclaration)
        {
            OldName = NewName = name;
            mDeclared = declared;
            mKind = Kind.Parameter;
            UsedInside = true;
            ParameterDeclaration = parameterDeclaration;
        }

        public AnonymousVariable(string name, Declared declared, IVariableDeclaration varDeclaration)
        {
            OldName = NewName = name;
            mDeclared = declared;
            mKind = Kind.LocalVariable;
            UsedInside = true;
            VariableDeclaration = varDeclaration;
        }

        public AnonymousVariable(string name, Declared declared, Kind kind)
        {
            OldName = NewName = name;
            mDeclared = declared;
            mKind = kind;
            UsedInside = true;
        }

        public Declared Declared
        {
            get
            {
                return (mDeclared);
            }
        }

        public Kind Kind
        {
            get
            {
                return (mKind);
            }
        }

        public bool UsedInside;
        public string OldName;
        public string NewName;
        public IVariableDeclaration VariableDeclaration;
        public IParameterDeclaration ParameterDeclaration;

        private Declared mDeclared;
        private Kind mKind;
    }
}

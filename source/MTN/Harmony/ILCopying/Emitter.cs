using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

namespace Harmony.ILCopying
{
	/// <summary>A leave try</summary>
	public class LeaveTry
	{
		/// <summary>Returns a string that represents the current object</summary>
		/// <returns>A string that represents the current object</returns>
		///
		public override string ToString()
		{
			return "(autogenerated)";
		}
	}

	/// <summary>An emitter</summary>
	public static class Emitter
	{
		static readonly GetterHandler codeLenGetter = FastAccess.CreateFieldGetter(typeof(ILGenerator), "code_len", "m_length");
		static readonly GetterHandler localsGetter = FastAccess.CreateFieldGetter(typeof(ILGenerator), "locals");
		static readonly GetterHandler localCountGetter = FastAccess.CreateFieldGetter(typeof(ILGenerator), "m_localCount");

		/// <summary>Code position</summary>
		/// <param name="il">The il</param>
		/// <returns>A string</returns>
		///
		public static string CodePos(ILGenerator il)
		{
			var offset = (int)codeLenGetter(il);
			return string.Format("L_{0:x4}: ", offset);
		}

		/// <summary>Logs an il</summary>
		/// <param name="il">		The il</param>
		/// <param name="opCode">  The operation code</param>
		/// <param name="argument">The argument</param>
		///
		public static void LogIL(ILGenerator il, OpCode opCode, object argument)
		{
			if (HarmonyInstance.DEBUG)
			{
				var argStr = FormatArgument(argument);
				var space = argStr.Length > 0 ? " " : "";
				FileLog.LogBuffered(string.Format("{0}{1}{2}{3}", CodePos(il), opCode, space, argStr));
			}
		}

		/// <summary>Logs local variable</summary>
		/// <param name="il">		The il</param>
		/// <param name="variable">The variable</param>
		///
		public static void LogLocalVariable(ILGenerator il, LocalBuilder variable)
		{
			if (HarmonyInstance.DEBUG)
			{
				var localCount = -1;
				var localsArray = localsGetter != null ? (LocalBuilder[])localsGetter(il) : null;
				if (localsArray != null && localsArray.Length > 0)
					localCount = localsArray.Length;
				else
					localCount = (int)localCountGetter(il);

				var str = string.Format("{0}Local var {1}: {2}{3}", CodePos(il), localCount - 1, variable.LocalType.FullName, variable.IsPinned ? "(pinned)" : "");
				FileLog.LogBuffered(str);
			}
		}

		/// <summary>Format argument</summary>
		/// <param name="argument">The argument</param>
		/// <returns>The formatted argument</returns>
		///
		public static string FormatArgument(object argument)
		{
			if (argument == null) return "NULL";
			var type = argument.GetType();

			if (type == typeof(string))
				return "\"" + argument + "\"";
			if (type == typeof(Label))
				return "Label" + ((Label)argument).GetHashCode();
			if (type == typeof(Label[]))
				return "Labels" + string.Join(",", ((Label[])argument).Select(l => l.GetHashCode().ToString()).ToArray());
			if (type == typeof(LocalBuilder))
				return ((LocalBuilder)argument).LocalIndex + " (" + ((LocalBuilder)argument).LocalType + ")";

			return argument.ToString().Trim();
		}

		/// <summary>Mark label</summary>
		/// <param name="il">	The il</param>
		/// <param name="label">The label</param>
		///
		public static void MarkLabel(ILGenerator il, Label label)
		{
			if (HarmonyInstance.DEBUG) FileLog.LogBuffered(CodePos(il) + FormatArgument(label));
			il.MarkLabel(label);
		}

		/// <summary>Mark block before</summary>
		/// <param name="il">	The il</param>
		/// <param name="block">The block</param>
		/// <param name="label">[out] The label</param>
		///
		public static void MarkBlockBefore(ILGenerator il, ExceptionBlock block, out Label? label)
		{
			label = null;
			switch (block.blockType)
			{
				case ExceptionBlockType.BeginExceptionBlock:
					if (HarmonyInstance.DEBUG)
					{
						FileLog.LogBuffered(".try");
						FileLog.LogBuffered("{");
						FileLog.ChangeIndent(1);
					}
					label = il.BeginExceptionBlock();
					return;

				case ExceptionBlockType.BeginCatchBlock:
					if (HarmonyInstance.DEBUG)
					{
						// fake log a LEAVE code since BeginCatchBlock() does add it
						LogIL(il, OpCodes.Leave, new LeaveTry());

						FileLog.ChangeIndent(-1);
						FileLog.LogBuffered("} // end try");

						FileLog.LogBuffered(".catch " + block.catchType);
						FileLog.LogBuffered("{");
						FileLog.ChangeIndent(1);
					}
					il.BeginCatchBlock(block.catchType);
					return;

				case ExceptionBlockType.BeginExceptFilterBlock:
					if (HarmonyInstance.DEBUG)
					{
						// fake log a LEAVE code since BeginCatchBlock() does add it
						LogIL(il, OpCodes.Leave, new LeaveTry());

						FileLog.ChangeIndent(-1);
						FileLog.LogBuffered("} // end try");

						FileLog.LogBuffered(".filter");
						FileLog.LogBuffered("{");
						FileLog.ChangeIndent(1);
					}
					il.BeginExceptFilterBlock();
					return;

				case ExceptionBlockType.BeginFaultBlock:
					if (HarmonyInstance.DEBUG)
					{
						// fake log a LEAVE code since BeginCatchBlock() does add it
						LogIL(il, OpCodes.Leave, new LeaveTry());

						FileLog.ChangeIndent(-1);
						FileLog.LogBuffered("} // end try");

						FileLog.LogBuffered(".fault");
						FileLog.LogBuffered("{");
						FileLog.ChangeIndent(1);
					}
					il.BeginFaultBlock();
					return;

				case ExceptionBlockType.BeginFinallyBlock:
					if (HarmonyInstance.DEBUG)
					{
						// fake log a LEAVE code since BeginCatchBlock() does add it
						LogIL(il, OpCodes.Leave, new LeaveTry());

						FileLog.ChangeIndent(-1);
						FileLog.LogBuffered("} // end try");

						FileLog.LogBuffered(".finally");
						FileLog.LogBuffered("{");
						FileLog.ChangeIndent(1);
					}
					il.BeginFinallyBlock();
					return;
			}
		}

		/// <summary>Mark block after</summary>
		/// <param name="il">	The il</param>
		/// <param name="block">The block</param>
		///
		public static void MarkBlockAfter(ILGenerator il, ExceptionBlock block)
		{
			if (block.blockType == ExceptionBlockType.EndExceptionBlock)
			{
				if (HarmonyInstance.DEBUG)
				{
					// fake log a LEAVE code since BeginCatchBlock() does add it
					LogIL(il, OpCodes.Leave, new LeaveTry());

					FileLog.ChangeIndent(-1);
					FileLog.LogBuffered("} // end handler");
				}
				il.EndExceptionBlock();
			}
		}

		/// <summary>MethodCopier calls when Operand type is InlineNone</summary>
		/// <param name="il">	 The il</param>
		/// <param name="opcode">The opcode</param>
		///
		public static void Emit(ILGenerator il, OpCode opcode)
		{
			if (HarmonyInstance.DEBUG) FileLog.LogBuffered(CodePos(il) + opcode);
			il.Emit(opcode);
		}

		/// <summary>MethodCopier calls by 3rd argument type</summary>
		/// <param name="il">	 The il</param>
		/// <param name="opcode">The opcode</param>
		/// <param name="local"> The local</param>
		///
		public static void Emit(ILGenerator il, OpCode opcode, LocalBuilder local)
		{
			LogIL(il, opcode, local);
			il.Emit(opcode, local);
		}

		/// <summary>MethodCopier calls by 3rd argument type</summary>
		/// <param name="il">	 The il</param>
		/// <param name="opcode">The opcode</param>
		/// <param name="field"> The field</param>
		///
		public static void Emit(ILGenerator il, OpCode opcode, FieldInfo field)
		{
			LogIL(il, opcode, field);
			il.Emit(opcode, field);
		}

		/// <summary>MethodCopier calls by 3rd argument type</summary>
		/// <param name="il">	 The il</param>
		/// <param name="opcode">The opcode</param>
		/// <param name="labels">The labels</param>
		///
		public static void Emit(ILGenerator il, OpCode opcode, Label[] labels)
		{
			LogIL(il, opcode, labels);
			il.Emit(opcode, labels);
		}

		/// <summary>MethodCopier calls by 3rd argument type</summary>
		/// <param name="il">	 The il</param>
		/// <param name="opcode">The opcode</param>
		/// <param name="label"> The label</param>
		///
		public static void Emit(ILGenerator il, OpCode opcode, Label label)
		{
			LogIL(il, opcode, label);
			il.Emit(opcode, label);
		}

		/// <summary>MethodCopier calls by 3rd argument type</summary>
		/// <param name="il">	 The il</param>
		/// <param name="opcode">The opcode</param>
		/// <param name="str">	 The string</param>
		///
		public static void Emit(ILGenerator il, OpCode opcode, string str)
		{
			LogIL(il, opcode, str);
			il.Emit(opcode, str);
		}

		/// <summary>MethodCopier calls by 3rd argument type</summary>
		/// <param name="il">	 The il</param>
		/// <param name="opcode">The opcode</param>
		/// <param name="arg">	 The argument</param>
		///
		public static void Emit(ILGenerator il, OpCode opcode, float arg)
		{
			LogIL(il, opcode, arg);
			il.Emit(opcode, arg);
		}

		/// <summary>MethodCopier calls by 3rd argument type</summary>
		/// <param name="il">	 The il</param>
		/// <param name="opcode">The opcode</param>
		/// <param name="arg">	 The argument</param>
		///
		public static void Emit(ILGenerator il, OpCode opcode, byte arg)
		{
			LogIL(il, opcode, arg);
			il.Emit(opcode, arg);
		}

		/// <summary>MethodCopier calls by 3rd argument type</summary>
		/// <param name="il">	 The il</param>
		/// <param name="opcode">The opcode</param>
		/// <param name="arg">	 The argument</param>
		///
		public static void Emit(ILGenerator il, OpCode opcode, sbyte arg)
		{
			LogIL(il, opcode, arg);
			il.Emit(opcode, arg);
		}

		/// <summary>MethodCopier calls by 3rd argument type</summary>
		/// <param name="il">	 The il</param>
		/// <param name="opcode">The opcode</param>
		/// <param name="arg">	 The argument</param>
		///
		public static void Emit(ILGenerator il, OpCode opcode, double arg)
		{
			LogIL(il, opcode, arg);
			il.Emit(opcode, arg);
		}

		/// <summary>MethodCopier calls by 3rd argument type</summary>
		/// <param name="il">	 The il</param>
		/// <param name="opcode">The opcode</param>
		/// <param name="arg">	 The argument</param>
		///
		public static void Emit(ILGenerator il, OpCode opcode, int arg)
		{
			LogIL(il, opcode, arg);
			il.Emit(opcode, arg);
		}

		/// <summary>MethodCopier calls by 3rd argument type</summary>
		/// <param name="il">	 The il</param>
		/// <param name="opcode">The opcode</param>
		/// <param name="meth">  The meth</param>
		///
		public static void Emit(ILGenerator il, OpCode opcode, MethodInfo meth)
		{
			LogIL(il, opcode, meth);
			il.Emit(opcode, meth);
		}

		/// <summary>MethodCopier calls by 3rd argument type</summary>
		/// <param name="il">	 The il</param>
		/// <param name="opcode">The opcode</param>
		/// <param name="arg">	 The argument</param>
		///
		public static void Emit(ILGenerator il, OpCode opcode, short arg)
		{
			LogIL(il, opcode, arg);
			il.Emit(opcode, arg);
		}

		/// <summary>MethodCopier calls by 3rd argument type</summary>
		/// <param name="il">		 The il</param>
		/// <param name="opcode">	 The opcode</param>
		/// <param name="signature">The signature</param>
		///
		public static void Emit(ILGenerator il, OpCode opcode, SignatureHelper signature)
		{
			LogIL(il, opcode, signature);
			il.Emit(opcode, signature);
		}

		/// <summary>MethodCopier calls by 3rd argument type</summary>
		/// <param name="il">	 The il</param>
		/// <param name="opcode">The opcode</param>
		/// <param name="con">	 The con</param>
		///
		public static void Emit(ILGenerator il, OpCode opcode, ConstructorInfo con)
		{
			LogIL(il, opcode, con);
			il.Emit(opcode, con);
		}

		/// <summary>MethodCopier calls by 3rd argument type</summary>
		/// <param name="il">	 The il</param>
		/// <param name="opcode">The opcode</param>
		/// <param name="cls">	 The cls</param>
		///
		public static void Emit(ILGenerator il, OpCode opcode, Type cls)
		{
			LogIL(il, opcode, cls);
			il.Emit(opcode, cls);
		}

		/// <summary>MethodCopier calls by 3rd argument type</summary>
		/// <param name="il">	 The il</param>
		/// <param name="opcode">The opcode</param>
		/// <param name="arg">	 The argument</param>
		///
		public static void Emit(ILGenerator il, OpCode opcode, long arg)
		{
			LogIL(il, opcode, arg);
			il.Emit(opcode, arg);
		}

		/// <summary>called from MethodInvoker (calls from MethodCopier use the corresponding Emit() call above)</summary>
		/// <param name="il">						  The il</param>
		/// <param name="opcode">					  The opcode</param>
		/// <param name="methodInfo">				  Information describing the method</param>
		/// <param name="optionalParameterTypes">List of types of the optional parameters</param>
		///
		public static void EmitCall(ILGenerator il, OpCode opcode, MethodInfo methodInfo, Type[] optionalParameterTypes)
		{
			if (HarmonyInstance.DEBUG) FileLog.LogBuffered(string.Format("{0}Call {1} {2} {3}", CodePos(il), opcode, methodInfo, optionalParameterTypes));
			il.EmitCall(opcode, methodInfo, optionalParameterTypes);
		}

		/// <summary>not called yet</summary>
		/// <param name="il">					The il</param>
		/// <param name="opcode">				The opcode</param>
		/// <param name="unmanagedCallConv">The unmanaged call convert</param>
		/// <param name="returnType">			Type of the return</param>
		/// <param name="parameterTypes">	List of types of the parameters</param>
		///
		public static void EmitCalli(ILGenerator il, OpCode opcode, CallingConvention unmanagedCallConv, Type returnType, Type[] parameterTypes)
		{
			if (HarmonyInstance.DEBUG) FileLog.LogBuffered(string.Format("{0}Calli {1} {2} {3} {4}", CodePos(il), opcode, unmanagedCallConv, returnType, parameterTypes));
			il.EmitCalli(opcode, unmanagedCallConv, returnType, parameterTypes);
		}

		/// <summary>not called yet</summary>
		/// <param name="il">						  The il</param>
		/// <param name="opcode">					  The opcode</param>
		/// <param name="callingConvention">	  The calling convention</param>
		/// <param name="returnType">				  Type of the return</param>
		/// <param name="parameterTypes">		  List of types of the parameters</param>
		/// <param name="optionalParameterTypes">List of types of the optional parameters</param>
		///
		public static void EmitCalli(ILGenerator il, OpCode opcode, CallingConventions callingConvention, Type returnType, Type[] parameterTypes, Type[] optionalParameterTypes)
		{
			if (HarmonyInstance.DEBUG) FileLog.LogBuffered(string.Format("{0}Calli {1} {2} {3} {4} {5}", CodePos(il), opcode, callingConvention, returnType, parameterTypes, optionalParameterTypes));
			il.EmitCalli(opcode, callingConvention, returnType, parameterTypes, optionalParameterTypes);
		}
	}
}
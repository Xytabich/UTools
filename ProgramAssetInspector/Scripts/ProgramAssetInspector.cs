using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEditor;
using UnityEngine;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.ProgramSources;
using VRC.Udon.VM.Common;

#if UDON_DEBUG
using VRC.Udon.Serialization.OdinSerializer;
#endif

namespace IL2UPP.Editor
{
	[CustomEditor(typeof(SerializedUdonProgramAsset))]
	public class ProgramAssetInspector : UnityEditor.Editor
	{
		private SerializedProperty _serializedProgramBytesStringSerializedProperty;
		private SerializedProperty _serializationDataFormatSerializedProperty;

		private bool showProgram = false;
		private IUdonProgram program = null;
		private string variablesAddresses;
		private string variablesNames;
		private string disassemblyAddresses;
		private List<GUIContent> disassembly = new List<GUIContent>();

		void OnEnable()
		{
			_serializedProgramBytesStringSerializedProperty = serializedObject.FindProperty("serializedProgramBytesString");
			_serializationDataFormatSerializedProperty = serializedObject.FindProperty("serializationDataFormat");
		}

		public override void OnInspectorGUI()
		{
			if(showProgram != EditorGUILayout.Foldout(showProgram, "Show program"))
			{
				showProgram = !showProgram;
				if(showProgram)
				{
					program = (target as SerializedUdonProgramAsset).RetrieveProgram();
					var symbols = program.SymbolTable;

					var cachedSb = new StringBuilder();
					var cachedSb2 = new StringBuilder();

					var heapDump = new List<(uint address, IStrongBox strongBoxedObject, Type objectType)>();
					var heap = program.Heap;
					heap.DumpHeapObjects(heapDump);
					heapDump.Sort((a, b) => a.address.CompareTo(b.address));

					for(var i = 0; i < heapDump.Count; i++)
					{
						var address = heapDump[i].address;
						cachedSb.AppendFormat("0x{0:X8}\n", address);
						cachedSb2.AppendLine(symbols.HasSymbolForAddress(address) ? symbols.GetSymbolFromAddress(address) : "[Unknown]");
					}

					variablesAddresses = cachedSb.ToString();
					variablesNames = cachedSb2.ToString();

					cachedSb2.Clear();
					disassembly.Clear();
					var publicMethods = program.EntryPoints;
					var bytes = program.ByteCode;
					uint index = 0;
					while(index < bytes.Length)
					{
						if(publicMethods.TryGetSymbolFromAddress(index, out string name))
						{
							cachedSb2.AppendLine();
							disassembly.Add(new GUIContent(string.Format("{0}:", name)));
						}
						uint address = index;
						uint variableAddress;
						OpCode op = (OpCode)UIntFromBytes(bytes, index);
						index += 4;
						cachedSb.Clear();
						cachedSb.Append(op);
						GUIContent content;
						switch(op)
						{
							case OpCode.PUSH:
								cachedSb.Append(", ");
								variableAddress = UIntFromBytes(bytes, index);
								cachedSb.AppendFormat("<i>{0}</i>", symbols.HasSymbolForAddress(variableAddress) ? symbols.GetSymbolFromAddress(variableAddress) : "[Unknown]");
								index += 4;
								content = new GUIContent(cachedSb.ToString(), string.Format("0x{0:X8}", variableAddress));
								break;
							case OpCode.EXTERN:
								cachedSb.Append(", \"");
								cachedSb.Append(heap.GetHeapVariable(UIntFromBytes(bytes, index)));
								cachedSb.Append("\"");
								index += 4;
								content = new GUIContent(cachedSb.ToString());
								break;
							case OpCode.JUMP:
							case OpCode.JUMP_IF_FALSE:
								cachedSb.Append(", ");
								cachedSb.AppendFormat("0x{0:X8}", UIntFromBytes(bytes, index));
								index += 4;
								content = new GUIContent(cachedSb.ToString());
								break;
							case OpCode.JUMP_INDIRECT:
								cachedSb.Append(", ");
								variableAddress = UIntFromBytes(bytes, index);
								cachedSb.AppendFormat("<i>{0}</i>", symbols.HasSymbolForAddress(variableAddress) ? symbols.GetSymbolFromAddress(variableAddress) : "[Unknown]");
								index += 4;
								content = new GUIContent(cachedSb.ToString(), string.Format("0x{0:X8}", variableAddress));
								break;
							default:
								content = new GUIContent(cachedSb.ToString());
								break;
						}
						cachedSb2.AppendFormat("0x{0:X8}\n", address);
						disassembly.Add(content);
					}
					disassemblyAddresses = cachedSb2.ToString();
				}
				else disassembly.Clear();
			}
			if(showProgram)
			{
				if(program != null)
				{
					GUILayout.Label("Variables:", EditorStyles.boldLabel);
					EditorGUILayout.BeginHorizontal();
					GUILayout.Label(variablesAddresses);
					GUILayout.Label(variablesNames);
					GUILayout.FlexibleSpace();
					EditorGUILayout.EndHorizontal();

					var style = new GUIStyle(EditorStyles.label);
					style.border = RemoveVertical(style.border);
					style.margin = RemoveVertical(style.margin);
					style.padding = RemoveVertical(style.padding);
					style.richText = true;
					var height = style.lineHeight;
					var heightProp = GUILayout.Height(height);

					GUILayout.Label("Program:", EditorStyles.boldLabel);
					EditorGUILayout.BeginHorizontal();
					GUILayout.Label(disassemblyAddresses, style);
					EditorGUILayout.BeginVertical();
					for(var i = 0; i < disassembly.Count; i++)
					{
						GUILayout.Label(disassembly[i], style, heightProp);
					}
					EditorGUILayout.EndVertical();
					GUILayout.FlexibleSpace();
					EditorGUILayout.EndHorizontal();
				}
				else showProgram = false;
			}

#if UDON_DEBUG
			DrawSerializationDebug();
#endif
		}

		private RectOffset RemoveVertical(RectOffset ro)
		{
			ro.top = 0;
			ro.bottom = 0;
			return ro;
		}

#if UDON_DEBUG
		private void DrawSerializationDebug()
		{
			EditorGUILayout.LabelField($"DataFormat: {(DataFormat)_serializationDataFormatSerializedProperty.enumValueIndex}");

			if(string.IsNullOrEmpty(_serializedProgramBytesStringSerializedProperty.stringValue))
			{
				return;
			}

			if(_serializationDataFormatSerializedProperty.enumValueIndex == (int)DataFormat.JSON)
			{
				using(new EditorGUI.DisabledScope(true))
				{
					EditorGUILayout.TextArea(System.Text.Encoding.UTF8.GetString(System.Convert.FromBase64String(_serializedProgramBytesStringSerializedProperty.stringValue)));
				}
			}
			else
			{
				using(new EditorGUI.DisabledScope(true))
				{
					SerializedUdonProgramAsset serializedUdonProgramAsset = (SerializedUdonProgramAsset)target;
					IUdonProgram udonProgram = serializedUdonProgramAsset.RetrieveProgram();
					byte[] serializedBytes = SerializationUtility.SerializeValue(udonProgram, DataFormat.JSON, out List<UnityEngine.Object> _);
					EditorGUILayout.TextArea(System.Text.Encoding.UTF8.GetString(serializedBytes));
				}
			}
		}
#endif

		private static uint UIntFromBytes(byte[] bytes, uint startIndex)
		{
			return (uint)((bytes[startIndex] << 24) + (bytes[startIndex + 1] << 16) + (bytes[startIndex + 2] << 8) + bytes[startIndex + 3]);
		}
	}
}
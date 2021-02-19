using System.Collections;
using UnityEditor;
using System;
using System.Reflection;
using VRC.Udon.ProgramSources;
using System.Collections.Generic;

namespace IL2UPP.Editor
{
	public static class EditorReplacer
	{
		[InitializeOnLoadMethod]
		private static void Init()
		{
			var metType = typeof(CustomEditor).Assembly.GetType("UnityEditor.CustomEditorAttributes+MonoEditorType");
			var attributesCollection = new TypeInfo(typeof(CustomEditor).Assembly.GetType("UnityEditor.CustomEditorAttributes"));
			if(!attributesCollection.GetStaticField<bool>("s_Initialized"))
			{
				attributesCollection.GetStaticMethod<Type, bool, Type>("FindCustomEditorTypeByType")(null, false);
			}
			var types = attributesCollection.GetStaticField<IDictionary>("kSCustomEditors").value;
			var editors = (IList)types[typeof(SerializedUdonProgramAsset)];
			foreach(var editor in editors)
			{
				var info = new TypeInfo(metType, editor);
				if(info.GetInstanceField<Type>("m_InspectorType") != typeof(ProgramAssetInspector))
				{
					var isFallback = info.GetInstanceField<bool>("m_IsFallback");
					isFallback.value = true;
				}
			}
		}

		private struct FieldProp<T>
		{
			public T value
			{
				get => (T)field.GetValue(instance);
				set
				{
					if(field.IsInitOnly) throw new InvalidOperationException("Cannot write to read-only field");
					field.SetValue(instance, value);
				}
			}

			private FieldInfo field;
			private object instance;

			public FieldProp(FieldInfo field, object instance = null)
			{
				this.field = field;
				this.instance = instance;
			}

			public static implicit operator T(FieldProp<T> field)
			{
				return field.value;
			}
		}

		private class TypeInfo
		{
			private Type type;
			private object instance;

			private Dictionary<string, object> instanceMembersCache = null;
			private Dictionary<string, object> staticMembersCache = null;

			public TypeInfo(Type type, object instance = null)
			{
				this.type = type;
				this.instance = instance;
			}

			public FieldProp<T> GetInstanceField<T>(string name)
			{
				if(instanceMembersCache == null || !instanceMembersCache.TryGetValue(name, out var member))
				{
					if(instanceMembersCache == null) instanceMembersCache = new Dictionary<string, object>();

					member = new FieldProp<T>(type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic), instance);
					instanceMembersCache[name] = member;
				}
				return (FieldProp<T>)member;
			}

			public FieldProp<T> GetStaticField<T>(string name)
			{
				if(staticMembersCache == null || !staticMembersCache.TryGetValue(name, out var member))
				{
					if(staticMembersCache == null) staticMembersCache = new Dictionary<string, object>();

					member = new FieldProp<T>(type.GetField(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic), null);
					staticMembersCache[name] = member;
				}
				return (FieldProp<T>)member;
			}

			public Func<Ti0, Ti1, To> GetStaticMethod<Ti0, Ti1, To>(string name)
			{
				if(staticMembersCache == null || !staticMembersCache.TryGetValue(name, out var member))
				{
					if(staticMembersCache == null) staticMembersCache = new Dictionary<string, object>();

					member = Delegate.CreateDelegate(typeof(Func<Ti0, Ti1, To>), type.GetMethod(name,
						BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null,
						new Type[] { typeof(Ti0), typeof(Ti1) }, null));
					staticMembersCache[name] = member;
				}
				return (Func<Ti0, Ti1, To>)member;
			}

			public static TypeInfo CtorInstance(Type type)
			{
				var instance = type.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null).Invoke(new object[0]);
				return new TypeInfo(type, instance);
			}
		}
	}
}
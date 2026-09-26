using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEngine;

namespace ValheimArena.Core
{
    /// <summary>
    /// JSON мода — Newtonsoft.Json из поставки игры. JsonUtility здесь не годится: вложенные классы из сборки
    /// плагина (состав, атаки и сопротивления профиля, правила в состоянии арены) он молча пропускает и при записи,
    /// и при чтении. Правила те же, что у JsonUtility: только поля (открытые и [SerializeField]), без свойств;
    /// поле, которого нет в тексте, и null в тексте оставляют значение по умолчанию.
    /// </summary>
    internal static class JsonIO
    {
        private sealed class FieldsOnly : DefaultContractResolver
        {
            protected override List<MemberInfo> GetSerializableMembers(Type type)
            {
                var list = new List<MemberInfo>();
                foreach (var f in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (f.IsNotSerialized || f.IsInitOnly) continue;
                    if (f.IsPublic || f.IsDefined(typeof(SerializeField), true)) list.Add(f);
                }
                return list;
            }

            protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
            {
                var p = base.CreateProperty(member, memberSerialization);
                p.Readable = true;
                p.Writable = true;
                return p;
            }
        }

        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new FieldsOnly(),
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            Culture = CultureInfo.InvariantCulture
        };

        public static string ToJson(object o, bool pretty = false) =>
            JsonConvert.SerializeObject(o, pretty ? Formatting.Indented : Formatting.None, Settings);

        public static T FromJson<T>(string json) where T : class =>
            string.IsNullOrEmpty(json) ? null : JsonConvert.DeserializeObject<T>(json, Settings);
    }
}

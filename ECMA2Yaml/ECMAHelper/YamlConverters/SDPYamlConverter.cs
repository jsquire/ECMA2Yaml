﻿using ECMA2Yaml.Models;
using ECMA2Yaml.Models.SDP;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECMA2Yaml
{
    public partial class SDPYamlConverter
    {
        private readonly ECMAStore _store;

        public Dictionary<string, ItemSDPModelBase> NamespacePages { get; } = new Dictionary<string, ItemSDPModelBase>();
        public Dictionary<string, ItemSDPModelBase> TypePages { get; } = new Dictionary<string, ItemSDPModelBase>();
        public Dictionary<string, ItemSDPModelBase> MemberPages { get; } = new Dictionary<string, ItemSDPModelBase>();
        public Dictionary<string, ItemSDPModelBase> OverloadPages { get; } = new Dictionary<string, ItemSDPModelBase>();

        public SDPYamlConverter(ECMAStore store)
        {
            _store = store;
        }

        public void Convert()
        {
            HashSet<string> memberTouchCache = new HashSet<string>();

            foreach (var ns in _store.Namespaces)
            {
                NamespacePages.Add(ns.Key, FormatNamespace(ns.Value));
            }

            foreach (var type in _store.TypesByUid.Values)
            {
                switch (type.ItemType)
                {
                    case ItemType.Enum:
                        var enumPage = FormatEnum(type, memberTouchCache);
                        TypePages.Add(enumPage.Uid, enumPage);
                        break;
                    case ItemType.Class:
                    case ItemType.Interface:
                    case ItemType.Struct:
                        var tPage = FormatType(type);
                        TypePages.Add(tPage.Uid, tPage);
                        break;
                    case ItemType.Delegate:
                        var dPage = FormatDelegate(type);
                        TypePages.Add(dPage.Uid, dPage);
                        break;
                }

                var mGroups = type.Members
                    ?.Where(m => !memberTouchCache.Contains(m.Uid))
                    .GroupBy(m => m.Overload);
                if (mGroups != null)
                {
                    foreach (var mGroup in mGroups)
                    {
                        var parentType = (Models.Type)mGroup.FirstOrDefault()?.Parent;
                        var ol = parentType?.Overloads.FirstOrDefault(o => o.Uid == mGroup.Key);
                        if (mGroup.Key == null)
                        {
                            foreach (var m in mGroup)
                            {
                                OverloadPages.Add(m.Uid, FormatOverload(null, new List<Member> { m }));
                            }
                        }
                        else
                        {
                            OverloadPages.Add(mGroup.Key, FormatOverload(ol, mGroup.ToList()));
                        }
                    }
                }
            }
        }

        private T InitWithBasicProperties<T>(ReflectionItem item) where T : ItemSDPModelBase, new()
        {
            var signatures = ConverterHelper.BuildSignatures(item)
                ?.Select(sig => new SignatureModel() { Lang = sig.Key, Value = sig.Value })
                .ToList();
            T rval = new T
            {
                Uid = item.Uid,
                CommentId = item.CommentId,
                Name = item.Name,

                Assemblies = item.VersionedAssemblyInfo?.MonikersPerValue.Keys.Select(asm => asm.Name).Distinct().ToList(),
                Attributes = item.Attributes?.Where(att => att.Visible).Select(att => att.TypeFullName).ToList(),
                Syntax = signatures,
                DevLangs = signatures?.Select(sig => sig.Lang).ToList().NullIfEmpty(),

                SeeAlso = BuildSeeAlsoList(item.Docs, _store),
                Summary = item.Docs.Summary,
                Remarks = item.Docs.Remarks,
                Examples = item.Docs.Examples
            };

            switch (item)
            {
                case Member m:
                    rval.Namespace = m.Parent.Parent.Name;
                    rval.FullName = m.FullDisplayName;
                    break;
                case ECMA2Yaml.Models.Type t:
                    rval.Namespace = t.Parent.Name == "" ? null : t.Parent.Name;
                    rval.FullName = t.FullName;
                    break;
                case Namespace n:
                    rval.Namespace = n.Name;
                    rval.FullName = n.Name;
                    break;
            }

            if (item.Metadata.TryGetValue(OPSMetadata.InternalOnly, out object val))
            {
                rval.IsInternalOnly = (bool)val;
            }

            if (item.Metadata.TryGetValue(OPSMetadata.AdditionalNotes, out object notes))
            {
                rval.AdditionalNotes = (AdditionalNotes)notes;
            }

            return rval;
        }

        private IEnumerable<TypeParameter> ConvertTypeParameters(ReflectionItem item)
        {
            if (item.TypeParameters?.Count > 0)
            {
                return item.TypeParameters.Select(tp =>
                    new TypeParameter()
                    {
                        Description = tp.Description,
                        Name = tp.Name
                    }).ToList();
            }
            return null;
        }

        private T ConvertParameter<T>(Parameter p, List<Parameter> knownTypeParams = null) where T: TypeReference, new()
        {
            var isGeneric = knownTypeParams?.Any(tp => tp.Name == p.Type) ?? false;
            return new T()
            {
                Description = p.Description,
                Type = isGeneric 
                    ? "" // should be `p.Type`, tracked in https://ceapex.visualstudio.com/Engineering/_workitems/edit/72695
                    : TypeStringToTypeMDString(p.OriginalTypeString ?? p.Type, _store)
            };
        }

        private Models.SDP.ThreadSafety ConvertThreadSafety(ReflectionItem item)
        {
            if (item.Docs.ThreadSafetyInfo != null)
            {
                return new Models.SDP.ThreadSafety()
                {
                    CustomizedContent = item.Docs.ThreadSafetyInfo.CustomContent,
                    IsSupported = item.Docs.ThreadSafetyInfo.Supported ?? false,
                    MemberScope = item.Docs.ThreadSafetyInfo.MemberScope
                };
            }
            return null;
        }

        public static string BuildSeeAlsoList(Docs docs, ECMAStore store)
        {
            StringBuilder sb = new StringBuilder();
            if (docs.AltMemberCommentIds != null)
            {
                foreach(var altMemberId in docs.AltMemberCommentIds)
                {
                    var uid = altMemberId.ResolveCommentId(store)?.Uid ?? altMemberId.Substring(altMemberId.IndexOf(':') + 1);
                    sb.AppendLine($"- <xref:{uid}>");
                }
            }
            if (docs.Related != null)
            {
                foreach (var rTag in docs.Related)
                {
                    sb.AppendLine($"- [{rTag.Text}]({rTag.Uri})");
                }
            }

            return sb.Length == 0 ? null : sb.ToString();
        }
    }
}

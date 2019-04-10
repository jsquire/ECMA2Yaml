﻿using ECMA2Yaml.Models;
using ECMA2Yaml.Models.SDP;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECMA2Yaml
{
    public partial class SDPYamlConverter
    {
        public MemberSDPModel FormatSingleMember(Member m)
        {
            var sdpMember = InitWithBasicProperties<MemberSDPModel>(m);

            sdpMember.NameWithType = m.Parent.Name + '.' + m.DisplayName;
            sdpMember.TypeParameters = ConvertTypeParameters(m);
            sdpMember.ThreadSafety = ConvertThreadSafety(m);
            sdpMember.Implements = m.Implements?.Select(commentId => DocIdToTypeMDString(commentId, _store))
                .Where(str => str != null)
                .ToList();

            var knowTypeParams = m.Parent.TypeParameters.ConcatList(m.TypeParameters);
            if (m.ReturnValueType != null
                && !string.IsNullOrEmpty(m.ReturnValueType.Type)
                && m.ReturnValueType.Type != "System.Void"
                && m.ItemType != ItemType.Event)
            {
                sdpMember.Returns = ConvertParameter<TypeReference>(m.ReturnValueType, knowTypeParams);
            }

            sdpMember.Parameters = m.Parameters?.Select(p =>
            {
                var r = ConvertParameter<ParameterReference>(p, knowTypeParams);
                r.Name = p.Name;
                return r;
            }).ToList();

            sdpMember.Exceptions = m.Docs.Exceptions?.Select(
                p => new TypeReference()
                {
                    Description = p.Description,
                    Type = UidToTypeMDString(p.Uid, _store)
                }).ToList().NullIfEmpty();

            sdpMember.Permissions = m.Docs.Permissions?.Select(
                p => new TypeReference()
                {
                    Description = p.Description,
                    Type = DocIdToTypeMDString(p.CommentId, _store)
                }).ToList().NullIfEmpty();

            return sdpMember;
        }
    }
}

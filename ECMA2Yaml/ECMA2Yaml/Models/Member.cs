﻿using Microsoft.DocAsCode.DataContracts.ManagedReference;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ECMA2Yaml.Models
{
    public class Member : ReflectionItem
    {
        public string FullName { get; set; }
        public MemberType MemberType { get; set; }
        public Dictionary<string, string> Signatures { get; set; }
        public AssemblyInfo AssemblyInfo { get; set; }
        public List<Parameter> TypeParameters { get; set; }
        public List<Parameter> Parameters { get; set; }
        public List<string> Attributes { get; set; }
        public string ReturnValueType { get; set; }
        public Docs Docs { get; set; }
        public string Overload { get; set; }

        //The ID of a generic method uses postfix ``n, n is the count of in method parameters, for example, System.Tuple.Create``1(``0)
        public override void BuildId(ECMAStore store)
        {
            Id = Name.Replace('.', '#');
            if (TypeParameters?.Count > 0)
            {
                Id = Id.Substring(0, Id.LastIndexOf('<')) + "``" + TypeParameters.Count;
            }
            //handle eii prefix
            Id = Id.Replace('<', '{').Replace('>', '}');
            if (Parameters?.Count > 0)
            {
                //Type conversion operator can be considered a special operator whose name is the UID of the target type,
                //with one parameter of the source type.
                //For example, an operator that converts from string to int should be Explicit(System.String to System.Int32).
                if (Name == "op_Explicit")
                {
                    Id += string.Format("({0} to {1})", Parameters.First().Type, ReturnValueType);
                }
                //spec is wrong, no need to treat indexer specially, so comment this part out
                //else if (MemberType == MemberType.Property && Signatures.ContainsKey("C#") && Signatures["C#"].Contains("["))
                //{
                //    Id += string.Format("[{0}]", string.Join(",", GetParameterUids(store)));
                //}
                else
                {
                    Id += string.Format("({0})", string.Join(",", GetParameterUids(store)));
                }
            }
        }

        private List<string> GetParameterUids(ECMAStore store)
        {
            List<string> ids = new List<string>();
            foreach (var p in Parameters)
            {
                var pt = p.Type.Replace('<', '{').Replace('>', '}');
                var paraUid = store.TypesByFullName.ContainsKey(pt) ? store.TypesByFullName[pt].Uid : pt;
                if (p.RefType != null)
                {
                    paraUid += "@";
                }
                paraUid = ReplaceGenericInParameterUid(((Type)Parent).TypeParameters, "`", paraUid);
                paraUid = ReplaceGenericInParameterUid(TypeParameters, "``", paraUid);
                ids.Add(paraUid);
            }

            return ids;
        }

        //Example:System.Collections.Generic.Dictionary`2.#ctor(System.Collections.Generic.IDictionary{`0,`1},System.Collections.Generic.IEqualityComparer{`0})
        private static Dictionary<string, Regex> TypeParameterRegexes = new Dictionary<string, Regex>();
        private string ReplaceGenericInParameterUid(List<Parameter> typeParameters, string prefix, string paraUid)
        {
            if (typeParameters?.Count > 0)
            {
                int genericCount = 0;
                foreach (var tp in typeParameters)
                {
                    string genericPara = prefix + genericCount;
                    if (tp.Name == paraUid)
                    {
                        return genericPara;
                    }

                    Regex regex = null;
                    if (TypeParameterRegexes.ContainsKey(tp.Name))
                    {
                        regex = TypeParameterRegexes[tp.Name];
                    }
                    else
                    {
                        regex = new Regex("[^\\w]*" + tp.Name + "[^\\w]*", RegexOptions.Compiled);
                        TypeParameterRegexes[tp.Name] = regex;
                    }
                    paraUid = regex.Replace(paraUid, match => match.Value.Replace(tp.Name, genericPara));
                    genericCount++;
                }
            }
            return paraUid;
        }
    }
}

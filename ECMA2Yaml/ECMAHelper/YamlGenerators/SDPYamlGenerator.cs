﻿using ECMA2Yaml;
using ECMA2Yaml.Models;
using Microsoft.DocAsCode.Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECMA2Yaml
{
    public static class SDPYamlGenerator
    {
        const int MaximumFileNameLength = 180;
        public static IDictionary<string, List<string>> Generate(
            ECMAStore store,
            string outputFolder,
            bool flatten)
        {
            WriteLine("Generating SDP Yaml models...");

            var sdpConverter = new SDPYamlConverter(store);
            sdpConverter.Convert();

            WriteLine("Writing SDP Yaml files...");
            ConcurrentDictionary<string, List<string>> fileMapping = new ConcurrentDictionary<string, List<string>>();
            ParallelOptions po = new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount };
            Parallel.ForEach(store.Namespaces, po, ns =>
            {
                var nsFolder = Path.Combine(outputFolder, ns.Key);
                if (!string.IsNullOrEmpty(ns.Key) && sdpConverter.NamespacePages.TryGetValue(ns.Key, out var nsPage))
                {
                    var nsFileName = Path.Combine(outputFolder, ns.Key + ".yml");
                    if (!string.IsNullOrEmpty(ns.Value.SourceFileLocalPath))
                    {
                        fileMapping.TryAdd(ns.Value.SourceFileLocalPath, new List<string> { nsFileName });
                    }
                    YamlUtility.Serialize(nsFileName, nsPage, nsPage.YamlMime);
                }

                if (!flatten && !Directory.Exists(nsFolder))
                {
                    Directory.CreateDirectory(nsFolder);
                }

                foreach (var t in ns.Value.Types)
                {
                    if (!string.IsNullOrEmpty(t.Uid) && sdpConverter.TypePages.TryGetValue(t.Uid, out var typePage))
                    {
                        var tFileName = Path.Combine(flatten ? outputFolder : nsFolder, t.Uid.Replace('`', '-') + ".yml");
                        var ymlFiles = new List<string>() { tFileName };
                        YamlUtility.Serialize(tFileName, typePage, typePage.YamlMime);

                        if (t.Members != null)
                        {
                            foreach (var m in t.Members)
                            {
                                if (!string.IsNullOrEmpty(m.Uid) && sdpConverter.MemberPages.TryGetValue(m.Uid, out var mPage))
                                {
                                    var fileName = PathUtility.ToCleanUrlFileName(m.Uid) + ".yml";
                                    var path = Path.Combine(flatten ? outputFolder : nsFolder, fileName);
                                    ymlFiles.Add(path);
                                    YamlUtility.Serialize(path, mPage, mPage.YamlMime);
                                }
                            }

                            if (t.Overloads != null)
                            {
                                foreach (var ol in t.Overloads)
                                {
                                    if (!string.IsNullOrEmpty(ol.Uid) && sdpConverter.OverloadPages.TryGetValue(ol.Uid, out var mPage))
                                    {
                                        var fileName = PathUtility.ToCleanUrlFileName(GetNewFileName(t.Uid, ol)) + ".yml";
                                        var path = Path.Combine(flatten ? outputFolder : nsFolder, fileName);
                                        ymlFiles.Add(path);
                                        YamlUtility.Serialize(path, mPage, mPage.YamlMime);
                                    }
                                }
                            }
                        }

                        if (!string.IsNullOrEmpty(t.SourceFileLocalPath))
                        {
                            fileMapping.TryAdd(t.SourceFileLocalPath, ymlFiles);
                        }
                    }
                }
            });

            WriteLine("Done writing SDP Yaml files.");
            return fileMapping;
        }

        static void WriteLine(string format, params object[] args)
        {
            string timestamp = string.Format("[{0}]", DateTime.Now.ToString());
            Console.WriteLine(timestamp + string.Format(format, args));
        }

        private static string GetNewFileName(string parentUid, Member item)
        {
            // For constructor, if the class is generic class e.g. ExpandedWrapper`11, class name can be pretty long
            // Use -ctor as file name
            var name = item.DisplayName ?? item.Name;
            return GetValidFileName(
                item.Uid.TrimEnd('*'),
                $"{parentUid}.{name}",
                $"{parentUid}.{name.Split('.').Last()}",
                $"{parentUid}.{Path.GetRandomFileName()}"
                );
        }

        private static string GetValidFileName(params string[] fileNames)
        {
            foreach (var fileName in fileNames)
            {
                if (!string.IsNullOrEmpty(fileName) && fileName.Length <= MaximumFileNameLength)
                {
                    return fileName;
                }
            }

            throw new Exception($"All the file name candidates {fileNames.ToDelimitedString()} exceed the maximum allowed file name length {MaximumFileNameLength}");
        }
    }
}

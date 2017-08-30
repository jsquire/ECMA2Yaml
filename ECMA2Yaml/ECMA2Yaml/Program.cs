﻿using ECMA2Yaml.Models;
using Microsoft.DocAsCode.Common;
using Microsoft.DocAsCode.DataContracts.ManagedReference;
using System;
using System.Linq;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace ECMA2Yaml
{
    class Program
    {
        static void Main(string[] args)
        {
            var opt = new CommandLineOptions();

            try
            {
                if (opt.Parse(args))
                {
                    if (!string.IsNullOrEmpty(opt.RepoRootPath))
                    {
                        OPSLogger.PathTrimPrefix = opt.RepoRootPath;
                    }
                    LoadAndConvert(opt);
                }
            }
            catch (Exception ex)
            {
                WriteLine(ex.ToString());
                OPSLogger.LogSystemError(ex.ToString());
            }
            finally
            {
                OPSLogger.Flush(opt.LogFilePath);
            }
        }

        static void LoadAndConvert(CommandLineOptions opt)
        {
            ECMALoader loader = new ECMALoader();
            WriteLine("Loading ECMAXML files...");
            var store = loader.LoadFolder(opt.SourceFolder, opt.FallbackSourceFolder);
            if (store == null)
            {
                return;
            }
            store.StrictMode = opt.StrictMode;

            WriteLine("Building loaded files...");
            store.Build();
            if (!string.IsNullOrEmpty(opt.FallbackRepoRootPath) && !string.IsNullOrEmpty(opt.FallbackGitBaseUrl))
            {
                store.TranslateSourceLocation(opt.FallbackRepoRootPath, opt.FallbackGitBaseUrl);
            }
            if (!string.IsNullOrEmpty(opt.RepoRootPath) && !string.IsNullOrEmpty(opt.GitBaseUrl))
            {
                store.TranslateSourceLocation(opt.RepoRootPath, opt.GitBaseUrl);
            }
            
            WriteLine("Loaded {0} namespaces.", store.Namespaces.Count);
            WriteLine("Loaded {0} types.", store.TypesByFullName.Count);
            WriteLine("Loaded {0} members.", store.MembersByUid.Count);
            WriteLine("Loaded {0} extension methods.", store.ExtensionMethodsByMemberDocId?.Values?.Count ?? 0);
            WriteLine("Loaded {0} attribute filters.", store.FilterStore?.AttributeFilters?.Count ?? 0);

            WriteLine("Generating Yaml models...");
            var nsPages = TopicGenerator.GenerateNamespacePages(store);
            var typePages = TopicGenerator.GenerateTypePages(store);

            if (!string.IsNullOrEmpty(opt.MetadataFolder))
            {
                WriteLine("Loading metadata overwrite files...");
                var metadataDict = YamlHeaderParser.LoadOverwriteMetadata(opt.MetadataFolder);
                var nsCount = ApplyMetadata(nsPages, metadataDict);
                if (nsCount > 0)
                {
                    WriteLine("Applied metadata overwrite for {0} namespaces", nsCount);
                }
                var typeCount = ApplyMetadata(typePages, metadataDict);
                if (typeCount > 0)
                {
                    WriteLine("Applied metadata overwrite for {0} types", typeCount);
                }
            }

            WriteLine("Writing Yaml files...");
            string overwriteFolder = Path.Combine(opt.OutputFolder, "overwrites");
            if (!Directory.Exists(overwriteFolder))
            {
                Directory.CreateDirectory(overwriteFolder);
            }
            ConcurrentDictionary<string, string> fileMapping = new ConcurrentDictionary<string, string>();
            ParallelOptions po = new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount };
            Parallel.ForEach(store.Namespaces, po, ns =>
            {
                var nsFolder = Path.Combine(opt.OutputFolder, ns.Key);
                var nsFileName = Path.Combine(opt.OutputFolder, ns.Key + ".yml");
                if (!string.IsNullOrEmpty(ns.Value.SourceFileLocalPath))
                {
                    fileMapping.TryAdd(ns.Value.SourceFileLocalPath, nsFileName);
                }
                YamlUtility.Serialize(nsFileName, nsPages[ns.Key], YamlMime.ManagedReference);
                
                if (!opt.Flatten)
                {
                    if (!Directory.Exists(nsFolder))
                    {
                        Directory.CreateDirectory(nsFolder);
                    }
                }

                foreach (var t in store.Namespaces[ns.Key].Types)
                {
                    var typePage = typePages[t.Uid];
                    var tFileName = Path.Combine(opt.Flatten ? opt.OutputFolder : nsFolder, t.Uid.Replace('`', '-') + ".yml");
                    if (!string.IsNullOrEmpty(t.SourceFileLocalPath))
                    {
                        fileMapping.TryAdd(t.SourceFileLocalPath, tFileName);
                    }
                    YamlUtility.Serialize(tFileName, typePage, YamlMime.ManagedReference);
                    if (t.Overloads != null && t.Overloads.Any(o => o.Docs != null))
                    {
                        foreach(var overload in t.Overloads.Where(o => o.Docs != null))
                        {
                            YamlHeaderWriter.WriteOverload(overload, overwriteFolder);
                        }
                    }

                    YamlHeaderWriter.WriteCustomContentIfAny(t.Uid, t.Docs, overwriteFolder);
                    if (t.Members != null)
                    {
                        foreach(var m in t.Members)
                        {
                            YamlHeaderWriter.WriteCustomContentIfAny(m.Uid, m.Docs, overwriteFolder);
                        }
                    }
                }
            });

            //Write TOC
            YamlUtility.Serialize(Path.Combine(opt.OutputFolder, "toc.yml"), TOCGenerator.Generate(store), YamlMime.TableOfContent);

            //Translate change list or save mapping file
            if (opt.ChangeListFiles.Count > 0)
            {
                foreach(var changeList in opt.ChangeListFiles)
                {
                    if (File.Exists(changeList))
                    {
                        var count = ChangeListUpdater.TranslateChangeList(changeList, fileMapping);
                        WriteLine("Translated {0} file entries in {1}.", count, changeList);
                    }
                }
            }
            else
            {
                var mappingFolder = string.IsNullOrEmpty(opt.LogFilePath) ? opt.OutputFolder : Path.GetDirectoryName(opt.LogFilePath);
                JsonUtility.Serialize(Path.Combine(mappingFolder, "XmlYamlMapping.json"), fileMapping, Newtonsoft.Json.Formatting.Indented);
            }
            
            //Save fallback file list as skip publish
            if (!string.IsNullOrEmpty(opt.SkipPublishFilePath) && loader.FallbackMapping?.Count > 0)
            {
                List<string> list = new List<string>();
                if (File.Exists(opt.SkipPublishFilePath))
                {
                    list = JsonUtility.Deserialize<List<string>>(opt.SkipPublishFilePath);
                    WriteLine("Read {0} entries in {1}.", list.Count, opt.SkipPublishFilePath);
                }
                list.AddRange(loader.FallbackMapping.Where(p => p.Key == p.Value && fileMapping.ContainsKey(p.Key)).Select(p => fileMapping[p.Key].Replace(opt.RepoRootPath, "").TrimStart('\\')));
                JsonUtility.Serialize(opt.SkipPublishFilePath, list, Newtonsoft.Json.Formatting.Indented);
                WriteLine("Write {0} entries to {1}.", list.Count, opt.SkipPublishFilePath);
            }

            WriteLine("Done writing Yaml files.");
        }

        static int ApplyMetadata(Dictionary<string, PageViewModel> pages, Dictionary<string, Dictionary<string, object>> metadataDict)
        {
            int count = 0;
            foreach(var page in pages)
            {
                if (page.Value != null)
                {
                    foreach(var item in page.Value.Items)
                    {
                        if (metadataDict.ContainsKey(item.Uid))
                        {
                            if (item.Metadata == null)
                            {
                                item.Metadata = new Dictionary<string, object>();
                            }
                            foreach(var mtaPair in metadataDict[item.Uid])
                            {
                                item.Metadata.Add(mtaPair.Key, mtaPair.Value);
                            }
                            count++;
                        }
                    }
                }
            }
            return count;
        }

        static void WriteLine(string format, params object[] args)
        {
            string timestamp = string.Format("[{0}]", DateTime.Now.ToString());
            Console.WriteLine(timestamp + string.Format(format, args));
        }
    }
}

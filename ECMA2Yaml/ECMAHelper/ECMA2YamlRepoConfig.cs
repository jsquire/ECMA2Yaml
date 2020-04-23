﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECMA2Yaml
{
    public class ECMA2YamlRepoConfig
    {
        [JsonProperty("SourceXmlFolder")]
        public string SourceXmlFolder { get; set; }

        [JsonProperty("OutputYamlFolder")]
        public string OutputYamlFolder { get; set; }

        [JsonProperty("Flatten")]
        public bool Flatten { get; set; }

        [JsonProperty("UWP")]
        public bool UWP { get; set; }
    }
}
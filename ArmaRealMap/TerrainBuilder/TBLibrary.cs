﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;

namespace ArmaRealMap.TerrainBuilder
{
    [XmlRoot("Library")]
    public class TBLibrary
    {
        [XmlElement("Template")]
        public List<TBTemplate> Template { get; set; }

        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlIgnore]
        public string FullPath { get; set; }
    }
}

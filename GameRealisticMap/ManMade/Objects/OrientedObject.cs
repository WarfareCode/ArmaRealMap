﻿using System.Text.Json.Serialization;
using GameRealisticMap.Geometries;

namespace GameRealisticMap.ManMade.Objects
{
    public class OrientedObject : IOrientedObject
    {
        [JsonConstructor]
        public OrientedObject(TerrainPoint point, float angle, ObjectTypeId typeId)
        {
            Point = point;
            Angle = angle;
            TypeId = typeId;
        }

        public TerrainPoint Point { get; }

        public float Angle { get; }

        public ObjectTypeId TypeId { get; }

        [JsonIgnore]
        public float Heading => -Angle;
    }
}

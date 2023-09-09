﻿using System.Numerics;
using GameRealisticMap.Geometries;

namespace GameRealisticMap.Test.Geometries
{
    public class TerrainPathTest
    {
        [Fact]
        public void TerrainPath_Length()
        {
            var path = new TerrainPath(new (0, 0), new (0, 10), new (10, 10));
            Assert.Equal(20, path.Length);

            path = new TerrainPath(new(0, 0), new(10, 10));
            Assert.Equal(14.14, Math.Round(path.Length, 2));
        }

        [Fact]
        public void TerrainPath_Distance()
        {
            var path = new TerrainPath(new(0, 0), new(0, 10));
            Assert.Equal(5, path.Distance(new(5, 5)));
            Assert.Equal(5, path.Distance(new(-5, 5)));
            Assert.Equal(5, path.Distance(new(0, 15)));
            Assert.Equal(5, path.Distance(new(0, -5)));
        }

        [Fact]
        public void TerrainPath_NearestPoint()
        {
            var path = new TerrainPath(new(0, 0), new(0, 10));
            Assert.Equal(new (0, 5), path.NearestPointBoundary(new (5, 5)));
            Assert.Equal(new (0, 5), path.NearestPointBoundary(new (-5, 5)));
            Assert.Equal(new (0, 10), path.NearestPointBoundary(new (0, 15)));
            Assert.Equal(new (0, 0), path.NearestPointBoundary(new (0, -5)));
        }

        [Fact]
        public void RoadsBuilder_PreventSplines()
        {
            Assert.Equal(new List<TerrainPoint>()
            {
                new TerrainPoint(0,0),
                new TerrainPoint(8,0),
                new TerrainPoint(10,0),
                new TerrainPoint(10,2),
                new TerrainPoint(10,10)
            },
            TerrainPath.PreventSplines(new List<TerrainPoint>()
            {
                new TerrainPoint(0,0),
                new TerrainPoint(10,0),
                new TerrainPoint(10,10)
            }, 2));

            Assert.Equal(new List<TerrainPoint>()
            {
                new TerrainPoint(0,0),
                new TerrainPoint(8,0),
                new TerrainPoint(10,0),
                new TerrainPoint(10,2),
                new TerrainPoint(10,10)
            },
            TerrainPath.PreventSplines(new List<TerrainPoint>()
            {
                new TerrainPoint(0,0),
                new TerrainPoint(8,0),
                new TerrainPoint(10,0),
                new TerrainPoint(10,2),
                new TerrainPoint(10,10)
            }, 2));

            Assert.Equal(new List<TerrainPoint>()
            {
                new TerrainPoint(0,0),
                new TerrainPoint(8,0),
                new TerrainPoint(10,0),
                new TerrainPoint(10,2),
                new TerrainPoint(10,8),
                new TerrainPoint(10,10),
                new TerrainPoint(12,10),
                new TerrainPoint(18,10),
                new TerrainPoint(20,10),
                new TerrainPoint(20,12),
                new TerrainPoint(20,20)
            },
            TerrainPath.PreventSplines(new List<TerrainPoint>()
            {
                new TerrainPoint(0,0),
                new TerrainPoint(10,0),
                new TerrainPoint(10,10),
                new TerrainPoint(20,10),
                new TerrainPoint(20,20)
            }, 2));
        }

        [Fact]
        public void TerrainPath_ExtendBothEnds()
        {
            var path = new TerrainPath(new(0, 0), new(0, 10), new(10, 10));
            var pathEx = path.ExtendBothEnds(5);
            Assert.Equal(30, pathEx.Length);
            Assert.Equal(new TerrainPoint[] { new(0, -5), new(0, 10), new(15, 10) }, pathEx.Points);

            path = new TerrainPath(new(0, 5), new(0, 15));
            pathEx = path.ExtendBothEnds(5);
            Assert.Equal(20, pathEx.Length);
            Assert.Equal(new TerrainPoint[] { new(0, 0), new(0, 20) }, pathEx.Points);
        }

        [Fact]
        public void TerrainPath_GetNormalizedVectorAtIndex()
        {
            var path = new TerrainPath(new(0, 0), new(0, 10), new(10, 10), new (20,10));
            Assert.Equal(new Vector2(0, 1), path.GetNormalizedVectorAtIndex(0));
            Assert.Equal(new Vector2(0.70710677f, 0.70710677f), path.GetNormalizedVectorAtIndex(1));
            Assert.Equal(new Vector2(1, 0), path.GetNormalizedVectorAtIndex(2));

            path = new TerrainPath(new(0, 0), new(0, -10), new(-10, -10), new(-20, -10));
            Assert.Equal(new Vector2(0, -1), path.GetNormalizedVectorAtIndex(0));
            Assert.Equal(new Vector2(-0.70710677f, -0.70710677f), path.GetNormalizedVectorAtIndex(1));
            Assert.Equal(new Vector2(-1, 0), path.GetNormalizedVectorAtIndex(2));
        }

        [Fact]
        public void TerrainPath_ClippedBy()
        {
            var path = new TerrainPath(new(5, 0), new(5, 5), new(5, 10), new(5, 15), new(5, 20));
            var clip = path.ClippedBy(TerrainPolygon.FromRectangle(new(2.5f, 2.5f), new(7.5f, 17.5f))).ToList();
            var clipPath = Assert.Single(clip);
            Assert.Equal(new TerrainPoint[] { new(5,2.5f), new(5,5), new(5,10), new(5, 15), new(5,17.5f) }, clipPath.Points);

            clip = path.ClippedBy(TerrainPolygon.FromRectangle(new(2.5f, 0f), new(7.5f, 20f))).ToList();
            clipPath = Assert.Single(clip);
            Assert.Equal(path.Points, clipPath.Points);

            clip = path.ClippedBy(TerrainPolygon.FromRectangle(new(10f, -10f), new(10f, 30f))).ToList();
            Assert.Empty(clip);

            // Order is not preserved by ClippedBy
            path = new TerrainPath(new(17109.277f, -14502.888f), new(16591.264f, -12701.211f), new(16163.766f, -11304.716f), new(15508.252f, -9480.714f), new(14873.61f, -7825.331f), new(13395f, -4382.8315f), new(13258.774f, -4007.939f), new(13170.557f, -3792.907f), new(13141.093f, -3670.9758f), new(13054.459f, -3426.7131f), new(12794.966f, -2589.082f), new(11699.636f, 1066.6356f), new(10796.126f, 4111.285f));
            clip = path.ClippedBy(TerrainPolygon.FromRectangle(new(0, 0), new(26000, 26000))).ToList();
            clipPath = Assert.Single(clip);
            Assert.Equal(new TerrainPoint[] { new(10796.125f, 4111.285f), new(11699.635f, 1066.635f), new(12019.221f, 0) }, clipPath.Points); // Reversed would be also acceptable
        }

        [Fact]
        public void TerrainPath_ClippedKeepOrientation()
        {
            var path = new TerrainPath(new(5, 0), new(5, 5), new(5, 10), new(5, 15), new(5, 20));

            var clip = path.ClippedKeepOrientation(TerrainPolygon.FromRectangle(new(2.5f, 2.5f), new(7.5f, 17.5f))).ToList();
            var clipPath = Assert.Single(clip);
            Assert.Equal(new TerrainPoint[] { new(5, 2.5f), new(5, 5), new(5, 10), new(5, 15), new(5, 17.5f) }, clipPath.Points);

            clip = path.ClippedKeepOrientation(TerrainPolygon.FromRectangle(new(2.5f, 0f), new(7.5f, 20f))).ToList();
            clipPath = Assert.Single(clip);
            Assert.Equal(path.Points, clipPath.Points);

            clip = path.ClippedKeepOrientation(TerrainPolygon.FromRectangle(new(10f, -10f), new(10f, 30f))).ToList();
            Assert.Empty(clip);

            // Order is not preserved by ClippedBy
            path = new TerrainPath(new(17109.277f, -14502.888f), new(16591.264f, -12701.211f), new(16163.766f, -11304.716f), new(15508.252f, -9480.714f), new(14873.61f, -7825.331f), new(13395f, -4382.8315f), new(13258.774f, -4007.939f), new(13170.557f, -3792.907f), new(13141.093f, -3670.9758f), new(13054.459f, -3426.7131f), new(12794.966f, -2589.082f), new(11699.636f, 1066.6356f), new(10796.126f, 4111.285f));
            clip = path.ClippedKeepOrientation(TerrainPolygon.FromRectangle(new(0, 0), new(26000, 26000))).ToList();
            clipPath = Assert.Single(clip);
            Assert.Equal(new TerrainPoint[] { new(12019.221f, 0), new(11699.635f, 1066.635f), new(10796.125f, 4111.285f) }, clipPath.Points);
        }

        [Fact]
        public void TerrainPath_Substract()
        {
            var path = new TerrainPath(new(5, 0), new(5, 5), new(5, 10), new(5, 15), new(5, 20));

            var result = path.Substract(TerrainPolygon.FromRectangle(new(2.5f, 2.5f), new(7.5f, 17.5f))).OrderBy(p => p.FirstPoint.Y).ToList();
            Assert.Equal(2, result.Count);
            Assert.Equal(new TerrainPoint[] { new(5, 0), new(5, 2.5f) }, result[0].Points);
            Assert.Equal(new TerrainPoint[] { new(5, 17.5f), new(5, 20) }, result[1].Points);

            result = path.Substract(TerrainPolygon.FromRectangle(new(10f, -10f), new(10f, 30f))).ToList();
            Assert.Equal(path.Points, Assert.Single(result).Points);

            Assert.Empty(path.Substract(TerrainPolygon.FromRectangle(new(2.5f, 0f), new(7.5f, 20f))));
        }
    }
}

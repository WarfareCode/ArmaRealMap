﻿using GameRealisticMap.Conditions;
using GameRealisticMap.ElevationModel;
using GameRealisticMap.ManMade;
using GameRealisticMap.ManMade.Airports;
using GameRealisticMap.ManMade.Buildings;
using GameRealisticMap.ManMade.DefaultUrbanAreas;
using GameRealisticMap.ManMade.Farmlands;
using GameRealisticMap.ManMade.Fences;
using GameRealisticMap.ManMade.Objects;
using GameRealisticMap.ManMade.Places;
using GameRealisticMap.ManMade.Railways;
using GameRealisticMap.ManMade.Roads;
using GameRealisticMap.Nature.DefaultAreas;
using GameRealisticMap.Nature.Forests;
using GameRealisticMap.Nature.Lakes;
using GameRealisticMap.Nature.Ocean;
using GameRealisticMap.Nature.RockAreas;
using GameRealisticMap.Nature.Scrubs;
using GameRealisticMap.Nature.Surfaces;
using GameRealisticMap.Nature.Trees;
using GameRealisticMap.Nature.Watercourses;
using GameRealisticMap.Nature.Weather;
using GameRealisticMap.Reporting;
using GameRealisticMap.Satellite;

namespace GameRealisticMap
{
    public class BuildersCatalog : IBuidersCatalog
    {
        private readonly Dictionary<Type, IBuilderAdapter> builders = new Dictionary<Type, IBuilderAdapter>();

        public BuildersCatalog(IProgressSystem progress, IBuildersConfig config)
        {
            Register(new OceanBuilder(progress));
            Register(new CoastlineBuilder(progress));
            Register(new RawSatelliteImageBuilder(progress));
            Register(new RawElevationBuilder(progress));
            Register(new CategoryAreaBuilder(progress));
            Register(new RoadsBuilder(progress, config.Roads));
            Register(new BuildingsBuilder(progress, config.Buildings));
            Register(new WatercoursesBuilder(progress));
            Register(new WatercourseRadialBuilder(progress));
            Register(new ForestBuilder(progress));
            Register(new ScrubBuilder(progress));
            Register(new RocksBuilder(progress));
            Register(new ForestRadialBuilder(progress));
            Register(new ScrubRadialBuilder(progress));
            Register(new LakesBuilder(progress));
            Register(new ForestEdgeBuilder(progress));
            Register(new SandSurfacesBuilder(progress));
            Register(new ElevationWithLakesBuilder(progress));
            Register(new MeadowsBuilder(progress));
            Register(new GrassBuilder(progress));
            Register(new FencesBuilder(progress));
            Register(new FarmlandsBuilder(progress));
            Register(new TreesBuilder(progress));
            Register(new OrientedObjectBuilder(progress));
            Register(new RailwaysBuilder(progress, config.RailwayCrossings));
            Register(new CitiesBuilder(progress));
            Register(new ElevationBuilder(progress));
            Register(new VineyardBuilder(progress));
            Register(new OrchardBuilder(progress));
            Register(new TreeRowsBuilder(progress));
            Register(new DefaultAreasBuilder(progress));
            Register(new ProceduralStreetLampsBuilder(progress));
            Register(new SidewalksBuilder(progress));
            Register(new DefaultResidentialAreasBuilder(progress));
            Register(new DefaultCommercialAreasBuilder(progress));
            Register(new DefaultIndustrialAreasBuilder(progress));
            Register(new DefaultMilitaryAreasBuilder(progress));
            Register(new DefaultRetailAreasBuilder(progress));
            Register(new DefaultAgriculturalAreasBuilder(progress));
            Register(new ConditionEvaluatorBuilder());
            Register(new ElevationContourBuilder(progress));
            Register(new WeatherBuilder(progress));
            Register(new IceSurfaceBuilder(progress));
            Register(new ScreeBuilder(progress));
            Register(new ElevationOutOfBoundsBuilder());
            Register(new AirportBuilder(progress));
            Register(new AerowaysBuilder(progress));
        }

        public void Register<TData>(IDataBuilder<TData> builder)
            where TData : class
        {
            builders.Add(typeof(TData), new BuilderAdapter<TData>(builder));
        }

        public IDataBuilder<TData> Get<TData>() where TData : class
        {
            if (builders.TryGetValue(typeof(TData), out var builder))
            {
                return (IDataBuilder<TData>)builder.Builder;
            }
            throw new NotSupportedException($"No builder for '{typeof(TData).Name}'");
        }

        public IEnumerable<object> GetAll(IContext ctx)
        {
            return builders
                .Select(g => g.Value.Get(ctx));
        }

        public IEnumerable<T> GetOfType<T>(IContext ctx, Func<Type,bool>? filter = null) where T : class
        {
            return builders
                .Where(p => typeof(T).IsAssignableFrom(p.Key) && (filter == null || filter(p.Key)))
                .Select(g => (T)g.Value.Get(ctx));
        }

        public int CountOfType<T>(Func<Type, bool>? filter = null) where T : class
        {
            return builders
                .Where(p => typeof(T).IsAssignableFrom(p.Key) && (filter == null || filter(p.Key)))
                .Count();
        }

        public IEnumerable<TResult> VisitAll<TResult>(IDataBuilderVisitor<TResult> visitor)
        {
            return builders
                .Select(g => g.Value.Accept(visitor));
        }
    }
}

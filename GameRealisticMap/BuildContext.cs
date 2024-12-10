﻿using GameRealisticMap.Osm;
using Pmad.HugeImages.Storage;
using Pmad.ProgressTracking;

namespace GameRealisticMap
{
    public class BuildContext : IBuildContext
    {
        private readonly SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);
        private readonly Dictionary<Type, Task> datas = new Dictionary<Type, Task>();
        private readonly IProgressScope rootScope;
        private readonly IBuidersCatalog catalog;

        public BuildContext(IBuidersCatalog catalog, IProgressScope rootScope, ITerrainArea area, IOsmDataSource source, IMapProcessingOptions imagery, IHugeImageStorage? his = null)
        {
            this.rootScope = rootScope;
            this.catalog = catalog;
            Area = area;
            OsmSource = source;
            Options = imagery;
            HugeImageStorage = his ?? new TemporaryHugeImageStorage();
        }

        public IBuidersCatalog Catalog => catalog;

        public ITerrainArea Area { get; }

        public IOsmDataSource OsmSource { get; }

        public IMapProcessingOptions Options { get; }

        public IHugeImageStorage HugeImageStorage { get; }

        public void DisposeHugeImages()
        {
            (HugeImageStorage as IDisposable)?.Dispose();
        }

        public T GetData<T>(IProgressScope? parentScope = null) 
            where T : class
        {
            return GetDataAsync<T>(parentScope).Result;
        }

        public Task<T> GetDataAsync<T>(IProgressScope? parentScope = null) where T : class
        {
            return GetDataTask<T>(parentScope).Unwrap();
        }

        public async Task<Task<T>> GetDataTask<T>(IProgressScope? parentScope = null) where T : class
        {
            await semaphoreSlim.WaitAsync().ConfigureAwait(false);
            try
            {
                if (datas.TryGetValue(typeof(T), out var data))
                {
                    return (Task<T>)data;
                }
                var newTask = CreateDataTask<T>(parentScope);
                datas.Add(typeof(T), newTask);
                return newTask;
            }
            finally
            {
                semaphoreSlim.Release();
            }
        }

        private Task<T> CreateDataTask<T>(IProgressScope? parentScope) where T : class
        {
            var builder = catalog.Get<T>();
            return Task.Run(async () =>
            {
                using (var scope = (parentScope ?? rootScope).CreateScope(builder.GetType().Name.Replace("Builder", "")))
                {
                    if (builder is IDataBuilderAsync<T> asyncBuilder)
                    {
                        return await asyncBuilder.BuildAsync(this, scope).ConfigureAwait(false);
                    }
                    return builder.Build(this, scope);
                }
            });
        }

        public IEnumerable<T> GetOfType<T>() where T : class
        {
            return catalog.GetOfType<T>(this);
        }

        public void SetData<T>(T value)
            where T : class
        {
            datas[typeof(T)] = Task.FromResult(value);
        }
    }
}

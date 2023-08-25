﻿using HugeImages.Storage;

namespace GameRealisticMap
{
    public interface IContext
    {
        T GetData<T>() where T : class;

        IEnumerable<T> GetOfType<T>() where T : class;

        IHugeImageStorage HugeImageStorage { get; }
    }
}

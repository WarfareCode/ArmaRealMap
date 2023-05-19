﻿using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using Caliburn.Micro;
using GameRealisticMap.Studio.Modules.Arma3Data;
using GameRealisticMap.Studio.Modules.CompositionTool.ViewModels;

namespace GameRealisticMap.Studio.Modules.AssetConfigEditor.Views
{
    internal class CompositionPreviewConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var composition = value as CompositionViewModel;
            if ( composition != null )
            {
                var singleModel = composition.SingleModel;
                if (singleModel != null)
                {
                    var uri = IoC.Get<IArma3Previews>().GetPreview(singleModel); // GetPreview can be really slow, find a way to make this lazy
                    if (uri != null)
                    {
                        return new BitmapImage(uri);
                    }
                }
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Caliburn.Micro;
using GameRealisticMap.Studio.Modules.Arma3Data;
using GameRealisticMap.Studio.Modules.CompositionTool.ViewModels;
using GameRealisticMap.Studio.UndoRedo;
using Gemini.Modules.UndoRedo;
using Xceed.Wpf.Toolkit;

namespace GameRealisticMap.Studio.Modules.AssetConfigEditor.Views
{
    public static class Behaviors
    {
        private static readonly DependencyProperty UndoRedoManagerProperty =
            DependencyProperty.RegisterAttached("UndoRedoManager", typeof(IUndoRedoManager), typeof(Behaviors), new PropertyMetadata(UndoRedoManagerChanged));

        private static void UndoRedoManagerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var grid = d as DataGrid;
            if (grid != null)
            {
                grid.CellEditEnding -= Grid_CellEditEnding;
                if (e.NewValue != null)
                {
                    grid.CellEditEnding += Grid_CellEditEnding;
                }
            }
            var textbox = d as TextBox;
            if (textbox != null)
            {
                textbox.GotFocus += (sender, _) => GotFocus<string>((FrameworkElement)sender!, TextBox.TextProperty);
                textbox.LostFocus += (sender, _) => LostFocus<string>((FrameworkElement)sender!, TextBox.TextProperty);
            }
            var colorPicker = d as ColorPicker;
            if (colorPicker != null)
            {
                colorPicker.GotFocus += (sender, _) => GotFocus<System.Windows.Media.Color?>((FrameworkElement)sender!, ColorPicker.SelectedColorProperty);
                colorPicker.LostFocus += (sender, _) => LostFocus<System.Windows.Media.Color?>((FrameworkElement)sender!, ColorPicker.SelectedColorProperty);
            }
        }

        internal static readonly DependencyProperty ValueWhenGotFocusProperty = DependencyProperty.RegisterAttached(
            "ValueWhenGotFocus",
            typeof(object),
            typeof(Behaviors));

        private static void GotFocus<T>(FrameworkElement sender, DependencyProperty property)
        {
            sender.SetValue(ValueWhenGotFocusProperty, sender.GetValue(property));
        }

        private static void LostFocus<T>(FrameworkElement sender, DependencyProperty property)
        {
            var binding = BindingOperations.GetBinding(sender, property);
            if (binding != null)
            {
                var newValue = (T?)sender.GetValue(property);
                var oldValue = (T?)sender.GetValue(ValueWhenGotFocusProperty);
                if (!EqualityComparer<T>.Default.Equals(oldValue, newValue))
                {
                    var undoRedo = (IUndoRedoManager)sender.GetValue(UndoRedoManagerProperty);
                    undoRedo.PushAction(new BindingFocusAction<T>(sender, binding, oldValue, newValue));
                }
            }
        }

        private static void Grid_CellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)
        {
            var grid = sender as DataGrid;
            if (grid != null)
            {
                var undoRedo = (IUndoRedoManager)grid.GetValue(UndoRedoManagerProperty);
                var col = e.Column as DataGridBoundColumn;
                if (col != null)
                {
                    undoRedo.PushAction(new BindingAction<string>(e.Row, (Binding)col.Binding));
                }
            }
        }

        public static void SetUndoRedoManager(UIElement target, IUndoRedoManager value)
        {
            target.SetValue(UndoRedoManagerProperty, value);
        }

        public static void SetEnforceScroll(ScrollViewer viewer, bool value)
        {
            viewer.PreviewMouseWheel -= Viewer_PreviewMouseWheel;
            if (value)
            {
                viewer.PreviewMouseWheel += Viewer_PreviewMouseWheel;
            }
        }

        private static void Viewer_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            var scv = (ScrollViewer)sender;
            scv.ScrollToVerticalOffset(scv.VerticalOffset - e.Delta);
            e.Handled = true;
        }

        public static readonly DependencyProperty DropTargetProperty =
            DependencyProperty.RegisterAttached("DropTarget", typeof(object), typeof(Behaviors), new PropertyMetadata(DropTargetChanged));
        public static void SetDropTarget(UIElement target, object value)
        {
            target.SetValue(DropTargetProperty, value);
        }

        public static void DropTargetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var viewer = (UIElement)d;
            viewer.DragEnter -= Viewer_DragEnter;
            viewer.Drop -= Viewer_Drop;
            var target = viewer.GetValue(DropTargetProperty) as CompositionImporter;
            if (target != null)
            {
                viewer.DragEnter += Viewer_DragEnter;
                viewer.Drop += Viewer_Drop;
                viewer.AllowDrop = true;
            }
            else
            {
                viewer.AllowDrop = false;
            }
        }

        private static void Viewer_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var p3d = ((string[])e.Data.GetData(DataFormats.FileDrop))
                    .Where(f => string.Equals(System.IO.Path.GetExtension(f), ".p3d"));

                var target = ((UIElement)sender).GetValue(DropTargetProperty) as CompositionImporter;
                if (target != null)
                {
                    target.FromFiles(p3d);
                }
                e.Handled = true;
            }
            if (e.Data.GetDataPresent("GRM.A3.Path"))
            {
                e.Handled = true;
                var target = ((UIElement)sender).GetValue(DropTargetProperty) as CompositionImporter;
                if (target != null)
                {
                    target.FromPaths(new[] { (string)e.Data.GetData("GRM.A3.Path") });
                }
            }
        }

        private static void Viewer_DragEnter(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.None;
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                if (((string[])e.Data.GetData(DataFormats.FileDrop)).Select(f => System.IO.Path.GetExtension(f)).All(e => string.Equals(e, ".p3d")))
                {
                    e.Effects = DragDropEffects.Link;
                }
            }
            if (e.Data.GetDataPresent("GRM.A3.Path"))
            {
                e.Effects = DragDropEffects.Link;
            }
        }

        public static readonly DependencyProperty CompositionPreviewProperty =
            DependencyProperty.RegisterAttached("CompositionPreview", typeof(CompositionViewModel), typeof(Behaviors), new PropertyMetadata(CompositionPreviewChanged));

        internal static void SetCompositionPreview(Image target, CompositionViewModel value)
        {
            target.SetValue(CompositionPreviewProperty, value);
        }

        public static void CompositionPreviewChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var img = d as Image;
            var vm = e.NewValue as CompositionViewModel;
            if (img != null && vm != null)
            {
                var source = new CompositionImage(vm, IoC.Get<IArma3Previews>());
                BindingOperations.SetBinding(img, Image.SourceProperty, new Binding("Preview") { 
                    Source = source, 
                    IsAsync = true, 
                    FallbackValue = source.PreviewFast
                });
            }
        }

        private class CompositionImage : PropertyChangedBase
        {
            private readonly CompositionViewModel vm;
            private readonly IArma3Previews arma3Previews;
            private Uri previewCache;

            public CompositionImage(CompositionViewModel vm, IArma3Previews arma3Previews)
            {
                this.vm = vm;
                this.arma3Previews = arma3Previews;
                previewCache = GetPreviewFast();
                vm.PropertyChanged += VmUpdated;
            }

            private Uri GetPreviewFast()
            {
                var model = vm.SingleModel;
                if (model != null)
                {
                    return arma3Previews.GetPreviewFast(model.Path);
                }
                return Arma3DataModule.NoPreview;
            }

            private Uri GetPreviewSlow()
            {
                var model = vm.SingleModel;
                if (model != null)
                {
                    return arma3Previews.GetPreview(model.Path).Result;
                }
                return Arma3DataModule.NoPreview;
            }

            private void VmUpdated(object? sender, PropertyChangedEventArgs e)
            {
                if ( e.PropertyName == "SingleModel")
                {
                    previewCache = GetPreviewFast();
                    NotifyOfPropertyChange(nameof(Preview));
                }
            }

            public Uri PreviewFast => previewCache;

            public Uri Preview
            {
                get
                {
                    if (previewCache.IsFile)
                    {
                        return previewCache;
                    }
                    return previewCache = GetPreviewSlow();
                }
            }
        }

    }
}

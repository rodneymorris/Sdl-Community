﻿using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Sdl.Community.Transcreate.Extensions;

namespace Sdl.Community.Transcreate.Controls
{
	/// <summary>
	/// Interaction logic for ToolWindowsControl.xaml
	/// </summary>
	public partial class ToolWindowsControl : UserControl
	{
		/// <summary>
		///     Identifies the <see cref="ControlHelp" /> property.
		/// </summary>
		public static readonly DependencyProperty HelpProperty =
			DependencyProperty.Register("ControlHelp", typeof(string), typeof(ToolWindowsControl),
				new PropertyMetadata(string.Empty));

		/// <summary>
		///     Identifies the <see cref="ControlClose" /> property.
		/// </summary>
		public static readonly DependencyProperty CloseProperty =
			DependencyProperty.Register("ControlClose", typeof(string), typeof(ToolWindowsControl),
				new PropertyMetadata(string.Empty));

		/// <summary>
		///     Identifies the <see cref="ControlMaximize" /> property.
		/// </summary>
		public static readonly DependencyProperty MaximizeProperty =
			DependencyProperty.Register("ControlMaximize", typeof(string), typeof(ToolWindowsControl),
				new PropertyMetadata(string.Empty));

		/// <summary>
		///     Identifies the <see cref="ControlMinimize" /> property.
		/// </summary>
		public static readonly DependencyProperty MinimizeProperty =
			DependencyProperty.Register("ControlMinimize", typeof(string), typeof(ToolWindowsControl),
				new PropertyMetadata(string.Empty));

		/// <summary>
		///     Identifies the <see cref="ControlRestore" /> property.
		/// </summary>
		public static readonly DependencyProperty RestoreProperty =
			DependencyProperty.Register("ControlRestore", typeof(string), typeof(ToolWindowsControl),
				new PropertyMetadata(string.Empty));


		/// <summary>
		///     Gets or sets the tool-tip for the help button.
		/// </summary>        
		public string ControlHelp
		{
			get => (string)GetValue(HelpProperty);
			set => SetValue(HelpProperty, value);
		}

		/// <summary>
		///     Gets or sets the tool-tip for the close button.
		/// </summary>
		public string ControlClose
		{
			get => (string)GetValue(CloseProperty);
			set => SetValue(CloseProperty, value);
		}

		/// <summary>
		///     Gets or sets the tool-tip for the maximize button.
		/// </summary>
		public string ControlMaximize
		{
			get => (string)GetValue(MaximizeProperty);
			set => SetValue(MaximizeProperty, value);
		}

		/// <summary>
		///     Gets or sets the tool-tip for the minimize button.
		/// </summary>
		public string ControlMinimize
		{
			get => (string)GetValue(MinimizeProperty);
			set => SetValue(MinimizeProperty, value);
		}

		/// <summary>
		///     Gets or sets the tool-tip for the restore button.
		/// </summary>
		public string ControlRestore
		{
			get => (string)GetValue(RestoreProperty);
			set => SetValue(RestoreProperty, value);
		}

		public ToolWindowsControl()
		{
			InitializeComponent();

			Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
			{
				if (string.IsNullOrWhiteSpace(ControlMinimize))
					ControlMinimize = "Minimize";
				if (string.IsNullOrWhiteSpace(ControlMaximize))
					ControlMaximize = "Maximize";
				if (string.IsNullOrWhiteSpace(ControlClose))
					ControlClose = "Close";
				if (string.IsNullOrWhiteSpace(ControlRestore))
					ControlRestore = "Restore";
				if (string.IsNullOrWhiteSpace(ControlHelp))
					ControlHelp = "Help";
			}));
		}

		private void CloseButton_OnClick(object sender, RoutedEventArgs e)
		{
			sender.ForWindowFromFrameworkElement(w => w.Close());
		}

		private void MinButton_Click(object sender, RoutedEventArgs e)
		{
			sender.ForWindowFromFrameworkElement(w => w.WindowState = WindowState.Minimized);
		}

		private void MaxButton_Click(object sender, RoutedEventArgs e)
		{
			sender.ForWindowFromFrameworkElement(w => w.WindowState = w.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized);
		}

		private void TitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			if (e.ClickCount > 1)
			{
				MaxButton_Click(sender, e);
			}
			else if (e.LeftButton == MouseButtonState.Pressed)
			{
				sender.ForWindowFromFrameworkElement(w => w.DragMove());
			}
		}
	}
}

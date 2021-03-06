﻿using System.IO;
using Newtonsoft.Json;
using Sdl.Community.Transcreate.Common;
using Sdl.Community.Transcreate.Model;
using Sdl.Community.Transcreate.View;
using Sdl.Community.Transcreate.ViewModel;
using Sdl.Desktop.IntegrationApi;
using Sdl.Desktop.IntegrationApi.Extensions;

namespace Sdl.Community.Transcreate.Actions
{
	[Action("TranscreateManager_OpenSettings_Action", 
		Name = "TranscreateManager_Settings_Name",
		Description = "TranscreateManager_Settings_Description",
		ContextByType = typeof(TranscreateViewController),
		Icon = "Settings"
		)]
	[ActionLayout(typeof(TranscreateManagerSettingsGroup), 7, DisplayType.Large)]
	public class OpenSettingsAction: AbstractViewControllerAction<TranscreateViewController>
	{
		private PathInfo _pathInfo;

		protected override void Execute()
		{
			var settings = GetSettings();
			var view = new SettingsWindow();			
			var viewModel = new SettingsViewModel(view, settings, _pathInfo);
			view.DataContext = viewModel;
			view.ShowDialog();
		}

		private Settings GetSettings()
		{
			if (File.Exists(_pathInfo.SettingsFilePath))
			{
				var json = File.ReadAllText(_pathInfo.SettingsFilePath);
				return JsonConvert.DeserializeObject<Settings>(json);
			}

			return new Settings();
		}

		public override void Initialize()
		{
			Enabled = true;
			_pathInfo = new PathInfo();
		}
	}
}

﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Markup;
using Sdl.Community.Transcreate.Common;
using Sdl.Community.Transcreate.FileTypeSupport.SDLXLIFF;
using Sdl.Community.Transcreate.Interfaces;
using Sdl.Community.Transcreate.LanguageMapping.Interfaces;
using Sdl.Community.Transcreate.Model;
using Sdl.Community.Transcreate.Wizard.View;
using Sdl.Community.Transcreate.Wizard.View.Convert;
using Sdl.Community.Transcreate.Wizard.View.Export;
using Sdl.Community.Transcreate.Wizard.View.Import;
using Sdl.Community.Transcreate.Wizard.ViewModel;
using Sdl.Community.Transcreate.Wizard.ViewModel.Convert;
using Sdl.Community.Transcreate.Wizard.ViewModel.Export;
using Sdl.Community.Transcreate.Wizard.ViewModel.Import;
using Sdl.Desktop.IntegrationApi.Extensions.Internal;
using Sdl.ProjectAutomation.Core;
using Sdl.TranslationStudioAutomation.IntegrationApi;

namespace Sdl.Community.Transcreate.Service
{
	public class WizardService
	{
		private readonly Enumerators.Action _action;
		private readonly PathInfo _pathInfo;
		private readonly CustomerProvider _customerProvider;
		private readonly Controllers _controllers;
		private readonly ImageService _imageService;
		private readonly SegmentBuilder _segmentBuilder;
		private readonly Settings _settings;
		private readonly IDialogService _dialogService;
		private readonly ILanguageProvider _languageProvider;
		private readonly ProjectAutomationService _projectAutomationService;
		private WizardWindow _wizardWindow;
		private ObservableCollection<WizardPageViewModelBase> _pages;
		private WizardContext _wizardContext;
		private bool _isCancelled;

		public WizardService(Enumerators.Action action, PathInfo pathInfo, CustomerProvider customerProvider,
			ImageService imageService, Controllers controllers, SegmentBuilder segmentBuilder, Settings settings,
			IDialogService dialogService, ILanguageProvider languageProvider, ProjectAutomationService projectAutomationService)
		{
			_action = action;
			_pathInfo = pathInfo;
			_customerProvider = customerProvider;
			_imageService = imageService;
			_controllers = controllers;
			_dialogService = dialogService;
			_segmentBuilder = segmentBuilder;
			_settings = settings;
			_languageProvider = languageProvider;
			_projectAutomationService = projectAutomationService;
		}

		public WizardContext ShowWizard(AbstractController controller, out string message)
		{
			message = string.Empty;

			if (_action == Enumerators.Action.None)
			{
				message = PluginResources.WizardMessage_NoActionSelected;
				return null;
			}

			var success = CreateWizardContext(controller, out message);
			if (!success)
			{
				return null;
			}

			var documents = _controllers.EditorController.GetDocuments();
			if (documents.Any())
			{
				message = PluginResources.WizardMessage_CloseOpenDocumentsInTheEditor;
				return null;
			}


			_isCancelled = false;
			_wizardWindow = new WizardWindow();
			_wizardWindow.Loaded += WizardWindowLoaded;
			_wizardWindow.ShowDialog();

			if (!_isCancelled && _wizardContext.Completed)
			{
				if (_action == Enumerators.Action.Import)
				{
					_controllers.ProjectsController.RefreshProjects();
				}

				return _wizardContext;
			}

			return null;
		}

		private bool CreateWizardContext(AbstractController controller, out string message)
		{
			_wizardContext = new WizardContext(_action, _settings);

			message = string.Empty;

			if (controller is ProjectsController || controller is FilesController)
			{
				var selectedProject = _controllers.ProjectsController.SelectedProjects.FirstOrDefault()
									  ?? _controllers.ProjectsController.CurrentProject;

				_wizardContext.Owner = controller is ProjectsController
					? Enumerators.Controller.Projects
					: Enumerators.Controller.Files;

				// activate the selected project if diffrent to the current project
				if (_controllers.ProjectsController.CurrentProject?.GetProjectInfo().Id != selectedProject.GetProjectInfo().Id)
				{
					_controllers.ProjectsController.Open(selectedProject);
				}

				_wizardContext.AnalysisBands = _projectAutomationService.GetAnalysisBands(selectedProject);

				var projectInfo = selectedProject.GetProjectInfo();
				var selectedFileIds = new List<string>();


				if (controller is FilesController)
				{
					selectedFileIds = _controllers.FilesController.SelectedFiles.Select(a => a.Id.ToString()).ToList();
				}
				else
				{
					foreach (var targetLanguage in projectInfo.TargetLanguages)
					{
						var allFiles = selectedProject.GetTargetLanguageFiles(targetLanguage);
						selectedFileIds.AddRange(
							from projectFile in allFiles
							where projectFile.Role == FileRole.Translatable
							select projectFile.Id.ToString());
					}
				}

				_wizardContext.LocalProjectFolder = projectInfo.LocalProjectFolder;
				_wizardContext.TransactionFolder = _wizardContext.GetDefaultTransactionPath();

				var project = _projectAutomationService.GetProject(selectedProject, selectedFileIds);				

				_wizardContext.Project = project;
				_wizardContext.ProjectFiles = project.ProjectFiles;
			}
			else if (controller is TranscreateViewController)
			{
				_wizardContext.Owner = Enumerators.Controller.Manager;

				var selectedProjectFiles = _controllers.TranscreateController.GetSelectedProjectFiles();
				var selectedProjects = _controllers.TranscreateController.GetSelectedProjects();
				var selectedFileIds = selectedProjectFiles?.Select(a => a.FileId.ToString()).ToList();

				if (selectedProjects.Count == 0)
				{
					message = PluginResources.WizardMessage_NoProjectSelected;
					return false;
				}

				if (selectedProjects.Count > 1)
				{
					message = PluginResources.WizardMessage_MultipleProjectsSelected;
					return false;
				}

				var selectedProject = _controllers.ProjectsController.GetProjects()
					.FirstOrDefault(a => a.GetProjectInfo().Id.ToString() == selectedProjects[0].Id);
				if (selectedProject == null)
				{
					message = PluginResources.WizardMessage_UnableToLocateSelectedProject;
					return false;
				}

				_wizardContext.AnalysisBands = _projectAutomationService.GetAnalysisBands(selectedProject);

				var projectInfo = selectedProject.GetProjectInfo();
				_wizardContext.LocalProjectFolder = projectInfo.LocalProjectFolder;
				_wizardContext.TransactionFolder = _wizardContext.GetDefaultTransactionPath();

				var project = _projectAutomationService.GetProject(selectedProject, selectedFileIds);				
				_wizardContext.Project = project;
				_wizardContext.ProjectFiles = project.ProjectFiles;
			}

			return true;
		}

		private void WizardWindowLoaded(object sender, RoutedEventArgs e)
		{
			_wizardWindow.Loaded -= WizardWindowLoaded;

			_pages = CreatePages(_wizardContext);
			AddDataTemplates(_wizardWindow, _pages);

			var viewModel = new WizardViewModel(_wizardWindow, _pages, _wizardContext, _action);
			viewModel.RequestClose += ViewModel_RequestClose;
			viewModel.RequestCancel += ViewModel_RequestCancel;
			_wizardWindow.DataContext = viewModel;
		}

		private ObservableCollection<WizardPageViewModelBase> CreatePages(WizardContext wizardContext)
		{
			var pages = new List<WizardPageViewModelBase>();

			if (_action == Enumerators.Action.Export)
			{
				pages.Add(new WizardPageExportFilesViewModel(_wizardWindow, new WizardPageExportFilesView(), wizardContext));
				pages.Add(new WizardPageExportOptionsViewModel(_wizardWindow, new WizardPageExportOptionsView(), wizardContext, _dialogService));
				pages.Add(new WizardPageExportSummaryViewModel(_wizardWindow, new WizardPageExportSummaryView(), wizardContext));
				pages.Add(new WizardPageExportPreparationViewModel(_wizardWindow, new WizardPageExportPreparationView(), wizardContext,
					_segmentBuilder, _pathInfo));
			}
			else if (_action == Enumerators.Action.Import)
			{
				pages.Add(new WizardPageImportFilesViewModel(_wizardWindow, new WizardPageImportFilesView(), wizardContext, _dialogService, _languageProvider));
				pages.Add(new WizardPageImportOptionsViewModel(_wizardWindow, new WizardPageImportOptionsView(), wizardContext));
				pages.Add(new WizardPageImportSummaryViewModel(_wizardWindow, new WizardPageImportSummaryView(), wizardContext));
				pages.Add(new WizardPageImportPreparationViewModel(_wizardWindow, new WizardPageImportPreparationView(), wizardContext,
					_segmentBuilder, _pathInfo));
			}
			else if (_action == Enumerators.Action.Convert)
			{				
				pages.Add(new WizardPageConvertOptionsViewModel(_wizardWindow, new WizardPageConvertOptionsView(), wizardContext, _dialogService));
				pages.Add(new WizardPageConvertSummaryViewModel(_wizardWindow, new WizardPageConvertSummaryView(), wizardContext));
				pages.Add(new WizardPageConvertPreparationViewModel(_wizardWindow, new WizardPageConvertPreparationView(), wizardContext,
					_segmentBuilder, _pathInfo, _controllers, _projectAutomationService));
			}

			UpdatePageIndexes(pages);

			return new ObservableCollection<WizardPageViewModelBase>(pages);
		}

		private void UpdatePageIndexes(IReadOnlyList<WizardPageViewModelBase> pages)
		{
			for (var i = 0; i < pages.Count; i++)
			{
				pages[i].PageIndex = i + 1;
				pages[i].TotalPages = pages.Count;
			}
		}

		private void AddDataTemplates(FrameworkElement element, IEnumerable<WizardPageViewModelBase> pages)
		{
			foreach (var page in pages)
			{
				AddDataTemplate(element, page.GetType(), page.View.GetType());
			}
		}

		private void AddDataTemplate(FrameworkElement element, Type viewModelType, Type viewType)
		{
			var template = CreateTemplate(viewModelType, viewType);
			var key = template.DataTemplateKey;
			element.Resources.Add(key, template);
		}

		private DataTemplate CreateTemplate(Type viewModelType, Type viewType)
		{
			const string xamlTemplate = "<DataTemplate DataType=\"{{x:Type vm:{0}}}\"><v:{1} /></DataTemplate>";
			var xaml = string.Format(xamlTemplate, viewModelType.Name, viewType.Name);

			var context = new ParserContext { XamlTypeMapper = new XamlTypeMapper(new string[0]) };

			context.XamlTypeMapper.AddMappingProcessingInstruction("vm", viewModelType.Namespace, viewModelType.Assembly.FullName);
			context.XamlTypeMapper.AddMappingProcessingInstruction("v", viewType.Namespace, viewType.Assembly.FullName);

			context.XmlnsDictionary.Add("", "http://schemas.microsoft.com/winfx/2006/xaml/presentation");
			context.XmlnsDictionary.Add("x", "http://schemas.microsoft.com/winfx/2006/xaml");
			context.XmlnsDictionary.Add("vm", "vm");
			context.XmlnsDictionary.Add("v", "v");

			var template = (DataTemplate)XamlReader.Parse(xaml, context);
			return template;
		}

		private void ViewModel_RequestCancel(object sender, EventArgs e)
		{
			_isCancelled = true;
			CloseWizard();
		}

		private void ViewModel_RequestClose(object sender, EventArgs e)
		{
			CloseWizard();
		}

		private void CloseWizard()
		{
			if (_wizardWindow.DataContext is WizardViewModel viewModel)
			{
				viewModel.RequestCancel -= ViewModel_RequestCancel;
				viewModel.RequestClose -= ViewModel_RequestClose;
				viewModel.Dispose();
			}

			_wizardWindow.Close();
		}
	}
}

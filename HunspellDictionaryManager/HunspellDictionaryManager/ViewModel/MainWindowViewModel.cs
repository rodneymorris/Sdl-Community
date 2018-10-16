﻿using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Sdl.Community.HunspellDictionaryManager.Commands;
using Sdl.Community.HunspellDictionaryManager.Helpers;
using Sdl.Community.HunspellDictionaryManager.Model;

namespace Sdl.Community.HunspellDictionaryManager.ViewModel
{
	public class MainWindowViewModel : ViewModelBase
	{
		#region Private Fields
		private ObservableCollection<HunspellLangDictionaryModel> _dictionaryLanguages = new ObservableCollection<HunspellLangDictionaryModel>();
		private HunspellLangDictionaryModel _selectedDictionaryLanguage;
		private string _newDictionaryLanguage;
		private string _labelVisibility = Constants.Hidden;
		private ICommand _createHunspellDictionaryCommand;

		#endregion

		#region Constructors
		public MainWindowViewModel()
		{
			LoadStudioLanguageDictionaries();
		}
		#endregion

		#region Public Properties
		public HunspellLangDictionaryModel SelectedDictionaryLanguage
		{
			get => _selectedDictionaryLanguage;
			set
			{
				_selectedDictionaryLanguage = value;
				OnPropertyChanged();
			}
		}

		public ObservableCollection<HunspellLangDictionaryModel> DictionaryLanguages
		{
			get => _dictionaryLanguages;
			set
			{
				_dictionaryLanguages = value;
				OnPropertyChanged();
			}
		}

		public string NewDictionaryLanguage
		{
			get => _newDictionaryLanguage;
			set
			{
				_newDictionaryLanguage = value;
				OnPropertyChanged();
			}
		}

		public string LabelVisibility
		{
			get => _labelVisibility;
			set
			{
				_labelVisibility = value;
				OnPropertyChanged();
			}
		}
		#endregion

		#region Public Methods
		#endregion

		#region Private Methods
		/// <summary>
		/// Load .dic and .aff files from the installed Studio location -> HunspellDictionaries folder
		/// </summary>
		private void LoadStudioLanguageDictionaries()
		{
			var studioPath = Utils.GetInstalledStudioPath();
			var hunspellDictionaryFolderPath = Path.Combine(Path.GetDirectoryName(studioPath), Constants.HunspellDictionaries);

			// get .dic files from Studio HunspellDictionaries folder
			var dictionaryFiles = Directory.GetFiles(hunspellDictionaryFolderPath, "*.dic").ToList();
			foreach (var hunspellDictionary in dictionaryFiles)
			{
				var hunspellLangDictionaryModel = new HunspellLangDictionaryModel()
				{
					DictionaryFile = hunspellDictionary,
					DisplayName = Path.GetFileNameWithoutExtension(hunspellDictionary)					
				};

				_dictionaryLanguages.Add(hunspellLangDictionaryModel);
			}

			// get .aff files from Studio HunspellDictionaries folder
			var affFiles = Directory.GetFiles(hunspellDictionaryFolderPath, "*.aff").ToList();
			foreach (var affFile in affFiles)
			{
				var dictLang = _dictionaryLanguages
					.Where(d => Path.GetFileNameWithoutExtension(d.DictionaryFile).Equals(Path.GetFileNameWithoutExtension(affFile)))
					.FirstOrDefault();

				if(dictLang != null)
				{
					dictLang.AffFile = affFile;
				}
			}
		}

		private void CreateHunspellDictionaryAction()
		{
			LabelVisibility = Constants.Visible;
		}
		#endregion

		#region Commands
		public ICommand CreateHunspellDictionaryCommand => _createHunspellDictionaryCommand ?? (_createHunspellDictionaryCommand = new CommandHandler(CreateHunspellDictionaryAction, true));
		#endregion
	}
}

﻿using System.Collections.ObjectModel;
using Sdl.Desktop.IntegrationApi.Interfaces;

namespace NotificationsSample
{
	public class SampleNotificationGroup:IStudioGroupNotification
	{
		private readonly ObservableCollection<IStudioNotification> _notifications;

		public SampleNotificationGroup(string key)
		{
			Key = key;
			_notifications = new ObservableCollection<IStudioNotification>();
		}
		public string Title { get; set; }
		public IStudioNotificationCommand Action { get; set; }
		public bool IsActionVisible { get; set; }
		public string Key { get; }
		public ObservableCollection<IStudioNotification> Notifications
		{
			get => _notifications; set{} }
	}
}

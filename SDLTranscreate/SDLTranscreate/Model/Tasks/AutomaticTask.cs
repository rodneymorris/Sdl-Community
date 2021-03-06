﻿using System.Collections.Generic;
using Sdl.Community.Transcreate.Common;

namespace Sdl.Community.Transcreate.Model.Tasks
{
	public class AutomaticTask
	{
		private Enumerators.Action _action;

		public AutomaticTask(Enumerators.Action action)
		{
			_action = action;

			var actionName = _action == Enumerators.Action.Export
				? "Export"
				: "Import";

			Guid = System.Guid.NewGuid().ToString();
			Status = "Completed";
			PredecessorTaskGuid = "00000000-0000-0000-0000-000000000000";
			PercentComplete = "100";
			TemplateIds = new List<TemplateId> {new TemplateId {Value = "Transcreate.BatchTasks." + actionName } };
			TaskFiles = new List<TaskFile>();
			OutputFiles = new List<OutputFile>();
			Reports = new List<Report>();
		}

		public string Guid { get; set; }

		public string Status { get; set; }

		public string PredecessorTaskGuid { get; set; }

		public string CreatedAt { get; set; }

		public string StartedAt { get; set; }

		public string CompletedAt { get; set; }

		public string CreatedBy { get; set; }

		public string PercentComplete { get; set; }

		public List<TemplateId> TemplateIds { get; set; }

		public List<TaskFile> TaskFiles { get; set; }

		public List<OutputFile> OutputFiles { get; set; }

		public List<Report> Reports { get; set; }

	}
}

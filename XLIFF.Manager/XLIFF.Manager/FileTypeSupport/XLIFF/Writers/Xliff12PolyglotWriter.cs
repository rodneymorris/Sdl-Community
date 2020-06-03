﻿using System;
using System.Collections.Generic;
using System.Xml;
using Sdl.Community.XLIFF.Manager.Common;
using Sdl.Community.XLIFF.Manager.FileTypeSupport.XLIFF.Model;
using Sdl.Community.XLIFF.Manager.Interfaces;
using Sdl.FileTypeSupport.Framework.NativeApi;

namespace Sdl.Community.XLIFF.Manager.FileTypeSupport.XLIFF.Writers
{
	public class Xliff12PolyglotWriter : IXliffWriter
	{
		private Dictionary<string, List<IComment>> Comments { get; set; }

		private bool IncludeTranslations { get; set; }

		public bool WriteFile(Xliff xliff, string outputFilePath, bool includeTranslations)
		{
			Comments = xliff.Comments;
			IncludeTranslations = includeTranslations;

			var settings = new XmlWriterSettings
			{
				OmitXmlDeclaration = true,
				Indent = true
			};

			var version = "1.2";
			var sdlSupport = Enumerators.XLIFFSupport.xliff12polyglot.ToString();
			var sdlVersion = version + ".1";

			using (var writer = XmlWriter.Create(outputFilePath, settings))
			{
				writer.WriteStartElement("xliff");
				writer.WriteAttributeString("version", version);
				writer.WriteAttributeString("xmlns", "sdl", null, "http://schemas.sdl.com/xliff");
				writer.WriteAttributeString("sdl", "support", null, sdlSupport);
				writer.WriteAttributeString("sdl", "version", null, sdlVersion);

				WriteDocInfo(xliff, writer);

				foreach (var xliffFile in xliff.Files)
				{
					writer.WriteStartElement("file");
					writer.WriteAttributeString("original", xliffFile.Original);
					writer.WriteAttributeString("source-language", xliffFile.SourceLanguage);
					if (includeTranslations)
					{
						writer.WriteAttributeString("target-language", xliffFile.TargetLanguage);
					}

					writer.WriteAttributeString("datatype", xliffFile.DataType);

					WriterFileHeader(writer, xliffFile);
					WriteFileBody(writer, xliffFile);

					writer.WriteEndElement(); // file
				}

				writer.WriteEndElement(); //xliff
			}

			return true;
		}

		private void WriteDocInfo(Xliff xliff, XmlWriter writer)
		{
			writer.WriteStartElement("sdl", "doc-info", null);
			writer.WriteAttributeString("project-id", xliff.DocInfo.ProjectId);
			writer.WriteAttributeString("source", xliff.DocInfo.Source);
			writer.WriteAttributeString("source-language", xliff.DocInfo.SourceLanguage);
			if (IncludeTranslations)
			{
				writer.WriteAttributeString("target-language", xliff.DocInfo.TargetLanguage);
			}

			writer.WriteAttributeString("created", GetDateToString(xliff.DocInfo.Created));

			writer.WriteEndElement(); //doc-info
		}

		private void WriteFileBody(XmlWriter writer, File xliffFile)
		{
			writer.WriteStartElement("body");
			foreach (var transUnit in xliffFile.Body.TransUnits)
			{
				// Polyglot flavor
				WriteGroupPolyglot(writer, transUnit);
			}

			writer.WriteEndElement(); // body
		}

		private void WriteGroupPolyglot(XmlWriter writer, TransUnit transUnit)
		{
			writer.WriteStartElement("group");
			writer.WriteAttributeString("id", transUnit.Id);

			WriteTransUnitPolytlot(writer, transUnit);

			writer.WriteEndElement(); // group
		}

		private void WriteTransUnitPolytlot(XmlWriter writer, TransUnit transUnit)
		{
			foreach (var segmentPair in transUnit.SegmentPairs)
			{
				var originType = segmentPair.TranslationOrigin != null ? segmentPair.TranslationOrigin.OriginType : string.Empty;
				var originSystem = segmentPair.TranslationOrigin != null ? segmentPair.TranslationOrigin.OriginSystem : string.Empty;
				var matchPercentage = segmentPair.TranslationOrigin?.MatchPercent.ToString() ?? "0";
				var structMatch = segmentPair.TranslationOrigin?.IsStructureContextMatch.ToString() ?? string.Empty;
				var textMatch = segmentPair.TranslationOrigin?.TextContextMatchLevel.ToString();

				writer.WriteStartElement("trans-unit");
				writer.WriteAttributeString("id", segmentPair.Id);
				if (segmentPair.IsLocked)
				{
					writer.WriteAttributeString("sdl", "locked", null, segmentPair.IsLocked.ToString());
				}

				if (IncludeTranslations)
				{
					writer.WriteAttributeString("sdl", "conf", null, segmentPair.ConfirmationLevel.ToString());
					writer.WriteAttributeString("sdl", "origin", null, originType);

					if (!string.IsNullOrEmpty(originSystem))
					{
						writer.WriteAttributeString("sdl", "origin-system", null, originSystem);
					}

					if (!string.IsNullOrEmpty(matchPercentage) && matchPercentage != "0")
					{
						writer.WriteAttributeString("sdl", "percent", null, matchPercentage);
					}

					if (!string.IsNullOrEmpty(structMatch) && structMatch != "False")
					{
						writer.WriteAttributeString("sdl", "struct-match", null, structMatch);
					}

					if (!string.IsNullOrEmpty(textMatch) && textMatch != "None")
					{
						writer.WriteAttributeString("sdl", "text-match", null, textMatch);
					}
				}

				WriteSegmentNotesPolyglot(writer, segmentPair);
				WriteSegmentPolyglot(writer, segmentPair, true);

				if (IncludeTranslations)
				{
					WriteSegmentPolyglot(writer, segmentPair, false);
				}

				writer.WriteEndElement(); // trans-unit
			}
		}

		private void WriteSegmentNotesPolyglot(XmlWriter writer, SegmentPair segmentPair)
		{
			var sourceComments = GetSegmentComments(segmentPair.Source.Elements);
			WriteNotes(writer, sourceComments, "source");

			if (IncludeTranslations)
			{
				var targetComments = GetSegmentComments(segmentPair.Target.Elements);
				WriteNotes(writer, targetComments, "target");
			}
		}

		private void WriteNotes(XmlWriter writer, Dictionary<string, List<IComment>> comments, string annotates)
		{
			foreach (var commentKeyPair in comments)
			{
				foreach (var comment in commentKeyPair.Value)
				{
					writer.WriteStartElement("note");
					writer.WriteAttributeString("sdl", "id", null, commentKeyPair.Key);
					writer.WriteAttributeString("sdl", "version", null, comment.Version);
					if (comment.DateSpecified)
					{
						writer.WriteAttributeString("sdl", "date", null, GetDateToString(comment.Date));
					}

					writer.WriteAttributeString("from", comment.Author);
					writer.WriteAttributeString("priority", GetPriority(comment.Severity).ToString());
					writer.WriteAttributeString("annotates", annotates);

					writer.WriteString(comment.Text);

					writer.WriteEndElement(); // note
				}
			}
		}

		private void WriteSegmentPolyglot(XmlWriter writer, SegmentPair segmentPair, bool isSource)
		{
			writer.WriteStartElement(isSource ? "source" : "target");
			if (segmentPair.IsLocked)
			{
				writer.WriteStartElement("mrk");
				writer.WriteAttributeString("mtype", "protected");
			}

			var elements = isSource ? segmentPair.Source.Elements : segmentPair.Target.Elements;

			foreach (var element in elements)
			{
				WriteSegment(writer, element);
			}

			if (segmentPair.IsLocked)
			{
				writer.WriteEndElement(); // mrk
			}

			writer.WriteEndElement(); // source or target
		}

		private void WriteSegment(XmlWriter writer, Element element)
		{
			if (element is ElementText text)
			{
				writer.WriteString(text.Text);
			}

			if (element is ElementTagPair tag)
			{
				switch (tag.Type)
				{
					case Element.TagType.OpeningTag:
						writer.WriteStartElement("bpt");
						writer.WriteAttributeString("id", tag.TagId);
						if (!string.IsNullOrEmpty(tag.DisplayText))
						{
							writer.WriteAttributeString("equiv-text", tag.DisplayText);
						}						
						writer.WriteString(tag.TagContent);
						writer.WriteEndElement();
						break;
					case Element.TagType.ClosingTag:
						writer.WriteStartElement("ept");
						writer.WriteAttributeString("id", tag.TagId);
						if (!string.IsNullOrEmpty(tag.DisplayText))
						{
							writer.WriteAttributeString("equiv-text", tag.DisplayText);
						}
						writer.WriteString(tag.TagContent);
						writer.WriteEndElement();
						break;
				}
			}

			if (element is ElementPlaceholder placeholder)
			{
				writer.WriteStartElement("ph");
				writer.WriteAttributeString("id", placeholder.TagId);
				if (!string.IsNullOrEmpty(placeholder.DisplayText))
				{
					writer.WriteAttributeString("equiv-text", placeholder.DisplayText);
				}
				writer.WriteString(placeholder.TagContent);
				writer.WriteEndElement();
			}

			if (element is ElementLocked locked)
			{
				switch (locked.Type)
				{
					case Element.TagType.OpeningTag:
						writer.WriteStartElement("mrk");
						writer.WriteAttributeString("mtype", "protected");
						break;
					case Element.TagType.ClosingTag:
						writer.WriteEndElement();
						break;
				}
			}

			if (element is ElementComment comment)
			{
				switch (comment.Type)
				{
					case Element.TagType.OpeningTag:
						writer.WriteStartElement("mrk");
						writer.WriteAttributeString("mtype", "x-sdl-comment");
						writer.WriteAttributeString("cid", comment.Id);
						break;
					case Element.TagType.ClosingTag:
						writer.WriteEndElement();
						break;
				}
			}
		}

		private void WriterFileHeader(XmlWriter writer, File xliffFile)
		{
			writer.WriteStartElement("header");
			if (!string.IsNullOrEmpty(xliffFile.Header?.Skl?.ExternalFile?.Uid))
			{
				writer.WriteStartElement("skl");

				writer.WriteStartElement("external-file");
				writer.WriteAttributeString("uid", xliffFile.Header.Skl.ExternalFile.Uid);
				writer.WriteAttributeString("href", xliffFile.Header.Skl.ExternalFile.Href);
				writer.WriteEndElement(); // external-file

				writer.WriteEndElement(); // skl
			}
			writer.WriteEndElement(); // header
		}

		private string GetDateToString(DateTime date)
		{
			var value = string.Empty;

			if (date != DateTime.MinValue || date != DateTime.MaxValue)
			{
				return date.ToUniversalTime()
					.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");
			}

			return value;
		}
		
		private int GetPriority(Severity severity)
		{
			switch (severity)
			{
				case Severity.High:
					return 3;
				case Severity.Medium:
					return 2;
				case Severity.Low:
					return 1;
			}

			return 0;
		}

		private Dictionary<string, List<IComment>> GetSegmentComments(IEnumerable<Element> elements)
		{
			var comments = new Dictionary<string, List<IComment>>();
			foreach (var element in elements)
			{
				if (element is ElementComment comment && comment.Type == Element.TagType.OpeningTag)
				{
					if (!Comments.ContainsKey(comment.Id))
					{
						continue;
					}

					if (comments.ContainsKey(comment.Id))
					{
						comments[comment.Id].AddRange(Comments[comment.Id]);
					}
					else
					{
						comments.Add(comment.Id, Comments[comment.Id]);
					}
				}
			}

			return comments;
		}
	}
}
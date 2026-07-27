// vendor-reference: 一次ソース参照用のローカル保存コピー（ecad2側の変更ではない）
// 取得元: https://github.com/Dirkster99/AvalonDock/blob/v4.74.1/source/Components/AvalonDock/Layout/LayoutAnchorGroup.cs
// 取得日: 2026-07-27
// 対象バージョン: v4.74.1（src/Ecad2.App/Ecad2.App.csprojでピン留め中のNuGetバージョンと一致）
// 注意: パッケージバージョンを更新した場合はこのファイルも再取得が必要（陳腐化に注意）
/************************************************************************
   AvalonDock

   Copyright (C) 2007-2013 Xceed Software Inc.

   This program is provided to you under the terms of the Microsoft Public
   License (Ms-PL) as published at https://opensource.org/licenses/MS-PL
 ************************************************************************/

using System;
using System.Windows.Markup;
using System.Xml.Serialization;

namespace AvalonDock.Layout
{
	/// <summary>
	/// Implements the layout model for the <see cref="Controls.LayoutAnchorGroupControl"/>.
	/// </summary>
	[ContentProperty(nameof(Children))]
	[Serializable]
	public class LayoutAnchorGroup : LayoutGroup<LayoutAnchorable>, ILayoutPreviousContainer, ILayoutPaneSerializable
	{
		/// <inheritdoc />
		protected override bool GetVisibility() => Children.Count > 0;

		/// <inheritdoc />
		public override void WriteXml(System.Xml.XmlWriter writer)
		{
			if (_id != null) writer.WriteAttributeString(nameof(ILayoutPaneSerializable.Id), _id);
			if (_previousContainer is ILayoutPaneSerializable paneSerializable) writer.WriteAttributeString("PreviousContainerId", paneSerializable.Id);
			base.WriteXml(writer);
		}

		public override void ReadXml(System.Xml.XmlReader reader)
		{
			if (reader.MoveToAttribute(nameof(ILayoutPaneSerializable.Id))) _id = reader.Value;
			if (reader.MoveToAttribute("PreviousContainerId")) ((ILayoutPreviousContainer)this).PreviousContainerId = reader.Value;
			base.ReadXml(reader);
		}

		[field: NonSerialized]
		private ILayoutContainer _previousContainer = null;

		[XmlIgnore]
		ILayoutContainer ILayoutPreviousContainer.PreviousContainer
		{
			get => _previousContainer;
			set
			{
				if (value == _previousContainer) return;
				_previousContainer = value;
				RaisePropertyChanged(nameof(ILayoutPreviousContainer.PreviousContainer));
				if (_previousContainer is ILayoutPaneSerializable paneSerializable && paneSerializable.Id == null)
					paneSerializable.Id = Guid.NewGuid().ToString();
			}
		}

		string ILayoutPreviousContainer.PreviousContainerId { get; set; }

		private string _id;

		/// <inheritdoc />
		string ILayoutPaneSerializable.Id { get => _id; set => _id = value; }
	}
}
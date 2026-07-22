// vendor-reference: 一次ソース参照用のローカル保存コピー（ecad2側の変更ではない）
// 取得元: https://github.com/Dirkster99/AvalonDock/blob/v4.74.1/source/Components/AvalonDock/Controls/AnchorablePaneTabPanel.cs
// 取得日: 2026-07-22
// 対象バージョン: v4.74.1（src/Ecad2.App/Ecad2.App.csprojでピン留め中のNuGetバージョンと一致）
// 注意: パッケージバージョンを更新した場合はこのファイルも再取得が必要（陳腐化に注意）
/************************************************************************
   AvalonDock

   Copyright (C) 2007-2013 Xceed Software Inc.

   This program is provided to you under the terms of the Microsoft Public
   License (Ms-PL) as published at https://opensource.org/licenses/MS-PL
 ************************************************************************/

using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AvalonDock.Layout;

namespace AvalonDock.Controls
{
	/// <summary>
	/// provides a <see cref="Panel"/> that contains the TabItem Headers of the <see cref="LayoutAnchorablePaneControl"/>.
	/// </summary>
	public class AnchorablePaneTabPanel : Panel
	{
		public AnchorablePaneTabPanel()
		{
			this.FlowDirection = System.Windows.FlowDirection.LeftToRight;
		}

		protected override Size MeasureOverride(Size availableSize)
		{
			double totWidth = 0;
			double maxHeight = 0;
			var visibleChildren = Children.Cast<UIElement>().Where(ch => ch.Visibility != System.Windows.Visibility.Collapsed);
			foreach (FrameworkElement child in visibleChildren)
			{
				child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
				totWidth += child.DesiredSize.Width;
				maxHeight = Math.Max(maxHeight, child.DesiredSize.Height);
			}

			if (totWidth > availableSize.Width)
			{
				double childFinalDesideredWidth = availableSize.Width / visibleChildren.Count();
				foreach (FrameworkElement child in visibleChildren)
				{
					child.Measure(new Size(childFinalDesideredWidth, availableSize.Height));
				}
			}

			return new Size(Math.Min(availableSize.Width, totWidth), maxHeight);
		}

		protected override Size ArrangeOverride(Size finalSize)
		{
			var visibleChildren = Children.Cast<UIElement>().Where(ch => ch.Visibility != System.Windows.Visibility.Collapsed);

			double finalWidth = finalSize.Width;
			double desideredWidth = visibleChildren.Sum(ch => ch.DesiredSize.Width);
			double offsetX = 0.0;

			if (finalWidth > desideredWidth)
			{
				foreach (FrameworkElement child in visibleChildren)
				{
					double childFinalWidth = child.DesiredSize.Width;
					child.Arrange(new Rect(offsetX, 0, childFinalWidth, finalSize.Height));

					offsetX += childFinalWidth;
				}
			}
			else
			{
				double childFinalWidth = finalWidth / visibleChildren.Count();
				foreach (FrameworkElement child in visibleChildren)
				{
					child.Arrange(new Rect(offsetX, 0, childFinalWidth, finalSize.Height));

					offsetX += childFinalWidth;
				}
			}

			return finalSize;
		}

		protected override void OnMouseLeave(System.Windows.Input.MouseEventArgs e)
		{
			if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed &&
				LayoutAnchorableTabItem.IsDraggingItem())
			{
				var contentModel = LayoutAnchorableTabItem.GetDraggingItem().Model as LayoutAnchorable;
				var manager = contentModel.Root.Manager;
				LayoutAnchorableTabItem.ResetDraggingItem();

				manager.StartDraggingFloatingWindowForContent(contentModel);
			}

			base.OnMouseLeave(e);
		}
	}
}
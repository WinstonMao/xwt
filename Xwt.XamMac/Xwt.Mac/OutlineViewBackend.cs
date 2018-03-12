﻿//
// OutlineViewBackend.cs
//
// Author:
//       Lluis Sanchez <lluis@xamarin.com>
//       Hywel Thomas <hywel.w.thomas@gmail.com>
//       strnadj <jan.strnadek@gmail.com>
//
// Copyright (c) 2014 Xamarin Inc
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Linq;
using AppKit;
using Foundation;
using Xwt.Backends;

namespace Xwt.Mac
{
	public class OutlineViewBackend : NSOutlineView
	{
		ITreeViewEventSink eventSink;
		protected ApplicationContext context;
		NSTrackingArea trackingArea;

		public OutlineViewBackend (ITreeViewEventSink eventSink, ApplicationContext context)
		{
			this.context = context;
			this.eventSink = eventSink;
			AllowsColumnReordering = false;
		}

		public NSOutlineView View {
			get { return this; }
		}

		public override NSObject WeakDataSource {
			get { return base.WeakDataSource; }
			set {
				base.WeakDataSource = value;
				AutosizeColumns ();
			}
		}

		bool animationsEnabled = true;
		public bool AnimationsEnabled {
			get { return animationsEnabled; }
			set { animationsEnabled = value; }
		}

		public override void AddColumn (NSTableColumn tableColumn)
		{
			base.AddColumn (tableColumn);
			AutosizeColumns ();
		}

		internal void AutosizeColumns ()
		{
			var columns = TableColumns ();
			if (columns.Length == 1 && columns[0].ResizingMask.HasFlag (NSTableColumnResizing.Autoresizing))
				return;
			var needsSizeToFit = false;
			for (nint i = 0; i < columns.Length; i++) {
				AutosizeColumn (columns[i], i);
				needsSizeToFit |= columns[i].ResizingMask.HasFlag (NSTableColumnResizing.Autoresizing);
			}
			if (needsSizeToFit)
				SizeToFit ();
		}

		void AutosizeColumn (NSTableColumn tableColumn, nint colIndex)
		{
			var contentWidth = tableColumn.HeaderCell.CellSize.Width;
			if (!tableColumn.ResizingMask.HasFlag (NSTableColumnResizing.UserResizingMask)) {
				contentWidth = Delegate.GetSizeToFitColumnWidth (this, colIndex);
				if (!tableColumn.ResizingMask.HasFlag (NSTableColumnResizing.Autoresizing))
					tableColumn.Width = contentWidth;
			}
			tableColumn.MinWidth = contentWidth;
		}

		public override void ExpandItem (NSObject item)
		{
			BeginExpandCollapseAnimation ();
			base.ExpandItem (item);
			EndExpandCollapseAnimation ();
			QueueColumnResize ();
		}

		public override void ExpandItem (NSObject item, bool expandChildren)
		{
			BeginExpandCollapseAnimation ();
			base.ExpandItem (item, expandChildren);
			EndExpandCollapseAnimation ();
			QueueColumnResize ();
		}

		public override void CollapseItem (NSObject item)
		{
			BeginExpandCollapseAnimation ();
			base.CollapseItem (item);
			EndExpandCollapseAnimation ();
			QueueColumnResize ();
		}

		public override void CollapseItem (NSObject item, bool collapseChildren)
		{
			BeginExpandCollapseAnimation ();
			base.CollapseItem (item, collapseChildren);
			EndExpandCollapseAnimation ();
			QueueColumnResize ();
		}

		public override void NoteHeightOfRowsWithIndexesChanged(NSIndexSet indexSet)
		{
			BeginExpandCollapseAnimation();
			base.NoteHeightOfRowsWithIndexesChanged(indexSet);
			EndExpandCollapseAnimation();
		}

		void BeginExpandCollapseAnimation ()
		{
			if (!AnimationsEnabled) {
				NSAnimationContext.BeginGrouping ();
				NSAnimationContext.CurrentContext.Duration = 0;
			}
		}

		void EndExpandCollapseAnimation ()
		{
			if (!AnimationsEnabled)
				NSAnimationContext.EndGrouping ();
		}

		public override void ReloadData ()
		{
			base.ReloadData ();
			QueueColumnResize ();
		}

		public override void ReloadData (Foundation.NSIndexSet rowIndexes, Foundation.NSIndexSet columnIndexes)
		{
			base.ReloadData (rowIndexes, columnIndexes);
			QueueColumnResize ();
		}

		public override void ReloadItem (Foundation.NSObject item)
		{
			base.ReloadItem (item);
			QueueColumnResize ();
		}

		public override void ReloadItem (Foundation.NSObject item, bool reloadChildren)
		{
			base.ReloadItem (item, reloadChildren);
			QueueColumnResize ();
		}

		bool columnResizeQueued;
		void QueueColumnResize ()
		{
			if (!columnResizeQueued) {
				columnResizeQueued = true;
				(context.Toolkit.GetSafeBackend (context.Toolkit) as ToolkitEngineBackend).InvokeBeforeMainLoop (delegate {
					columnResizeQueued = false;
					AutosizeColumns ();
				});
			}
		}

		public override void UpdateTrackingAreas ()
		{
			if (trackingArea != null) {
				RemoveTrackingArea (trackingArea);
				trackingArea.Dispose ();
			}
			var viewBounds = this.Bounds;
			var options = NSTrackingAreaOptions.MouseMoved | NSTrackingAreaOptions.ActiveInKeyWindow | NSTrackingAreaOptions.MouseEnteredAndExited;
			trackingArea = new NSTrackingArea (viewBounds, options, this, null);
			AddTrackingArea (trackingArea);
		}

		public override void RightMouseDown (NSEvent theEvent)
		{
			base.RightMouseUp (theEvent);
			var p = ConvertPointFromView (theEvent.LocationInWindow, null);
			ButtonEventArgs args = new ButtonEventArgs ();
			args.X = p.X;
			args.Y = p.Y;
			args.Button = PointerButton.Right;
			args.IsContextMenuTrigger = theEvent.TriggersContextMenu ();
			context.InvokeUserCode (delegate {
				eventSink.OnButtonPressed (args);
			});
		}

		public override void RightMouseUp (NSEvent theEvent)
		{
			base.RightMouseUp (theEvent);
			var p = ConvertPointFromView (theEvent.LocationInWindow, null);
			ButtonEventArgs args = new ButtonEventArgs ();
			args.X = p.X;
			args.Y = p.Y;
			args.Button = PointerButton.Right;
			context.InvokeUserCode (delegate {
				eventSink.OnButtonReleased (args);
			});
		}

		public override void MouseDown (NSEvent theEvent)
		{
			base.MouseDown (theEvent);
			var p = ConvertPointFromView (theEvent.LocationInWindow, null);
			ButtonEventArgs args = new ButtonEventArgs ();
			args.X = p.X;
			args.Y = p.Y;
			args.Button = PointerButton.Left;
			args.IsContextMenuTrigger = theEvent.TriggersContextMenu ();
			context.InvokeUserCode (delegate {
				eventSink.OnButtonPressed (args);
			});
		}

		public override void MouseUp (NSEvent theEvent)
		{
			base.MouseUp (theEvent);
			var p = ConvertPointFromView (theEvent.LocationInWindow, null);
			ButtonEventArgs args = new ButtonEventArgs ();
			args.X = p.X;
			args.Y = p.Y;
			args.Button = (PointerButton) (int) theEvent.ButtonNumber + 1;
			context.InvokeUserCode (delegate {
				eventSink.OnButtonReleased (args);
			});
		}

		public override void MouseEntered (NSEvent theEvent)
		{
			base.MouseEntered (theEvent);
			context.InvokeUserCode (eventSink.OnMouseEntered);
		}

		public override void MouseExited (NSEvent theEvent)
		{
			base.MouseExited (theEvent);
			context.InvokeUserCode (eventSink.OnMouseExited);
		}

		public override void MouseMoved (NSEvent theEvent)
		{
			base.MouseMoved (theEvent);
			var p = ConvertPointFromView (theEvent.LocationInWindow, null);
			MouseMovedEventArgs args = new MouseMovedEventArgs ((long) TimeSpan.FromSeconds (theEvent.Timestamp).TotalMilliseconds, p.X, p.Y);
			context.InvokeUserCode (delegate {
				eventSink.OnMouseMoved (args);
			});
		}

		public override void MouseDragged (NSEvent theEvent)
		{
			base.MouseDragged (theEvent);
			var p = ConvertPointFromView (theEvent.LocationInWindow, null);
			MouseMovedEventArgs args = new MouseMovedEventArgs ((long) TimeSpan.FromSeconds (theEvent.Timestamp).TotalMilliseconds, p.X, p.Y);
			context.InvokeUserCode (delegate {
				eventSink.OnMouseMoved (args);
			});
		}
	}
}
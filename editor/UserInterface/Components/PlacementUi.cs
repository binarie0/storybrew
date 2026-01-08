using BrewLib.Graphics;
using BrewLib.UserInterface;
using BrewLib.Util;
using OpenTK;
using OpenTK.Input;
using StorybrewCommon.Storyboarding;
using StorybrewEditor.Storyboarding;
using StorybrewEditor.UserInterface.Drawables;
using System;
using System.Diagnostics;

namespace StorybrewEditor.UserInterface.Components
{
    public class PlacementUi : Widget
    {
        public StoryboardSegment Segment
        {
            get => placementDrawable.Segment;
            set
            {
                placementDrawable.Segment = value;
                if (placementDrawable.Segment != null)
                    placementDrawable.ParentTransform = placementDrawable.Segment.BuildCompleteParentTransform();
                else placementDrawable.ParentTransform = null;
            }
        }

        /// <summary>
        /// Actually, this wasn't even necessary. Since a segment can take care of its own position, rotation, and scale
        /// it ended up being easier to just directly edit those values instead of monitoring the placement scale, rotation, and position.
        /// </summary>
        //private EditorStoryboardSegment editorSegment;

        public override Vector2 MinSize => placementDrawable?.MinSize ?? Vector2.Zero;
        public override Vector2 PreferredSize => placementDrawable?.PreferredSize ?? Vector2.Zero;

        private PlacementDrawable placementDrawable;

        internal PlacementUIState GetState() => state;

        public PlacementUi(WidgetManager manager) : base(manager)
        {
            placementDrawable = new PlacementDrawable(this);

            OnClickDown += placementUi_OnClickDown;
            OnClickUp += placementUi_onClickUp;
            OnClickMove += placementUi_onClickMove;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                OnClickDown -= placementUi_OnClickDown;
                OnClickUp -= placementUi_onClickUp;
                OnClickMove -= placementUi_onClickMove;
            }
            placementDrawable = null;
        }

        private Vector2 dragStartPosition;
        private PlacementUIState state = PlacementUIState.Idle;
        private bool placementUi_OnClickDown(WidgetEvent evt, MouseButtonEventArgs e)
        {
            if (e.Button == MouseButton.Left)
            {
                //editorSegment = Segment.AsEditorSegment();
                dragStartPosition = new Vector2(e.X, e.Y);
                var keyboardState = Keyboard.GetState();
                if (keyboardState.IsKeyDown(Key.ShiftLeft))
                    state = PlacementUIState.Scaling;
                else if (keyboardState.IsKeyDown(Key.ControlLeft))
                    state = PlacementUIState.Rotating;
                else state = PlacementUIState.Moving;
                return true;
            }
            return false;
        }
        private void placementUi_onClickUp(WidgetEvent evt, MouseButtonEventArgs e)
        {
            state = PlacementUIState.Idle;
            //editorSegment = null;
        }
        private void placementUi_onClickMove(WidgetEvent evt, MouseMoveEventArgs e)
        {
            if (state == PlacementUIState.Idle)
                return;

            Debug.Assert(e.XDelta != 0 || e.YDelta != 0);

            Vector2 mouseDelta = new Vector2(e.XDelta, e.YDelta);
            var dragEndPosition = dragStartPosition + mouseDelta;
            var dragFrom = placementDrawable.ScreenToSegment(dragStartPosition);
            var dragTo = placementDrawable.ScreenToSegment(dragEndPosition);

            var deltaSegment = dragTo - dragFrom;
            //Debug.Assert(dragFrom != dragTo);

            switch (state)
            {
                case PlacementUIState.Moving:
                    Segment.Position += deltaSegment;
                    //editorSegment.PlacementPosition += deltaSegment;
                    break;
                case PlacementUIState.Scaling:
                    var oldScale = Segment.Scale;
                    //editorSegment.PlacementScale *= dragTo.Length / dragFrom.Length;
                    Segment.Scale *= dragTo.Length / dragFrom.Length;
                    if (Segment.Scale == 0)
                    {
                        //editorSegment.PlacementScale = oldScale;
                        Segment.Scale = oldScale;
                    }
                    break;
                case PlacementUIState.Rotating:
                    var fromAngle = Math.Atan2(dragFrom.Y, dragFrom.X);
                    var toAngle = Math.Atan2(dragTo.Y, dragTo.X);
                    var angleDelta = toAngle - fromAngle;
                    //editorSegment.PlacementRotation += angleDelta;
                    Segment.Rotation += angleDelta;
                    break;
            }
            dragStartPosition = dragEndPosition;
            
        }

        protected override void DrawBackground(DrawContext drawContext, float actualOpacity)
        {
            base.DrawBackground(drawContext, actualOpacity);
            if (placementDrawable.Segment != null)
                placementDrawable.Draw(drawContext, Manager.Camera, Bounds, actualOpacity);
        }

        internal enum PlacementUIState
        {
            Idle,
            Moving,
            Scaling,
            Rotating,
        }
    }
}

using BrewLib.Graphics;
using BrewLib.Graphics.Cameras;
using BrewLib.Util;
using OpenTK;
using StorybrewCommon.Storyboarding;
using StorybrewCommon.Storyboarding.Commands;
using StorybrewCommon.Storyboarding.CommandValues;
using StorybrewCommon.Storyboarding.Display;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace StorybrewEditor.Storyboarding
{
    internal class ModularStoryboardSegment : StoryboardSegment, DisplayableObject, HasPostProcess
    {
        public override string Identifier { get; }

        internal Effect Effect;
        internal EditorStoryboardLayer Layer;
        

        public ModularStoryboardSegment(Effect effect, EditorStoryboardLayer layer, string identifier = null)
        {
            Effect = effect;
            Layer = layer;
            Identifier = identifier;
        }

        private CommandTimeline<CommandPosition> moveCommands = new CommandTimeline<CommandPosition>();
        private CommandTimeline<CommandPosition> originCommands = new CommandTimeline<CommandPosition>();
        private CommandTimeline<CommandDecimal> scaleCommands = new CommandTimeline<CommandDecimal>();
        private CommandTimeline<CommandDecimal> rotateCommands = new CommandTimeline<CommandDecimal>();

        public void MoveTransform(OsbEasing easing, CommandDecimal StartTime, CommandDecimal EndTime, CommandPosition StartPosition, CommandPosition EndPosition)
        {
            moveCommands.Add(new MoveCommand(easing, StartTime, EndTime, StartPosition, EndPosition));
        }

        public void MoveTransform(CommandDecimal StartTime, CommandDecimal EndTime, CommandPosition StartPosition, CommandPosition EndPosition)
            => MoveTransform(OsbEasing.None, StartTime, EndTime, StartPosition, EndPosition);

        public void MoveTransform(CommandDecimal Time, CommandPosition Position)
            => MoveTransform(OsbEasing.None, Time, Time, Position, Position);

        public void ScaleTransform(OsbEasing easing, CommandDecimal StartTime, CommandDecimal EndTime, CommandDecimal StartScale, CommandDecimal EndScale)
        {
            scaleCommands.Add(new ScaleCommand(easing, StartTime, EndTime, StartScale, EndScale));
        }

        public void ScaleTransform(CommandDecimal StartTime, CommandDecimal EndTime, CommandDecimal StartScale, CommandDecimal EndScale)
            => ScaleTransform(OsbEasing.None, StartTime, EndTime, StartScale, EndScale);

        public void ScaleTransform(CommandDecimal Time, CommandDecimal Scale)
            => this.ScaleTransform(OsbEasing.None, Time, Time, Scale, Scale);

        public void RotateTransform(OsbEasing easing, CommandDecimal StartTime, CommandDecimal EndTime, CommandDecimal StartRotation, CommandDecimal EndRotation)
        {
            rotateCommands.Add(new RotateCommand(easing, StartTime, EndTime, StartRotation, EndRotation));
        }

        public void RotateTransform(CommandDecimal StartTime, CommandDecimal EndTime, CommandDecimal StartRotation, CommandDecimal EndRotation)
            => RotateTransform(OsbEasing.None, StartTime, EndTime, StartRotation, EndRotation);

        public void RotateTransform(CommandDecimal Time, CommandDecimal Rotation)
            => RotateTransform(OsbEasing.None, Time, Time, Rotation, Rotation);

        public void AdjustTransformOrigin(OsbEasing easing, CommandDecimal StartTime, CommandDecimal EndTime, CommandPosition StartPosition, CommandPosition EndPosition)
        {
            originCommands.Add(new MoveCommand(easing, StartTime, EndTime, StartPosition, EndPosition));
        }

        public void AdjustTransformOrigin(CommandDecimal Time, CommandPosition Position)
            => AdjustTransformOrigin(OsbEasing.None, Time, Time, Position, Position);

        public void AdjustTransformOrigin(CommandDecimal StartTime, CommandDecimal EndTime, CommandPosition StartPosition, CommandPosition EndPosition)
            => AdjustTransformOrigin(OsbEasing.None, StartTime, EndTime, StartPosition, EndPosition);

        private readonly List<StoryboardObject> storyboardObjects = new List<StoryboardObject>();
        private readonly List<DisplayableObject> displayableObjects = new List<DisplayableObject>();
        private readonly List<EventObject> eventObjects = new List<EventObject>();
        public override Vector2 Origin { get; set; }
        public override Vector2 Position { get; set; }
        public override double Rotation { get; set; }

        public override bool ReverseDepth { get; set; }

        private readonly Dictionary<string, ModularStoryboardSegment> Segments = new Dictionary<string, ModularStoryboardSegment>();
        public override IEnumerable<StoryboardSegment> NamedSegments => Segments.Values;

        private List<DisplayableObject>[] displayableBuckets;

        private double startTime;
        public override double StartTime => startTime;

        private double endTime;
        public override double EndTime => endTime;

        public override double Scale { get; set; }

        public override OsbAnimation CreateAnimation(string path, int frameCount, double frameDelay, OsbLoopType loopType, OsbOrigin origin, Vector2 initialPosition)
        {
            var storyboardObject = new EditorOsbAnimation()
            {
                TexturePath = path,
                Origin = origin,
                FrameCount = frameCount,
                FrameDelay = frameDelay,
                LoopType = loopType,
                InitialPosition = initialPosition,
            };
            storyboardObjects.Add(storyboardObject);
            displayableObjects.Add(storyboardObject);
            displayableBuckets = null;
            return storyboardObject;
        }

        public override OsbAnimation CreateAnimation(string path, int frameCount, double frameDelay, OsbLoopType loopType, OsbOrigin origin = OsbOrigin.Centre)
        => CreateAnimation(path, frameCount, frameDelay, loopType, origin, OsbSprite.DefaultPosition);

        public override OsbSample CreateSample(string path, double time, double volume = 100)
        {
            throw new NotImplementedException();
        }

        public override StoryboardSegment CreateSegment(string identifier = null)
        {
            if (identifier != null)
            {
                var originalName = identifier;
                var count = 1;
                while (Segments.ContainsKey(identifier))
                {
                    count++;
                    identifier = $"{originalName}#{count}";
                }
            }
            return GetSegment(identifier);
        }

        public override OsbSprite CreateSprite(string path, OsbOrigin origin, Vector2 initialPosition)
        {
            var storyboardObject = new EditorOsbSprite()
            {
                TexturePath = path,
                Origin = origin,
                InitialPosition = initialPosition,
            };
            storyboardObjects.Add(storyboardObject);
            displayableObjects.Add(storyboardObject);
            displayableBuckets = null;
            return storyboardObject;
        }

        public override OsbSprite CreateSprite(string path, OsbOrigin origin = OsbOrigin.Centre)
        => CreateSprite(path, origin, OsbSprite.DefaultPosition);

        public override void Discard(StoryboardObject storyboardObject)
        {
            storyboardObjects.Remove(storyboardObject);
            if (storyboardObject is DisplayableObject displayableObject)
            {
                displayableObjects.Remove(displayableObject);
                displayableBuckets = null;
            }
            if (storyboardObject is EventObject eventObject)
                eventObjects.Remove(eventObject);
            if (storyboardObject is EditorStoryboardSegment segment)
            {
                if (segment.Identifier != null)
                    Segments.Remove(segment.Identifier);
            }
        }

        public override StoryboardSegment GetSegment(string identifier)
        {
            if (!Segments.TryGetValue(identifier, out ModularStoryboardSegment segment))
            {
                segment = new ModularStoryboardSegment(Effect, Layer, identifier);
                Segments.Add(identifier, segment);
            }
            return segment;
        }

        public void Draw(DrawContext drawContext, Camera camera, Box2 bounds, float opacity, StoryboardTransform transform, Project project, FrameStats frameStats)
        {
            var displayTime = project.DisplayTime * 1000;
            if (displayTime < StartTime || EndTime < displayTime)
                return;

            if (Layer.Highlight || Effect.Highlight)
                opacity *= (float)((Math.Sin(drawContext.Get<Editor>().TimeSource.Current * 4) + 1) * 0.5);

            //TODO: make this work with the timeline
            var localTransform = new StoryboardTransform(transform,
                    Origin + (Vector2)originCommands.ValueAtTime(displayTime), 
                    Position + (Vector2)moveCommands.ValueAtTime(displayTime), Rotation + rotateCommands.ValueAtTime(displayTime), (float)Scale + scaleCommands.ValueAtTime(displayTime));
            if (displayableObjects.Count < 1000)
            {
                foreach (var displayableObject in displayableObjects)
                    displayableObject.Draw(drawContext, camera, bounds, opacity, localTransform, project, frameStats);
            }
            else
            {
                var bucketLength = 10000;
                var segmentDuration = EndTime - StartTime;

                var bucketCount = Math.Max(1, (int)Math.Ceiling(segmentDuration / bucketLength));
                var currentBucketIndex = (int)((displayTime - StartTime) / bucketLength);

                if (displayableBuckets == null)
                {
                    Debug.Print($"Creating {bucketCount} display buckets for {displayableObjects.Count} sprites");
                    displayableBuckets = new List<DisplayableObject>[bucketCount];
                }

                var currentBucket = displayableBuckets[currentBucketIndex];
                if (currentBucket == null)
                {
                    var bucketStartTime = StartTime + currentBucketIndex * bucketLength;
                    var bucketEndTime = StartTime + (currentBucketIndex + 1) * bucketLength;
                    displayableBuckets[currentBucketIndex] = currentBucket = new List<DisplayableObject>();

                    foreach (var displayableObject in displayableObjects)
                        if (displayableObject.StartTime <= bucketEndTime && bucketStartTime <= displayableObject.EndTime)
                        {
                            currentBucket.Add(displayableObject);
                            displayableObject.Draw(drawContext, camera, bounds, opacity, localTransform, project, frameStats);
                        }
                }
                else
                    foreach (var displayableObject in currentBucket)
                        displayableObject.Draw(drawContext, camera, bounds, opacity, localTransform, project, frameStats);
            }
        }

        public void PostProcess()
        {
            if (ReverseDepth)
            {
                storyboardObjects.Reverse();
                displayableObjects.Reverse();
            }

            foreach (var storyboardObject in storyboardObjects)
                (storyboardObject as HasPostProcess)?.PostProcess();

            startTime = double.MaxValue;
            endTime = double.MinValue;
            foreach (var sbo in storyboardObjects)
            {
                startTime = Math.Min(startTime, sbo.StartTime);
                endTime = Math.Max(endTime, sbo.EndTime);
            }
            displayableBuckets = null;
        }

        public IEnumerable<(StoryboardObject StoryboardObject, StoryboardTransform Transform)> Flatten(StoryboardTransform transform)
        {
            var localTransform = new StoryboardTransform(transform, Origin, Position, Rotation, (float)Scale);
            foreach (var storyboardObject in storyboardObjects)
            {
                if (storyboardObject is EditorStoryboardSegment segment)
                    foreach (var entry in segment.Flatten(localTransform))
                        yield return entry;

                else yield return (storyboardObject, localTransform);
            }
        }

        public override void WriteOsb(TextWriter writer, ExportSettings exportSettings, OsbLayer osbLayer, StoryboardTransform transform, CancellationToken token = default)
        {
            var entries = Flatten(transform).ToArray();

            var writers = new StringWriter[entries.Length];
            Parallel.For(0, entries.Length, new ParallelOptions { CancellationToken = token, }, index =>
            {
                var entry = entries[index];
                var entryWriter = writers[index] = new StringWriter(writer.FormatProvider);
                entry.StoryboardObject.WriteOsb(entryWriter, exportSettings, osbLayer, entry.Transform, token);
            });

            foreach (var w in writers)
                writer.Write(w.ToString());
        }

        public int CalculateSize(OsbLayer osbLayer, CancellationToken token = default)
        {
            var exportSettings = ExportSettings.SizeCalculation;

            using (var stream = new ByteCounterStream())
            using (var writer = new StreamWriter(stream, Project.Encoding))
            {
                foreach (var sbo in storyboardObjects)
                {
                    token.ThrowIfCancellationRequested();
                    sbo.WriteOsb(writer, exportSettings, osbLayer, null, token);
                }

                return (int)stream.Length;
            }
        }
    }
}

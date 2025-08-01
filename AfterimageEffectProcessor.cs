using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

namespace Afterimage
{
    internal class AfterimageEffectProcessor : IVideoEffectProcessor
    {
        readonly DisposeCollector disposer = new();
        ID2D1CommandList? commandList;
        ID2D1Image? input;
        readonly AffineTransform2D wrap;
        readonly private IGraphicsDevicesAndContext devices;
        readonly AfterimageEffect item;

        private readonly Queue<(ID2D1Bitmap1 Image, int Frame, Vector2 DrawPoint, float RotationZ )> frameHistory = new();

        public ID2D1Image Output { get; }

        public AfterimageEffectProcessor(IGraphicsDevicesAndContext devices, AfterimageEffect item)
        {
            this.devices = devices;
            this.item = item;

            wrap = new AffineTransform2D(devices.DeviceContext);
            disposer.Collect(wrap);

            Output = wrap.Output;
            disposer.Collect(Output);
        }

        public DrawDescription Update(EffectDescription effectDescription)
        {
            if (commandList != null)
                disposer.RemoveAndDispose(ref commandList);

            if (input is null)
            {
                wrap.SetInput(0, null, true);
                return effectDescription.DrawDescription;
            }

            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;
            var dc = devices.DeviceContext;
            
            var inputBounds = devices.DeviceContext.GetImageLocalBounds(input);
            var translationToOrigin = Matrix3x2.CreateTranslation(-inputBounds.Left, -inputBounds.Top);

            using var preprocessedEffect = new AffineTransform2D(dc);
            preprocessedEffect.SetInput(0, input, true);
            preprocessedEffect.TransformMatrix = translationToOrigin;
            var processedInput = preprocessedEffect.Output;

            
            var currentDrawPoint = effectDescription.DrawDescription.DrawPoint;
            var currentRotation = effectDescription.DrawDescription.Rotation.Z;
            var processedBounds = devices.DeviceContext.GetImageLocalBounds(processedInput);
            var processedSize = new System.Drawing.Size(
                (int)(processedBounds.Right - processedBounds.Left),
                (int)(processedBounds.Bottom - processedBounds.Top));

            commandList = dc.CreateCommandList();
            disposer.Collect(commandList);
            dc.Target = commandList;
            dc.BeginDraw();

            var currentCenterOffset = new Vector2(processedSize.Width / 2f, processedSize.Height / 2f);
            dc.Transform = Matrix3x2.CreateTranslation(-currentCenterOffset);
            dc.DrawImage(processedInput);
            dc.Transform = Matrix3x2.Identity;

            foreach (var (historyImage, historyTime, historyDrawPoint, historyRotationZ) in frameHistory)
            {
                var timeDiff = (float)(frame - historyTime);
                var effectOpacity = item.Opacity.GetValue(frame, length, fps);

                float decayFactor = 1.0f;

                if (item.IsDecay)
                {
                    var duration = item.Duration.GetValue(frame, length, fps);
                    if (duration > 0)
                    {
                        decayFactor = 1.0f - Math.Clamp(timeDiff / (float)duration, 0, 1.0f);
                    }
                }

                float opacity = decayFactor * ((float)effectOpacity / 100f);
                if (opacity <= 0.01f) continue;

                var relativePos = historyDrawPoint - currentDrawPoint;
                var inverseRotationMatrix = Matrix3x2.CreateRotation(-currentRotation * (float)(Math.PI / 180));
                var localRelativePos = Vector2.Transform(relativePos, inverseRotationMatrix);

                var historySize = historyImage.Size;
                var toOriginMatrix = Matrix3x2.CreateTranslation(-historySize.Width / 2f, -historySize.Height / 2f);

                var relativeRotation = historyRotationZ - currentRotation;
                var rotationMatrix = Matrix3x2.CreateRotation(relativeRotation * (float)(Math.PI / 180));

                var translationMatrix = Matrix3x2.CreateTranslation(localRelativePos);

                dc.Transform = toOriginMatrix * rotationMatrix * translationMatrix;
                dc.DrawBitmap(historyImage, opacity, BitmapInterpolationMode.Linear);
                dc.Transform = Matrix3x2.Identity;
            }

            dc.EndDraw();
            dc.Target = null;
            commandList.Close();

            var commandListRange = dc.GetImageLocalBounds(commandList);
            var x = -(commandListRange.Left + commandListRange.Right) / 2;
            var y = -(commandListRange.Top + commandListRange.Bottom) / 2;
            wrap.TransformMatrix = Matrix3x2.CreateTranslation(x, y);
            wrap.SetInput(0, commandList, true);


            // フレームが連続していない場合は履歴をクリア
            if (frameHistory.Count > 0  && effectDescription.Usage == TimelineSourceUsage.Paused)
            {
                var lastHistoryFrame = frameHistory.Last().Frame;
                if (!(frame == lastHistoryFrame || frame == lastHistoryFrame + 1))
                {
                    while (frameHistory.Count > 0)
                    {
                        var (Image, _, _, _) = frameHistory.Dequeue();
                        Image.Dispose();
                    }
                }
            }

            // 履歴の枚数を制限
            var frameCount = item.FrameCount.GetValue(frame, length, fps);
            while (frameHistory.Count >= frameCount)
            {
                var (Image, _,_, _) = frameHistory.Dequeue();
                Image.Dispose();
            }

            // 現在のフレームをコピーして履歴に追加
            var width = (int)Math.Ceiling(inputBounds.Right - inputBounds.Left);
            var height = (int)Math.Ceiling(inputBounds.Bottom - inputBounds.Top);
            const int maxTextureDimension = 16384;
            width = Math.Min(width, maxTextureDimension);
            height = Math.Min(height, maxTextureDimension);

            var inputSize = new System.Drawing.Size(width, height);

            if (inputSize.Width > 0 && inputSize.Height > 0)
            {
                var bitmapProps = new BitmapProperties1(
                    new(Vortice.DXGI.Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied),
                    96, 96,
                    BitmapOptions.Target);
                var frameCopy = dc.CreateBitmap(new Vortice.Mathematics.SizeI(inputSize.Width, inputSize.Height), bitmapProps);

                dc.Target = frameCopy;
                dc.BeginDraw();
                dc.DrawImage(processedInput);
                dc.EndDraw();
                dc.Target = null;
                frameHistory.Enqueue((frameCopy, frame, currentDrawPoint, currentRotation));
            }

            return effectDescription.DrawDescription;
        }

        public void ClearInput()
        {
        }

        public void SetInput(ID2D1Image? input)
        {
            this.input = input;
        }

        public void Dispose()
        {
            while (frameHistory.Count > 0)
            {
                var (Image, _, _, _) = frameHistory.Dequeue();
                Image.Dispose();
            }

            wrap?.SetInput(0, null, true);
            disposer.Dispose();
        }
    }
}

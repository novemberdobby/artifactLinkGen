using OCV = OpenCvSharp;

namespace HadesBoonBot.Processors
{
    internal class LocalDebug : IProcessor
    {
        string m_sourceId;
        string? m_holdingArea;

        public LocalDebug(string sourceId, string? holdingArea)
        {
            m_sourceId = sourceId;
            m_holdingArea = holdingArea;
        }

        public void Run(List<Classifiers.ClassifiedScreenMeta> screens)
        {
            int imgIdx = -1;
            foreach (var screen in screens)
            {
                imgIdx++;
                if (screen.LocalSource != null && screen.RemoteSource != null && screen.Screen != null)
                {
                    using OCV.Mat image = OCV.Cv2.ImRead(screen.LocalSource);

                    string debugFolder = "local_debug";
                    if (!string.IsNullOrEmpty(m_holdingArea))
                    {
                        debugFolder = Path.Combine(m_holdingArea, debugFolder);
                    }

                    debugFolder = Util.CreateDir(Path.Combine(debugFolder, m_sourceId));

                    string ext = Path.GetExtension(screen.LocalSource);
                    image.SaveImage(Path.Combine(debugFolder, $"{m_sourceId}_{imgIdx}{ext}"));
                    ScreenMetadata meta = new(image);

                    foreach (var slot in screen.Screen.Slots)
                    {
                        if (meta.TryGetTraitRect(slot.Col, slot.Row, out var getRect))
                        {
                            var rect = getRect!.Value;
                            var middle = new OCV.Point(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2);

                            image.DrawMarker(middle, OCV.Scalar.Black, OCV.MarkerTypes.Diamond, (int)meta.BoonWidth, (int)(meta.Multiplier * 5));
                            image.DrawMarker(middle, OCV.Scalar.White, OCV.MarkerTypes.Diamond, (int)meta.BoonWidth, (int)(meta.Multiplier * 3));

                            image.PutText($"{slot.Col}_{slot.Row}", rect.Location, OCV.HersheyFonts.HersheyComplexSmall, 1f, OCV.Scalar.Black, (int)(meta.Multiplier * 2));
                            image.PutText($"{slot.Col}_{slot.Row}", rect.Location, OCV.HersheyFonts.HersheyComplexSmall, 1f, OCV.Scalar.White, (int)(meta.Multiplier * 1));

                            var nameOffset = new OCV.Point(0, 20);
                            image.PutText(slot.Trait.Name, rect.Location + nameOffset, OCV.HersheyFonts.HersheyComplexSmall, 1f, OCV.Scalar.Black, (int)(meta.Multiplier * 2));
                            image.PutText(slot.Trait.Name, rect.Location + nameOffset, OCV.HersheyFonts.HersheyComplexSmall, 1f, OCV.Scalar.White, (int)(meta.Multiplier * 1));
                        }
                    }

                    foreach (var pinSlot in screen.Screen.PinSlots)
                    {
                        var rect = meta.GetPinRect(screen.Screen.GetColumnCount(), pinSlot.Row).iconRect;
                        var middle = new OCV.Point(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2);

                        image.DrawMarker(middle, OCV.Scalar.Black, OCV.MarkerTypes.Diamond, (int)meta.PinnedBoonWidth, (int)(meta.Multiplier * 5));
                        image.DrawMarker(middle, OCV.Scalar.White, OCV.MarkerTypes.Diamond, (int)meta.PinnedBoonWidth, (int)(meta.Multiplier * 3));

                        image.PutText($"{pinSlot.Row}", rect.Location, OCV.HersheyFonts.HersheyComplexSmall, 1f, OCV.Scalar.Black, (int)(meta.Multiplier * 2));
                        image.PutText($"{pinSlot.Row}", rect.Location, OCV.HersheyFonts.HersheyComplexSmall, 1f, OCV.Scalar.White, (int)(meta.Multiplier * 1));

                        var nameOffset = new OCV.Point(0, 20);
                        image.PutText(pinSlot.Trait.Name, rect.Location + nameOffset, OCV.HersheyFonts.HersheyComplexSmall, 1f, OCV.Scalar.Black, (int)(meta.Multiplier * 2));
                        image.PutText(pinSlot.Trait.Name, rect.Location + nameOffset, OCV.HersheyFonts.HersheyComplexSmall, 1f, OCV.Scalar.White, (int)(meta.Multiplier * 1));
                    }

                    image.SaveImage(Path.Combine(debugFolder, $"{m_sourceId}_{imgIdx}_debug{ext}"));
                    Console.WriteLine($"Ran local debug for image {screen.RemoteSource}");
                }
                else
                {
                    Console.Error.WriteLine($"Local debug failed for image {screen.RemoteSource}");
                }
            }
        }
    }
}

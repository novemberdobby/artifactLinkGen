using OCV = OpenCvSharp;

namespace HadesBoonBot.Processors
{
    internal class LocalDebug : PostProcessor
    {
        public string? HoldingArea { get; set; }

        public override void Run(IEnumerable<Classifiers.ClassifiedScreenMeta> screens, Reddit.RedditClient client, Reddit.Controllers.Post post, Codex codex)
        {
            int imgIdx = -1;
            foreach (var screen in screens)
            {
                imgIdx++;
                if (screen.LocalSource != null && screen.RemoteSource != null && screen.Screen != null)
                {
                    using OCV.Mat image = OCV.Cv2.ImRead(screen.LocalSource);

                    string debugFolder = "local_debug";
                    if (!string.IsNullOrEmpty(HoldingArea))
                    {
                        debugFolder = Path.Combine(HoldingArea, debugFolder);
                    }

                    debugFolder = Util.CreateDir(Path.Combine(debugFolder, post.Id));

                    string ext = Path.GetExtension(screen.LocalSource);
                    image.SaveImage(Path.Combine(debugFolder, $"{post.Id}_{imgIdx}{ext}"));
                    ScreenMetadata meta = new(image);

                    var shadowOffset = new OCV.Point(2, 2);
                    var nameOffset = new OCV.Point(0, 20);
                    int yPad = (int)(meta.Multiplier * 8);

                    foreach (var slot in screen.Screen.Slots)
                    {
                        if (slot.Trait == codex.EmptyBoon)
                        {
                            continue;
                        }

                        if (meta.TryGetTraitRect(slot.Col, slot.Row, out var getRect))
                        {
                            var rect = getRect!.Value;
                            var middle = new OCV.Point(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2);

                            image.DrawMarker(middle, OCV.Scalar.Black, OCV.MarkerTypes.Diamond, (int)meta.BoonWidth, (int)(meta.Multiplier * 5));
                            image.DrawMarker(middle, OCV.Scalar.White, OCV.MarkerTypes.Diamond, (int)meta.BoonWidth, (int)(meta.Multiplier * 3));

                            image.PutText($"{slot.Col}_{slot.Row}", rect.Location + shadowOffset, OCV.HersheyFonts.HersheyPlain, 1.25f, OCV.Scalar.Black, thickness: 2, lineType: OCV.LineTypes.AntiAlias);
                            image.PutText($"{slot.Col}_{slot.Row}", rect.Location, OCV.HersheyFonts.HersheyPlain, 1.25f, OCV.Scalar.White, thickness: 2, lineType: OCV.LineTypes.AntiAlias);

                            string nameLines = slot.Trait.Name.Replace(' ', '\n');
                            image.PutTextMultiline(nameLines, rect.Location + nameOffset + shadowOffset, OCV.HersheyFonts.HersheyPlain, 1.25f, OCV.Scalar.Black, thickness: 2, yPadding: yPad, lineType: OCV.LineTypes.AntiAlias);
                            image.PutTextMultiline(nameLines, rect.Location + nameOffset, OCV.HersheyFonts.HersheyPlain, 1.25f, OCV.Scalar.White, thickness: 2, yPadding: yPad, lineType: OCV.LineTypes.AntiAlias);
                        }
                    }

                    foreach (var pinSlot in screen.Screen.PinSlots)
                    {
                        var rect = meta.GetPinRect(screen.Screen.GetColumnCount(), pinSlot.Row).iconRect;
                        var middle = new OCV.Point(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2);

                        image.DrawMarker(middle, OCV.Scalar.Black, OCV.MarkerTypes.Diamond, (int)meta.PinnedBoonWidth, (int)(meta.Multiplier * 5));
                        image.DrawMarker(middle, OCV.Scalar.White, OCV.MarkerTypes.Diamond, (int)meta.PinnedBoonWidth, (int)(meta.Multiplier * 3));

                        image.PutText($"{pinSlot.Row}", rect.Location + shadowOffset, OCV.HersheyFonts.HersheyPlain, 1.25f, OCV.Scalar.Black, thickness: 2, lineType: OCV.LineTypes.AntiAlias);
                        image.PutText($"{pinSlot.Row}", rect.Location, OCV.HersheyFonts.HersheyPlain, 1.25f, OCV.Scalar.White, thickness: 2, lineType: OCV.LineTypes.AntiAlias);

                        image.PutTextMultiline(pinSlot.Trait.Name, rect.Location + nameOffset + shadowOffset, OCV.HersheyFonts.HersheyPlain, 1.25f, OCV.Scalar.Black, thickness: 2, lineType: OCV.LineTypes.AntiAlias);
                        image.PutTextMultiline(pinSlot.Trait.Name, rect.Location + nameOffset, OCV.HersheyFonts.HersheyPlain, 1.25f, OCV.Scalar.White, thickness: 2, lineType: OCV.LineTypes.AntiAlias);
                    }

                    image.SaveImage(Path.Combine(debugFolder, $"{post.Id}_{imgIdx}_debug{ext}"));
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

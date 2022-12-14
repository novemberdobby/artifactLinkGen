using OCV = OpenCvSharp;

namespace HadesBoonBot.Processors
{
    internal class WebPage : PostProcessor
    {
        public string OutputPage { get; set; }

        public override void Run(IEnumerable<Classifiers.ClassifiedScreenMeta> screens, Reddit.RedditClient client, Reddit.Controllers.Post post, Codex codex)
        {
            foreach (var screen in screens)
            {
                //TODO add is_spoiler, provider name, check all descriptions, etc
                //TODO test in all browsers

                if (screen.LocalSource != null && screen.RemoteSource != null && screen.Screen != null)
                {
                    using OCV.Mat image = OCV.Cv2.ImRead(screen.LocalSource);
                    ScreenMetadata meta = new(image);

                    using StreamWriter file = new(OutputPage);

                    file.WriteLine($"<!doctype html>");
                    file.WriteLine($"<html>");
                    file.WriteLine($"<body>");

                    //display the screen
                    file.WriteLine($"<img class='zeroed' src='{screen.RemoteSource}' />");

                    //add a div per tray trait
                    foreach (var trayItem in screen.Screen.Slots)
                    {
                        //except on empty boons (still show a popup for empty ability slots)
                        if (trayItem.Trait == codex.EmptyBoon)
                        {
                            continue;
                        }

                        if (meta.TryGetTraitRect(trayItem.Col, trayItem.Row, out var iconRect) && iconRect.HasValue)
                        {
                            string posStyle = $"width: {iconRect.Value.Width}px; height: {iconRect.Value.Height}px; left: {iconRect.Value.Left}px; top: {iconRect.Value.Top}px";
                            file.WriteLine($"<div class='trait' style='{posStyle};'></div>");
                        }
                    }

                    //find a sensible position for the info box
                    int columnCount = screen.Screen.GetColumnCount();
                    int infoBoxLeft = meta.GetPinRect(columnCount, 0).iconRect.Left;
                    int infoBoxTop = image.Height / 3;
                    int infoBoxWidth = image.Width / 2;
                    int infoBoxHeight = image.Height / 3;

                    file.WriteLine($"<svg id='infoBoxLink' class='zeroed' style='display: none;' width='{image.Width}' height='{image.Height}'><line x1='0' y1='0' x2='0' y2='0' stroke='white' stroke-width='3' stroke-linecap='round'/></svg>");
                    file.WriteLine($"<div id='infoBox' style='display: none; position: absolute; left: {infoBoxLeft}px; top: {infoBoxTop}px; width: {infoBoxWidth}px; height: {infoBoxHeight}px; border: 3px solid white; background: rgba(1, 1, 1, 0.9); color: white; overflow: hidden; line-break: auto;'></div>");
                    
                    //styles & script
                    file.WriteLine(@"
<style type=""text/css"">
.trait {
  position: absolute;
  transform: rotateZ(45deg) scale(0.9);
  animation: traitHover 2s infinite;
}

@keyframes traitHover {
  0% {
    opacity: 40%;
  }
  
  50% {
    opacity: 100%;
  }
  
  100% {
    opacity: 40%;
  }
}

.zeroed {
  position: absolute;
  left: 0px; top: 0px;
}
</style>

<script type=""text/javascript"">
function CheckTraits(event) {

  var anyActive = false;
  var allTraits = document.getElementsByClassName('trait');
  
  for (var i = 0; i < allTraits.length; i++) {
    var bounds = allTraits[i].getBoundingClientRect();
    
    //only activate if mouse is in the diamond zone
    var dx = Math.abs(event.clientX - (bounds.x + bounds.width / 2));
    var dy = Math.abs(event.clientY - (bounds.y + bounds.height / 2));
    var d = dx / bounds.width + dy / bounds.height;
    if (d <= 0.5) {
      SetActiveTrait(allTraits[i]);
      anyActive = true;
      break;
    }
    else {
      allTraits[i].style.border = '';
    }
  }
  
  if (!anyActive) {
    document.getElementById('infoBox').style.display = 'none';
    document.getElementById('infoBoxLink').style.display = 'none';
  }
}

function SetActiveTrait(trait) {
  var allTraits = document.getElementsByClassName('trait');
  for (var i = 0; i < allTraits.length; i++) {
    allTraits[i].style.border = '';
  }

  trait.style.border = '3px solid white';
  var traitBox = trait.getBoundingClientRect();
  
  var infoBox = document.getElementById('infoBox');
  var infoBoxLink = document.getElementById('infoBoxLink');
  
  infoBox.style.display = '';
  var infoBoxBox = infoBox.getBoundingClientRect();
  
  infoBoxLink.style.display = '';
  var line = infoBoxLink.children[0];
  line.x1.baseVal.value = traitBox.right - 2;
  line.y1.baseVal.value = traitBox.top + traitBox.height / 2;
  line.x2.baseVal.value = infoBoxBox.left;
  line.y2.baseVal.value = infoBoxBox.top + infoBoxBox.height / 2;
}

document.addEventListener('mousemove', (event) => CheckTraits(event));

</script>
");

                    file.WriteLine($"</body>");
                    file.WriteLine($"</html>");
                }
            }
        }
    }
}

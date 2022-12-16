using System.Text;
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

                    //display the screen itself
                    file.WriteLine($"<img class='zeroed' src='{screen.RemoteSource}' />");

                    StringBuilder traitData = new();
                    traitData.AppendLine("trait_data = {}");

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
                            string id = $"trait_tray_{trayItem.Col}_{trayItem.Row}";
                            string posStyle = $"width: {iconRect.Value.Width}px; height: {iconRect.Value.Height}px; left: {iconRect.Value.Left}px; top: {iconRect.Value.Top}px";
                            file.WriteLine($"<div class='trait' id='{id}' style='{posStyle};'></div>");

                            var info = GetSafeTraitInfo(trayItem.Trait);
                            traitData.AppendLine($"trait_data['{id}'] = {info};");
                        }
                    }

                    //add a div per pin row trait
                    int columnCount = screen.Screen.GetColumnCount();
                    foreach (var pinItem in screen.Screen.PinSlots)
                    {
                        string id = $"trait_pins_{pinItem.Row}";
                        var pinIconRect = meta.GetPinRect(columnCount, pinItem.Row).iconRect;

                        //deflate slightly; tray icons have a frame but pin rows don't
                        pinIconRect.Inflate(-(int)(pinIconRect.Width * 0.1f), -(int)(pinIconRect.Height * 0.1f));

                        string posStyle = $"width: {pinIconRect.Width}px; height: {pinIconRect.Height}px; left: {pinIconRect.Left}px; top: {pinIconRect.Top}px";
                        file.WriteLine($"<div class='trait' id='{id}' style='{posStyle};'></div>");

                        var info = GetSafeTraitInfo(pinItem.Trait);
                        traitData.AppendLine($"trait_data['{id}'] = {info};");
                    }

                    //find a sensible position for the info box
                    var pinRect = meta.GetPinRect(columnCount, 0).iconRect;
                    int infoBoxLeft = pinRect.Right + pinRect.Width / 2;
                    int infoBoxTop = image.Height / 3;
                    int infoBoxWidth = image.Width / 2;
                    int infoBoxHeight = image.Height / 3;

                    file.WriteLine($@"
<svg id='infoBoxLink' class='zeroed' style='display: none;' width='{image.Width}' height='{image.Height}'><line x1='0' y1='0' x2='0' y2='0' stroke='white' stroke-width='3' stroke-linecap='round'/></svg>
<div id='infoBox' style='display: none;'>
  <div id='infoBoxTraitName'></div>
  <div id='infoBoxTraitDesc'></div>
</div>
");
                    file.WriteLine($"");
                    file.WriteLine($"");
                    
                    //styles & script
                    file.WriteLine(@"
<style type=""text/css"">
.trait {
  position: absolute;
  transform: rotateZ(45deg) scale(0.9);
}

.zeroed {
  position: absolute;
  left: 0px;
  top: 0px;
}

#infoBox {
"
+
@$"
left: {infoBoxLeft}px;
top: {infoBoxTop}px;
width: {infoBoxWidth}px;
height: {infoBoxHeight}px;
"
+
@"
  position: absolute;
  border: 3px double white;
  background: rgba(1, 1, 1, 0.9);
  color: white;
  overflow: hidden;
  line-break: auto;
  box-sizing: border-box;
  padding: 1em;
}

#infoBoxTraitName {
  font-family: Arial, Helvetica, sans-serif;
  font-size: x-large;
  padding-bottom: 0.5em;
}

#infoBoxTraitDesc {
  font-family: Arial, Helvetica, sans-serif;
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

  //hide all
  for (var i = 0; i < allTraits.length; i++) {
    allTraits[i].style.border = '';
  }

  trait.style.border = '3px solid white';
  var traitBox = trait.getBoundingClientRect();
  
  var infoBox = document.getElementById('infoBox');
  var infoBoxLink = document.getElementById('infoBoxLink');
  
  infoBox.style.display = '';
  var infoBoxBox = infoBox.getBoundingClientRect();

  //data for each trait
"
+
traitData.ToString()
+
@"
  document.getElementById('infoBoxTraitName').innerText = trait_data[trait.id].name;
  document.getElementById('infoBoxTraitDesc').innerText = trait_data[trait.id].desc;

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

        private string GetSafeTraitInfo(Codex.Provider.Trait trait)
        {
            string makeSafe(string input) => input.Replace("'", "\\'").Replace("\n", "\\n");

            string safeName = makeSafe(trait.Name);
            string safeDesc = makeSafe(trait.Description);

            return $"{{ name: '{safeName}', desc: '{safeDesc}' }}";
        }
    }
}

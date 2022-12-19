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

                            var info = PrepareTraitInfo(trayItem.Trait, codex, OutputPage);
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

                        string posStyle = $"left: {pinIconRect.Left}px; top: {pinIconRect.Top}px; width: {pinIconRect.Width}px; height: {pinIconRect.Height}px;";
                        file.WriteLine($"<div class='trait' id='{id}' style='{posStyle}'></div>");

                        var info = PrepareTraitInfo(pinItem.Trait, codex, OutputPage);
                        traitData.AppendLine($"trait_data['{id}'] = {info};");
                    }

                    //find a sensible position for the info box
                    var pinRect = meta.GetPinRect(columnCount, 0).iconRect;
                    OCV.Rect infoBoxRect = new(pinRect.Right + pinRect.Width / 2, image.Height / 3, image.Width / 2, image.Height / 3);

                    //offset a little for the border, scale up
                    pinRect.Left = infoBoxRect.Left + 5;
                    pinRect.Top = infoBoxRect.Top + 5;
                    pinRect.Width = (int)(pinRect.Width * 1.5f);
                    pinRect.Height = (int)(pinRect.Height * 1.5f);
                    infoBoxRect.Height = pinRect.Height + 10;

                    file.WriteLine($@"
<svg id='infoBoxLink' class='zeroed' style='display: none;' width='{image.Width}' height='{image.Height}'><path id='curve' d='' stroke='white' stroke-width='3' stroke-linecap='round' fill='transparent'/>
  <circle cx='' cy='' r='4' fill='#ffffff'>
    <animateMotion dur='1s' repeatCount='indefinite'>
      <mpath xlink:href='#curve'></mpath>
    </animateMotion>
  </circle>
</svg>
<div id='infoBox' style='display: none;'>
  <div id='infoBoxTraitName'></div>
  <div id='infoBoxTraitDesc'></div>
</div>
");
                    //and the icon
                    file.WriteLine($@"<img id='infoBoxTraitIcon' style='display: none; position: absolute; left: {pinRect.Left}px; top: {pinRect.Top}px; width: {pinRect.Width}px; height: {pinRect.Height}px;'></img>");
                    
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
left: {infoBoxRect.Left}px;
top: {infoBoxRect.Top}px;
width: {infoBoxRect.Width}px;
height: {infoBoxRect.Height}px;
padding: 1em;
padding-left: {pinRect.Right - infoBoxRect.Left}px;
"
+
@"
  position: absolute;
  border: 3px double white;
  box-shadow: rgb(0, 0, 0) 0px 0px 40px 20px;
  background: rgba(1, 1, 1, 0.9);
  color: white;
  overflow: hidden;
  line-break: auto;
  box-sizing: border-box;
}

#infoBoxTraitName {
  font-family: Arial, Helvetica, sans-serif;
  font-size: x-large;
  padding-bottom: 0.5em;
}

#infoBoxTraitDesc {
  font-family: Arial, Helvetica, sans-serif;
  font-size: large;
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
    document.getElementById('infoBoxTraitIcon').style.display = 'none';
  }
}

function SetActiveTrait(trait) {

  if (trait.style.border != '') {
    return;
  }

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
  //fill text fields
  var data = trait_data[trait.id];
  document.getElementById('infoBoxTraitName').innerText = data.name;
  document.getElementById('infoBoxTraitDesc').innerHTML = data.desc;

  //position info box
  infoBoxLink.style.display = '';
  var line = infoBoxLink.children[0];

  //draw curve
  var c1x = (traitBox.right - 2) + (infoBoxBox.left - traitBox.right) / 2;
  var c1y = traitBox.top + traitBox.height / 2;
  var c2x = infoBoxBox.left - (infoBoxBox.left - traitBox.right) / 2;
  var c2y = infoBoxBox.top + infoBoxBox.height / 2;

  var d = 'M' + (traitBox.right - 2) + ',' + (traitBox.top + traitBox.height / 2) + ' C' +
    c1x + ',' + c1y + ' ' + c2x + ',' + c2y + ' ' + infoBoxBox.left + ',' + (infoBoxBox.top + infoBoxBox.height / 2);

  line.setAttributeNS(null, 'd', d);

  //set icon
  var infoBoxIcon = document.getElementById('infoBoxTraitIcon');
  infoBoxIcon.style.display = '';
  infoBoxIcon.src = data.icon;
}

document.addEventListener('mousemove', (event) => CheckTraits(event));

</script>
");

                    file.WriteLine($"</body>");
                    file.WriteLine($"</html>");
                }
            }
        }

        private static string PrepareTraitInfo(Codex.Provider.Trait trait, Codex codex, string outputPage)
        {
            string makeSafe(string input) => input.Replace("'", "\\'").Replace("\n", "\\n");

            string safeName = makeSafe(trait.Name);
            string safeDesc = makeSafe(trait.Description);

            string outputFolder = Path.Combine(Path.GetDirectoryName(outputPage)!, "icons");
            string iconPath = Path.Combine(outputFolder, trait.Name + ".png");

            if(!File.Exists(iconPath))
            {
                if (!Directory.Exists(outputFolder))
                {
                    Directory.CreateDirectory(outputFolder);
                }

                trait.Icon!.SaveImage(iconPath);
            }

            DirectoryInfo pageInfo = new(outputPage);
            DirectoryInfo iconInfo = new(iconPath);

            string relativePath = iconInfo.FullName[pageInfo.Parent!.FullName.Length..].Replace('\\', '/');
            return $"{{ name: '{safeName}', desc: '{safeDesc}', icon: '.{makeSafe(relativePath)}' }}";
        }
    }
}

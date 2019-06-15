<%@ taglib prefix="bs" tagdir="/WEB-INF/tags" %>
<%@ taglib prefix="forms" tagdir="/WEB-INF/tags/forms" %>

<style type="text/css">
.portableArtifactLink {
  background: url(${resources}link.png) 0 0 no-repeat;
     width: 16px;
     height: 16px;
     display: inline-block;
     align-self: center;
}
</style>


<tr class="noBorder">
  <td colspan="2">
    <bs:dialog dialogId="generateDialog" title="Generate portable link" closeCommand="BS.PortableArtifactLinker.GenerateDialog.close()">
      <div id="generateResult" style="overflow-y:auto; height:400px"></div>
      <div class="popupSaveButtonsBlock">
        <forms:cancel label="Close" onclick="BS.PortableArtifactLinker.GenerateDialog.close()"/>
      </div>
    </bs:dialog>
  </td>
</tr>


<script type="text/javascript">

  BS.PortableArtifactLinker = {
    onLinkHover: function(item, isSpan) {
      //TODO: test in all browsers

      var link = undefined;
      var childLinks = item.getElementsByTagName('a'); //should get direct children really
      for(var i = 0; i < childLinks.length; i++)
      {
        if(childLinks[i].parentElement == item)
        {
          link = childLinks[i];
          break;
        }
      }

      if(link == undefined)
      {
        return;
      }

      var toAdd = isSpan ? item : link;
      var existing = toAdd.lastChild;
      if(existing == undefined || !existing.hasClassName("portableArtifactLink"))
      {
        var btn = document.createElement("a");
        btn.classList.add("portableArtifactLink");
        btn.textContent = " ";
        btn.title = "Generate portable link";
        btn.href = "#";
        btn.onclick = function() { BS.PortableArtifactLinker.openGenerateDialog(link.href); }
        toAdd.appendChild(btn);
      }

      existing = toAdd.lastChild;
      var links = document.getElementsByClassName("portableArtifactLink");
      for(var i = 0; i < links.length; i++)
      {
        //hide the rest
        links[i].style.display = links[i] == existing ? "" : "none";
      }
    },
    
    GenerateDialog: OO.extend(BS.AbstractModalDialog, {
      getContainer: function () {
        return $('generateDialog');
      },
      
      init: function(href) {
        $('generateResult').innerHTML = "";
      },
    }),

    openGenerateDialog: function(href) {
      BS.PortableArtifactLinker.GenerateDialog.init(href);
      BS.PortableArtifactLinker.GenerateDialog.showCentered();
    },
  };
  
$(document).on('mouseover', 'li', function(a,b) { BS.PortableArtifactLinker.onLinkHover(b, false); });
$(document).on('mouseover', 'span', function(a,b) { BS.PortableArtifactLinker.onLinkHover(b, true); });

</script>
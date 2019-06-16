<%@ taglib prefix="bs" tagdir="/WEB-INF/tags" %>
<%@ taglib prefix="forms" tagdir="/WEB-INF/tags/forms" %>
<%@ taglib prefix="c" uri="http://java.sun.com/jsp/jstl/core" %>

<%@ page import="novemberdobby.teamcity.artifactLinkGen.Constants" %>

<c:set var="generate_url" value="<%=Constants.GENERATOR_URL%>"/>

<style type="text/css">
.portableArtifactLink {
  background: url(${resources}link.png) 0 0 no-repeat;
     width: 16px;
     height: 16px;
     display: inline-block;
     align-self: center;
}
div#dialog_options table tbody tr td {
  padding: 4px 4px 4px 4px;
}
</style>


<bs:dialog dialogId="generateDialog" title="Generate portable link" closeCommand="BS.PortableArtifactLinker.GenerateDialog.close()">

  <div id="dialog_options">
    <table>
      <tr>
        <td>Link expiry:</td>
        <td>
          <select id="link_expiry" onchange="BS.PortableArtifactLinker.onExpiryChange()">
            <option value="5" >5 minutes</option>
            <option value="15" selected="true">15 minutes</option>
            <option value="custom">Custom</option>
            <option value="-1">None*</option>
          </select>
          <div id="link_expiry_none_warning" style="color:#FF0000">
            *Note: this link should be manually removed when no longer needed!
          </div>
        </td>
      </tr>
      <tr>
        <td id="link_expiry_custom_label">Expiry time (minutes):</td>
        <td>
          <input type="number" min="1" id="link_expiry_custom" value="" class="textProperty">
        </td>
      </tr>
    </table>
  </div>

  <div id="generateResult" style="overflow-y:auto"></div>
  <forms:saving id="generateProgress"/>
  <div class="popupSaveButtonsBlock">
    <forms:button id="btnGenerateLink" onclick="BS.PortableArtifactLinker.GenerateDialog.generate()" className="btn_primary">Generate</forms:button>
    <forms:cancel label="Close" onclick="BS.PortableArtifactLinker.GenerateDialog.close()"/>
  </div>
</bs:dialog>


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
        //TODO: can we stop these clicks propagating further? or trees can get toggled
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

    onExpiryChange: function() {
      var expValue = $('link_expiry').value;
      $('link_expiry_custom').style.display = $('link_expiry_custom_label').style.display = (expValue == "custom" ? "" : "none");
      $('link_expiry_none_warning').style.display = expValue == "-1" ? "" : "none";
    },
    
    GenerateDialog: OO.extend(BS.AbstractModalDialog, {

      _href: undefined,

      getContainer: function () {
        return $('generateDialog');
      },
      
      init: function(href) {
        _href = href;
        $('generateResult').innerHTML = "";
        BS.Util.show('dialog_options');
        BS.PortableArtifactLinker.onExpiryChange();
      },

      result: function(transport) {
        if(transport && transport.responseText)
        {
          if(transport.status == 200)
          {
            BS.PortableArtifactLinker.GenerateDialog.showCentered();
            BS.Util.hide('dialog_options');
            $('generateResult').textContent = transport.responseText;
          }
          else if(transport.status == 400) //bad request
          {
            $('generateResult').textContent = transport.responseText;
          }
          else
          {
            alert("Unknown result " + transport.status);
          }
        }
      },

      generate: function() {
        BS.Util.show('generateProgress');

        BS.ajaxRequest(window['base_uri'] + '${generate_url}', {
          method: "GET",
          parameters: {
            'link': _href,
            'expiry': $('link_expiry').value,
            'expiry_custom': $('link_expiry_custom').value,
          },
          onComplete: function(transport) {
            BS.Util.hide('generateProgress');
            BS.PortableArtifactLinker.GenerateDialog.result(transport);
          }
        });

      }
    }),

    openGenerateDialog: function(href) {
      BS.PortableArtifactLinker.GenerateDialog.init(href);
      BS.PortableArtifactLinker.GenerateDialog.showCentered();
    },
  };
  
$(document).on('mouseover', 'li', function(a,b) { BS.PortableArtifactLinker.onLinkHover(b, false); });
$(document).on('mouseover', 'span', function(a,b) { BS.PortableArtifactLinker.onLinkHover(b, true); });

</script>
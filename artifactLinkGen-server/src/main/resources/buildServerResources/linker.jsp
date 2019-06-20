<%@ taglib prefix="bs" tagdir="/WEB-INF/tags" %>
<%@ taglib prefix="forms" tagdir="/WEB-INF/tags/forms" %>
<%@ taglib prefix="c" uri="http://java.sun.com/jsp/jstl/core" %>

<%@ page import="novemberdobby.teamcity.artifactLinkGen.Constants" %>

<c:set var="generate_url" value="<%=Constants.CREATE_URL%>"/>
<c:set var="manage_tab_id" value="<%=Constants.MANAGE_TAB_ID%>"/>
<c:set var="non_admin_min_time" value="<%=Constants.NON_ADMIN_MIN_TIME%>"/>
<c:set var="non_admin_max_time" value="<%=Constants.NON_ADMIN_MAX_TIME%>"/>

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
      <tbody>
        <tr>
          <td>Link expiry:</td>
          <td>
            <select id="link_expiry" onchange="BS.PortableArtifactLinker.onExpiryChange()">
              <option value="${non_admin_min_time}" >${non_admin_min_time} minutes</option>
              <option value="${non_admin_max_time}" selected="true">${non_admin_max_time} minutes</option>
              <c:if test='${isAdmin}'>
              <option value="custom">Custom</option>
              <option value="-1">Never*</option>
              </c:if>
            </select>
            <div id="link_expiry_never_warning" style="color:#FF0000">
              *Note: this link should be <a target="_blank" href="/admin/admin.html?item=${manage_tab_id}">manually removed</a> when no longer needed!
            </div>
          </td>
        </tr>
        <c:if test='${not isAdmin}'>
        <tr>
          <td colspan="2">
            <div>Project administrators can create links with longer expiries.</div>
          </td>
        </tr>
        </c:if>
        <tr>
          <td id="link_expiry_custom_label">Expiry time (minutes):</td>
          <td>
            <input type="number" min="1" id="link_expiry_custom" value="" class="textProperty">
          </td>
        </tr>
      </tbody>
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
      if(!$('artifactsTree').contains(item)) {
        return;
      }

      //hack for hovering over an 'N kb' text element which sometimes won't work otherwise
      if(item.childNodes.length == 1 && item.childNodes[0].nodeName == "#text")
      {
        item = item.parentElement;
      }

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
        
        btn.onclick = function() {
          event.stopPropagation();
          BS.PortableArtifactLinker.openGenerateDialog(link.href);
        }
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
      $('link_expiry_never_warning').style.display = expValue == "-1" ? "" : "none";
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
        BS.Util.show('btnGenerateLink');
        BS.PortableArtifactLinker.onExpiryChange();
      },

      result: function(transport) {
        if(transport && transport.responseText)
        {
          $('generateResult').innerHTML = transport.responseText;
          if(transport.status == 200)
          {
            BS.Util.hide('dialog_options');
            BS.Util.hide('btnGenerateLink');
          }
          BS.PortableArtifactLinker.GenerateDialog.showCentered();
        }
      },

      generate: function() {
        BS.Util.show('generateProgress');

        BS.ajaxRequest(window['base_uri'] + '${generate_url}', {
          method: "POST",
          parameters: {
            'linkTarget': _href,
            'buildId' : ${portableArtifact_buildId},
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
<style type="text/css">
.portableArtifactLink {
  background: url(${resources}link.png) 0 0 no-repeat;
     width: 16px;
     height: 16px;
     display: inline-block;
     align-self: center;
}
</style>


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
        var artifactLink = link.href; //TODO
        
        var btn = document.createElement("a");
        btn.classList.add("portableArtifactLink");
        btn.textContent = " ";
        btn.target = "_blank";
        btn.href = "/";
        toAdd.appendChild(btn);
      }

      existing = toAdd.lastChild;
      var links = document.getElementsByClassName("portableArtifactLink");
      for(var i = 0; i < links.length; i++)
      {
        links[i].style.display = links[i] == existing ? "" : "none";
      }
    },
  };
  
$(document).on('mouseover', 'li', function(a,b) { BS.PortableArtifactLinker.onLinkHover(b, false); });
$(document).on('mouseover', 'span', function(a,b) { BS.PortableArtifactLinker.onLinkHover(b, true); });

</script>
<style type="text/css">
.portableArtifactLink {

}
</style>


<script type="text/javascript">

  BS.PortableArtifactLinker = {
    onLinkHover: function(item) {
      //TODO: test in all browsers
      if(!item.hasClassName("FileTreeNode.*") || item.firstChild == undefined || item.firstChild.href == undefined)
      {
        return;
      }

      var existing = item.firstChild.lastChild;
      if(existing == undefined || !existing.hasClassName("portableArtifactLink"))
      {
        var artifactLink = item.firstChild.href; //TODO
        
        var btn = document.createElement("a");
        btn.classList.add("portableArtifactLink");
        btn.textContent = "$";
        btn.target = "_blank";
        btn.href = "/";
        item.firstChild.appendChild(btn);
      }

      existing = item.firstChild.lastChild;

      var links = document.getElementsByClassName("portableArtifactLink");
      for(var i = 0; i < links.length; i++)
      {
        links[i].style.display = links[i] == existing ? "" : "none";
      }
    },
  };
  
$(document).on('mouseover', 'li', function(a,b) { BS.PortableArtifactLinker.onLinkHover(b); });

</script>
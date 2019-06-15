package novemberdobby.teamcity.artifactLinkGen;

import java.util.Map;

import javax.servlet.http.HttpServletRequest;

import org.jetbrains.annotations.NotNull;

import jetbrains.buildServer.web.openapi.PagePlaces;
import jetbrains.buildServer.web.openapi.PlaceId;
import jetbrains.buildServer.web.openapi.PluginDescriptor;
import jetbrains.buildServer.web.openapi.SimplePageExtension;

public class ArtifactLinker extends SimplePageExtension {

    PluginDescriptor m_descriptor;
  
    public ArtifactLinker(@NotNull PagePlaces pagePlaces, @NotNull PluginDescriptor descriptor) {
      super(pagePlaces, PlaceId.BUILD_ARTIFACTS_FRAGMENT, Constants.LINKER_ID, descriptor.getPluginResourcesPath(Constants.LINKER_JSP));
      m_descriptor = descriptor;
      register();
    }
  
    @Override
    public boolean isAvailable(@NotNull HttpServletRequest request) {
      return super.isAvailable(request);
    }
  
    @Override
    public void fillModel(@NotNull Map<String, Object> model, @NotNull HttpServletRequest request) {
      model.put("resources", m_descriptor.getPluginResourcesPath());
    }
  }
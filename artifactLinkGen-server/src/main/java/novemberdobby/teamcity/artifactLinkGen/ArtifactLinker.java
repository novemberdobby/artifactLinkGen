package novemberdobby.teamcity.artifactLinkGen;

import java.util.Map;

import javax.servlet.http.HttpServletRequest;

import org.jetbrains.annotations.NotNull;

import jetbrains.buildServer.controllers.BuildDataExtensionUtil;
import jetbrains.buildServer.serverSide.SBuild;
import jetbrains.buildServer.serverSide.SBuildServer;
import jetbrains.buildServer.serverSide.auth.Permission;
import jetbrains.buildServer.users.SUser;
import jetbrains.buildServer.web.openapi.PagePlaces;
import jetbrains.buildServer.web.openapi.PlaceId;
import jetbrains.buildServer.web.openapi.PluginDescriptor;
import jetbrains.buildServer.web.openapi.SimplePageExtension;
import jetbrains.buildServer.web.util.SessionUser;

public class ArtifactLinker extends SimplePageExtension {

    PluginDescriptor m_descriptor;
    SBuildServer m_server;
  
    public ArtifactLinker(@NotNull PagePlaces pagePlaces, @NotNull PluginDescriptor descriptor, @NotNull SBuildServer server) {
      super(pagePlaces, PlaceId.BUILD_ARTIFACTS_FRAGMENT, Constants.LINKER_ID, descriptor.getPluginResourcesPath(Constants.LINKER_JSP));
      m_descriptor = descriptor;
      m_server = server;
      register();
    }
  
    @Override
    public boolean isAvailable(@NotNull HttpServletRequest request) {
      return super.isAvailable(request);
    }
  
    @Override
    public void fillModel(@NotNull Map<String, Object> model, @NotNull HttpServletRequest request) {
      model.put("resources", m_descriptor.getPluginResourcesPath());

      SBuild build = BuildDataExtensionUtil.retrieveBuild(request, m_server);
      model.put("isAdmin", false);

      if(build != null) {
        model.put("portableArtifact_buildId", build.getBuildId());

        SUser user = SessionUser.getUser(request);
        model.put("isAdmin", user != null && user.isPermissionGrantedForProject(build.getProjectId(), Permission.EDIT_PROJECT)); //project admin (developer doesn't count)
      }
    }
  }
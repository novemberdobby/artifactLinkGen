package novemberdobby.teamcity.artifactLinkGen;

import java.util.Map;

import javax.servlet.http.HttpServletRequest;

import org.jetbrains.annotations.NotNull;

import jetbrains.buildServer.controllers.admin.AdminPage;
import jetbrains.buildServer.serverSide.SBuild;
import jetbrains.buildServer.serverSide.SBuildServer;
import jetbrains.buildServer.serverSide.auth.Permission;
import jetbrains.buildServer.users.SUser;
import jetbrains.buildServer.web.openapi.Groupable;
import jetbrains.buildServer.web.openapi.PagePlaces;
import jetbrains.buildServer.web.openapi.PluginDescriptor;
import jetbrains.buildServer.web.util.SessionUser;

public class LinkManager extends AdminPage {

    private SBuildServer m_server;
    private LinkServer m_linkServer;

    public LinkManager(
            @NotNull final SBuildServer server,
            @NotNull final LinkServer linkServer,
            @NotNull final PagePlaces pagePlaces,
            @NotNull final PluginDescriptor descriptor
        ) {

        super(pagePlaces, Constants.MANAGE_TAB_ID, descriptor.getPluginResourcesPath(Constants.MANAGE_TAB_JSP), Constants.MANAGE_TAB_NAME);
        register();
        m_server = server;
        m_linkServer = linkServer;
    }

    @Override
    public String getGroup() {
        return Groupable.SERVER_RELATED_GROUP;
    }

    @Override
    public void fillModel(@NotNull Map<String, Object> model, @NotNull HttpServletRequest request) {
        Map<String, LinkData> links = m_linkServer.getLinks();
        SUser user = SessionUser.getUser(request);

        if(!user.isSystemAdministratorRoleGranted()) {
            //limit to links under projects this user can administrate
            links.entrySet().removeIf(link -> {
                SBuild build = m_server.findBuildInstanceById(link.getValue().getBuildID());
                return build == null || !user.isPermissionGrantedForProject(build.getProjectId(), Permission.EDIT_PROJECT);
            });
        }

        model.put("links", links);
        model.put("usermodel", m_server.getUserModel());
        model.put("server", m_server);
    }
}
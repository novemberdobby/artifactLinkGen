package novemberdobby.teamcity.artifactLinkGen;

import java.util.Map;

import javax.servlet.http.HttpServletRequest;

import org.jetbrains.annotations.NotNull;

import jetbrains.buildServer.controllers.admin.AdminPage;
import jetbrains.buildServer.serverSide.SBuildServer;
import jetbrains.buildServer.web.openapi.Groupable;
import jetbrains.buildServer.web.openapi.PagePlaces;
import jetbrains.buildServer.web.openapi.PluginDescriptor;

public class LinkManager extends AdminPage {
    //TODO: admins see all, others see their own links, link to download artifact

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

        model.put("usermodel", m_server.getUserModel());
        model.put("links", links);
        model.put("server", m_server);
    }
}
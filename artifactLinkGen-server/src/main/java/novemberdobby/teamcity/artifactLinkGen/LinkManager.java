package novemberdobby.teamcity.artifactLinkGen;

import jetbrains.buildServer.controllers.admin.AdminPage;
import jetbrains.buildServer.web.openapi.Groupable;
import jetbrains.buildServer.web.openapi.PagePlaces;
import jetbrains.buildServer.web.openapi.PluginDescriptor;

public class LinkManager extends AdminPage {
    //TODO: admins see all, others see their own links, link to download artifact, show path etc. delete button
    //TODO: link to this page in "expiry: none" tooltip

    LinkServer m_linkServer;

    public LinkManager(LinkServer linkServer, PagePlaces pagePlaces, PluginDescriptor descriptor) {
        super(pagePlaces, Constants.MANAGE_TAB_ID, descriptor.getPluginResourcesPath(Constants.MANAGE_TAB_JSP), Constants.MANAGE_TAB_NAME);
        register();
        m_linkServer = linkServer;
    }

    @Override
    public String getGroup() {
        return Groupable.SERVER_RELATED_GROUP;
    }
}
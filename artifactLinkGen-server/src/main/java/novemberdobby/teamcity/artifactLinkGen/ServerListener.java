package novemberdobby.teamcity.artifactLinkGen;

import jetbrains.buildServer.serverSide.BuildServerAdapter;
import jetbrains.buildServer.serverSide.BuildServerListener;
import jetbrains.buildServer.util.EventDispatcher;

public class ServerListener extends BuildServerAdapter {

    LinkServer m_linkServer;

    public ServerListener(EventDispatcher<BuildServerListener> eventDispatcher, LinkServer linkServer) {
        m_linkServer = linkServer;
        eventDispatcher.addListener(this);
    }

    @Override
    public void serverStartup() {
        m_linkServer.load();
    }
}
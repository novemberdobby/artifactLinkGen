package novemberdobby.teamcity.artifactLinkGen;

import java.util.Date;
import java.util.Map;
import java.util.UUID;

import javax.servlet.http.HttpServletRequest;
import javax.servlet.http.HttpServletResponse;

import com.intellij.util.concurrency.ReentrantLock;

import org.springframework.web.servlet.ModelAndView;

import jetbrains.buildServer.controllers.AuthorizationInterceptor;
import jetbrains.buildServer.controllers.BaseController;
import jetbrains.buildServer.log.Loggers;
import jetbrains.buildServer.users.SUser;
import jetbrains.buildServer.web.openapi.WebControllerManager;
import jetbrains.buildServer.web.util.SessionUser;

public class LinkServer extends BaseController {
    private ReentrantLock m_lock = new ReentrantLock();
    private Map<UUID, LinkData> m_links; //TODO: some way to view & manage links

    public LinkServer(WebControllerManager web, AuthorizationInterceptor authIntercept) {
        web.registerController(Constants.GENERATOR_URL, this);
        authIntercept.addPathNotRequiringAuth(Constants.GENERATOR_URL);
    }

    @Override
    protected ModelAndView doHandle(HttpServletRequest request, HttpServletResponse response) throws Exception {

        SUser user = SessionUser.getUser(request);
        
        if(request.getMethod().equals("GET")) {
            
            String repoLink = request.getParameter("link");

            //only logged-in users can generate
            if(repoLink != null) {
                if(user != null) {
                    //TODO user.isPermissionGrantedForProject(projectId, permission)
                    //TODO return {server url}/{GENERATOR_URL}?guid=X
                } else {
                    response.sendError(HttpServletResponse.SC_BAD_REQUEST, "Only logged-in users can generate portable artifact links");
                    return null;
                }
            } else {
                //anyone can download
                String linkId = request.getParameter("guid");
                //TODO: serve artifact
                //TODO: 404 & server error on missing
            }
        }

        response.sendError(HttpServletResponse.SC_OK);

        return null;
    }

    //TODO: save/load: on generate, on server start, on manual delete etc etc
    private class LinkData {
        Date Generated;
        Long GeneratedByUserID;
        
        Date Expiry;

        Long BuildID;
        String ArtifactPath;
    }

}
package novemberdobby.teamcity.artifactLinkGen;

import java.net.MalformedURLException;
import java.net.URL;
import java.time.Instant;
import java.util.Date;
import java.util.HashMap;
import java.util.Map;
import java.util.UUID;
import java.util.concurrent.locks.ReentrantLock;
import java.util.regex.Matcher;

import javax.servlet.http.HttpServletRequest;
import javax.servlet.http.HttpServletResponse;

import org.springframework.web.servlet.ModelAndView;

import jetbrains.buildServer.controllers.AuthorizationInterceptor;
import jetbrains.buildServer.controllers.BaseController;
import jetbrains.buildServer.log.Loggers;
import jetbrains.buildServer.serverSide.SBuild;
import jetbrains.buildServer.serverSide.SBuildServer;
import jetbrains.buildServer.serverSide.SBuildType;
import jetbrains.buildServer.serverSide.SProject;
import jetbrains.buildServer.serverSide.auth.Permission;
import jetbrains.buildServer.users.SUser;
import jetbrains.buildServer.web.openapi.WebControllerManager;
import jetbrains.buildServer.web.util.SessionUser;

public class LinkServer extends BaseController {

    private SBuildServer m_server;
    private ReentrantLock m_lock = new ReentrantLock();
    private Map<UUID, LinkData> m_links = new HashMap<UUID, LinkData>(); //TODO: some way to view & manage links

    public LinkServer(SBuildServer server, WebControllerManager web, AuthorizationInterceptor authIntercept) {
        m_server = server;
        web.registerController(Constants.CREATE_URL, this);
        
        web.registerController(Constants.GET_URL, this);
        authIntercept.addPathNotRequiringAuth(Constants.GET_URL);
    }

    @Override
    protected ModelAndView doHandle(HttpServletRequest request, HttpServletResponse response) throws Exception {

        SUser user = SessionUser.getUser(request);
        String originator = String.format("[%s]:%s", request.getRemoteAddr(), request.getRemotePort());

        if(!request.getMethod().equals("GET")) {
            return null;
        }

        if(request.getRequestURI().equals(Constants.CREATE_URL)) {
            
            //only logged-in users can get to this point
            String repoLink = request.getParameter("linkTarget");
            URL url = null;
            try {
                url = new URL(repoLink);
            } catch (MalformedURLException e) {
                return Util.sendErrorBody(response, "Malformed url: %s", repoLink);
            }

            //extract the build ID & artifact path
            Matcher mtch = Constants.LINK_PATTERN.matcher(url.getPath());
            if(!mtch.find()) {
                return Util.sendErrorBody(response, "Malformed path: %s", url.getPath());
            }
        
            String buildIdStr = request.getParameter("buildId");
            Long buildId = Util.parseLong(buildIdStr, -1L);

            String buildIdStrLink = mtch.group("id");
            String artifact = mtch.group("path");
            
            //check the target build exists
            SBuild build = m_server.findBuildInstanceById(buildId);
            SBuildType buildType = build == null ? null : build.getBuildType();

            if(buildType == null || !buildIdStrLink.equals(buildIdStr)) { //second comparison is just a safety check
                return Util.sendErrorBody(response, "Build ID or type ID was invalid (%s, %s)", buildIdStr, buildIdStrLink);
            }

            SProject parentProj = buildType.getProject();
            if(parentProj == null || !user.isPermissionGrantedForProject(parentProj.getProjectId(), Permission.VIEW_PROJECT)) {
                return Util.sendErrorBody(response, "Build's parent project doesn't exist or user has no access");
            }

            Long expiryMins = -1L;
            String expiryStr = request.getParameter("expiry");

            if("none".equals(expiryStr)) {
                expiryMins = -1L;
            } else if("custom".equals(expiryStr)) {
                expiryMins = Util.parseLong(request.getParameter("expiry_custom"), 15L);
            } else {
                expiryMins = Util.parseLong(expiryStr, 15L);
            }

            LinkData link = new LinkData(user, expiryMins, buildId, artifact);
            UUID uid = UUID.randomUUID();

            m_lock.lock();
            try {
                m_links.put(uid, link);
                save();
            } finally {
                m_lock.unlock();
            }

            Loggers.SERVER.info(String.format("[PortableArtifacts] User %s generated link %s: %s", user.getUsername(), uid, link));
            String finalLink = String.format("%s%s?guid=%s", m_server.getRootUrl(), Constants.GET_URL, uid);

            response.setStatus(HttpServletResponse.SC_OK);
            response.getWriter().write(String.format("<a href='%s'>%s</a>", finalLink, finalLink));

        } else {
            //TODO test on https
            //if(!request.isSecure()) {
            //    response.sendError(HttpServletResponse.SC_FORBIDDEN, "Insecure request");
            //    return null;
            //}

            String uidStr = request.getParameter("guid");
            UUID uid = null;
            try {
                uid = UUID.fromString(uidStr);
            } catch(IllegalArgumentException e) { }
            
            LinkData data = null;

            m_lock.lock();
            try {
                data = m_links.get(uid);

                //expired? delete it now
                if(data != null && data.Expiry != null && data.Expiry.before(Date.from(Instant.now()))) {
                    m_links.remove(uid);
                    data = null;
                    save();
                }
            } finally {
                m_lock.unlock();
            }

            if(data == null) {
                Loggers.SERVER.error(String.format("[PortableArtifacts] Unknown ID for portable artifact link with ID '%s'. Source: %s", uidStr, originator));
                response.sendError(HttpServletResponse.SC_NOT_FOUND, "Unknown ID for portable artifact link");
                return null;
            }

            Loggers.SERVER.info(String.format("[PortableArtifacts] Serving artifact %s to %s: %s", uidStr, originator, data));

            //TODO: serve artifact
            //TODO: support single use token - remove & save on download start
        }

        return null;
    }

    private class LinkData {
        Date Generated;
        Long GeneratedByUserID;
        
        Date Expiry;

        Long BuildID;
        String ArtifactPath;

        public LinkData(SUser user, Long expiryMins, Long buildId, String artifactPath) {
            Generated = Date.from(Instant.now());
            GeneratedByUserID = user.getId();

            if(expiryMins > 0) {
                Expiry = Date.from(Generated.toInstant().plusSeconds(expiryMins * 60)); //hmm
            }

            BuildID = buildId;
            ArtifactPath = artifactPath;
        }

        @Override
        public String toString() {
            return String.format("Generated by user %s at %s, expires after %s", GeneratedByUserID, Generated, Expiry == null ? "<never>" : Expiry.toString());
        }
    }

    //TODO on manual delete
    public void save() {
        m_lock.lock();
        try {
            //TODO
        } finally {
            m_lock.unlock();
        }
    }

    //TODO on server start
    public void load() {
        m_lock.lock();
        try {
            //TODO
        } finally {
            m_lock.unlock();
        }
    }
}
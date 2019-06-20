package novemberdobby.teamcity.artifactLinkGen;

import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.InputStream;
import java.io.InputStreamReader;
import java.io.OutputStreamWriter;
import java.io.Reader;
import java.io.Writer;
import java.lang.reflect.Type;
import java.net.MalformedURLException;
import java.net.URL;
import java.nio.file.Paths;
import java.util.HashMap;
import java.util.Map;
import java.util.UUID;
import java.util.concurrent.locks.ReentrantLock;
import java.util.regex.Matcher;

import javax.servlet.ServletOutputStream;
import javax.servlet.http.HttpServletRequest;
import javax.servlet.http.HttpServletResponse;

import com.google.gson.Gson;
import com.google.gson.GsonBuilder;
import com.google.gson.reflect.TypeToken;

import org.springframework.web.servlet.ModelAndView;

import jetbrains.buildServer.controllers.AuthorizationInterceptor;
import jetbrains.buildServer.controllers.BaseController;
import jetbrains.buildServer.log.Loggers;
import jetbrains.buildServer.serverSide.SBuild;
import jetbrains.buildServer.serverSide.SBuildServer;
import jetbrains.buildServer.serverSide.SBuildType;
import jetbrains.buildServer.serverSide.SProject;
import jetbrains.buildServer.serverSide.ServerPaths;
import jetbrains.buildServer.serverSide.artifacts.BuildArtifact;
import jetbrains.buildServer.serverSide.artifacts.BuildArtifacts;
import jetbrains.buildServer.serverSide.artifacts.BuildArtifactsViewMode;
import jetbrains.buildServer.serverSide.auth.Permission;
import jetbrains.buildServer.users.SUser;
import jetbrains.buildServer.web.openapi.WebControllerManager;
import jetbrains.buildServer.web.util.SessionUser;

public class LinkServer extends BaseController {

    private SBuildServer m_server;
    private ServerPaths m_serverPaths;
    private ReentrantLock m_lock = new ReentrantLock();
    private Map<String, LinkData> m_links = new HashMap<String, LinkData>(); //TODO: some way to view & manage links

    public LinkServer(SBuildServer server, ServerPaths serverPaths, WebControllerManager web, AuthorizationInterceptor authIntercept) {
        m_server = server;
        m_serverPaths = serverPaths;

        //generating links
        web.registerController(Constants.CREATE_URL, this);
        
        //serving artifacts via a link
        web.registerController(Constants.GET_URL, this);
        authIntercept.addPathNotRequiringAuth(Constants.GET_URL);

        //managing links
        web.registerController(Constants.MANAGE_URL, this);
    }

    @Override
    protected ModelAndView doHandle(HttpServletRequest request, HttpServletResponse response) throws Exception {

        SUser user = SessionUser.getUser(request);
        String originator = String.format("[%s]:%s", request.getRemoteAddr(), request.getRemotePort());

        if(request.getRequestURI().equals(Constants.CREATE_URL) && request.getMethod().equals("POST")) {
            
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

            if(!user.isPermissionGrantedForProject(parentProj.getProjectId(), Permission.EDIT_PROJECT)) {
                expiryMins = Math.max(Math.min(expiryMins, 15), 5);
            }

            LinkData link = new LinkData(user, expiryMins, buildId, artifact);
            String uid = UUID.randomUUID().toString();

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

        } else if(request.getRequestURI().equals(Constants.MANAGE_URL) && request.getMethod().equals("POST")) {

            //TODO: check admin (at least of owning project or on server), log the deletion
            String uid = request.getParameter("guid");

            m_lock.lock();
            try {
                m_links.remove(uid);
                save();
            } finally {
                m_lock.unlock();
            }

        } else if(request.getRequestURI().equals(Constants.GET_URL) && request.getMethod().equals("GET")) {

            //TODO test on https
            //if(!request.isSecure()) {
            //    response.sendError(HttpServletResponse.SC_FORBIDDEN, "Insecure request");
            //    return null;
            //}

            //report the logged-in user if they exist
            String getOriginator = user != null ? user.getUsername() : originator;

            String uid = request.getParameter("guid");
            LinkData link = null;

            m_lock.lock();
            try {
                link = m_links.get(uid);

                if(link != null && link.hasExpired()) {
                    m_links.remove(uid);
                    link = null;
                    save();
                }
            } finally {
                m_lock.unlock();
            }

            if(link == null) {
                Loggers.SERVER.error(String.format("[PortableArtifacts] Unknown ID for portable artifact link with ID '%s'. Source: %s", uid, getOriginator));
                response.sendError(HttpServletResponse.SC_NOT_FOUND, "Unknown ID for portable artifact link");
                return null;
            }

            Loggers.SERVER.info(String.format("[PortableArtifacts] Serving artifact %s to %s: %s", uid, getOriginator, link));

            InputStream inStream = null;
            SBuild build = m_server.findBuildInstanceById(link.getBuildID());
            if(build != null) {
                BuildArtifacts arts = build.getArtifacts(BuildArtifactsViewMode.VIEW_ALL_WITH_ARCHIVES_CONTENT);
                BuildArtifact artifact = arts.getArtifact(link.getArtifactPath());

                if(artifact != null) {
                    inStream = artifact.getInputStream();
                }
            }

            if(inStream == null) {
                response.sendError(HttpServletResponse.SC_NOT_FOUND, "Build or artifact no longer exists");
                return null;
            } else {

                String fileName = link.getArtifactPath();

                //normalise to final path name if it's inside an archive
                int lastSlash = fileName.lastIndexOf('/');
                if(lastSlash != -1) {
                    fileName = fileName.substring(lastSlash + 1);
                }

                response.setHeader("Content-disposition", String.format("attachment; filename=%s", fileName));

                try {
                    ServletOutputStream outStream = response.getOutputStream();

                    byte[] output = new byte[2048];
                    int read = 0;
                    while((read = inStream.read(output, 0, output.length)) > 0)
                    {
                        outStream.write(output, 0, read);
                    }
                    outStream.close();
                } finally {
                    inStream.close();
                }
            }

            //TODO: support single use token - remove & save on download start. don't remove on coming from admin page (check perms)
            /*if("1".equals(request.getParameter("fromAdminPage")) && user != null && ) {

            }*/
        }

        return null;
    }

    public void save() {
        m_lock.lock();
        try {
            Writer writer = new OutputStreamWriter(new FileOutputStream(getSavePath()) , "UTF-8");
            Gson gson = new GsonBuilder().create();

            gson.toJson(m_links, writer);
            writer.close();
        } catch(Exception ex) {
            Loggers.SERVER.error("Failed to save portable artifact link data: " + ex.toString());
        } finally {
            m_lock.unlock();
        }
    }

    public void load() {
        m_lock.lock();
        try {
            Reader reader = new InputStreamReader(new FileInputStream(getSavePath()) , "UTF-8");
            Gson gson = new GsonBuilder().create();

            Type type = new TypeToken<Map<String, LinkData>>(){}.getType();
            m_links = gson.fromJson(reader, type);
            reader.close();
        }
        catch(Exception ex) {
            Loggers.SERVER.error("Failed to load portable artifact link data: " + ex.toString());
        } finally {
            m_lock.unlock();
        }
    }

    private String getSavePath() {
        return Paths.get(m_serverPaths.getConfigDir(), "portable_artifact_links.json").toString();
    }

    public Map<String, LinkData> getLinks() {
        m_lock.lock();
        try {
            if(m_links.entrySet().removeIf(link -> link.getValue().hasExpired())) {
                save();
            }

            return new HashMap<String, LinkData>(m_links);
        } finally {
            m_lock.unlock();
        }
    }
}
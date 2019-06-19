package novemberdobby.teamcity.artifactLinkGen;

import java.util.regex.Pattern;

public class Constants {
    public static final String LINKER_ID = "portableArtifactLinker";
    public static final String LINKER_JSP = "linker.jsp";
    
    public static final String CREATE_URL = "/portable_artifact_generate.html";
    public static final String GET_URL = "/portable_artifact.html";
    public static final String MANAGE_URL = "/portable_artifact_manage.html";
    
    public static final Integer NON_ADMIN_MIN_TIME = 5;
    public static final Integer NON_ADMIN_MAX_TIME = 15;

    public static final Pattern LINK_PATTERN = Pattern.compile("/(?<id>\\d+):id/(?<path>.*)");
    
    public static final String MANAGE_TAB_ID = "manage_portable_artifacts";
	public static final String MANAGE_TAB_NAME = "Portable Artifacts";
	public static final String MANAGE_TAB_JSP = "manage_links.jsp";
}
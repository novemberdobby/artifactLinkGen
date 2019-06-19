package novemberdobby.teamcity.artifactLinkGen;

import java.util.regex.Pattern;

public class Constants {
    public static final String LINKER_ID = "portableArtifactLinker";
    public static final String LINKER_JSP = "linker.jsp";
    public static final String CREATE_URL = "/portable_artifact_generate.html";
    public static final String GET_URL = "/portable_artifact.html";
    public static final Pattern LINK_PATTERN = Pattern.compile("/(?<id>\\d+):id/(?<path>.*)");
	public static final String MANAGE_TAB_ID = "manage_portable_artifacts";
	public static final String MANAGE_TAB_NAME = "Portable Artifacts";
	public static final String MANAGE_TAB_JSP = "manage_links.jsp";
}
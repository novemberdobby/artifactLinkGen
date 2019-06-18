package novemberdobby.teamcity.artifactLinkGen;

import javax.servlet.http.HttpServletResponse;

import org.springframework.web.servlet.ModelAndView;

public class Util {
    public static ModelAndView sendErrorBody(HttpServletResponse response, String message, Object... fmtArgs) {
        response.setStatus(HttpServletResponse.SC_BAD_REQUEST);

        try {
            response.getWriter().write(String.format(message, fmtArgs));
        } catch (Exception e) { }

        return null;
    }

    public static Long parseLong(String input, Long defaultValue) {
        try {
            return Long.parseLong(input);
        } catch (NumberFormatException e) {
            return defaultValue;
        }
    }
}
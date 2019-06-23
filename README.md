# Portable Artifact Links - TeamCity plugin for generating static, authenticationless links to build artifacts
![demo](/images/demo.gif)

Any artifact can be linked, including those within archives. Users don't need to be logged in to download them!

Project/server adminis can manage links through the server administration page:

![link_manager_page](/images/link_manager_page.png)

Non-admin users can only generate links that last up to 15 minutes.

For https://youtrack.jetbrains.com/issue/TW-46777

## Building
This plugin is built with Maven. To compile it, run the following in the root folder:

```
mvn package
```
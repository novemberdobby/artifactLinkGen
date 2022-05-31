namespace HadesBoonBot
{
    internal class Util
    {
        internal static string CreateDir(string path, bool deleteFirst = false)
        {
            bool exists = Directory.Exists(path);
            bool create = !exists || deleteFirst;

            if (deleteFirst && exists)
            {
                Directory.Delete(path, true);
            }

            if (create)
            {
                Directory.CreateDirectory(path);
            }

            return path;
        }

        internal async static void DownloadFile(HttpClient client, string url, string targetFile, bool dontOverwrite = true)
        {
            //"asynchronous"
            if (!(File.Exists(targetFile) && dontOverwrite))
            {
                using var stream = await client.GetStreamAsync(url);
                using var fstream = new FileStream(targetFile, FileMode.CreateNew);
                await stream.CopyToAsync(fstream);
            }
        }
    }
}
